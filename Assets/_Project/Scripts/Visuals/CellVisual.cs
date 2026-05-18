using GridEmpire.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GridEmpire.Visuals
{
    public class CellVisual : MonoBehaviour, ICellPresenter
    {
        [Header("Settings")]
        [SerializeField] private GameObject selectionEffect;
        // Ezzel a kapcsolóval a szerkesztõben (Inspector) is tudsz váltani
        public static bool IsDebugMode = GameController.IsDebugMode;

        [SerializeReference] private CellData _data;
        private Renderer _renderer;
        private MaterialPropertyBlock _propBlock;

        private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");

        // Statikus cache a teljesítményért
        private static Dictionary<int, Color> _playerColorCache;
        private static readonly Color NeutralColor = new Color(0.3f, 0.3f, 0.3f);

        public CellData Data => _data;

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            _propBlock = new MaterialPropertyBlock();
            if (selectionEffect != null) selectionEffect.SetActive(false);

            EnsureColorCacheInitialized();
        }

        private void EnsureColorCacheInitialized()
        {
            if (_playerColorCache == null)
                _playerColorCache = new Dictionary<int, Color>();

            var players = GameController.Instance?.Players;
            if (players == null || players.Count == 0) return;

            foreach (var p in players)
                _playerColorCache[p.Id] = p.Color;
        }

        public void Initialize(CellData data)
        {
            _data = data;
            _data.OnVisualUpdateRequired += UpdateVisual;
            UpdateVisual();

            SetDebugMode(IsDebugMode);
        }

        public void SetSelected(bool isSelected)
        {
            if (selectionEffect != null) selectionEffect.SetActive(isSelected);
        }

        public void UpdateVisual()
        {
            if (_data == null || _renderer == null) return;
            EnsureColorCacheInitialized();
            Color baseColor = NeutralColor;

            if (_data.OwnerId != -1)
            {
                // Van owner – de nézzük meg csökken-e az influence
                if (_playerColorCache.TryGetValue(_data.OwnerId, out Color ownerColor))
                {
                    float progress = _data.GetCaptureProgress(_data.OwnerId);
                    // Ha az influence csökken, halványítjuk a színt
                    baseColor = Color.Lerp(NeutralColor, ownerColor, progress);
                }
            }
            else if (_data.InfluenceDisplay.Count > 0)
            {
                var topInfiltrator = _data.InfluenceDisplay
                    .OrderByDescending(i => i.Influence)
                    .FirstOrDefault(i => i.Influence > 0);

                if (topInfiltrator != null && _playerColorCache.TryGetValue(topInfiltrator.PlayerId, out Color infColor))
                {
                    baseColor = Color.Lerp(NeutralColor, infColor, topInfiltrator.Influence);
                }
            }

            if (_data.IsBase) baseColor *= 1.5f;

            Color finalColor;
            if (IsDebugMode)
            {
                finalColor = baseColor;
            }
            else
            {
                finalColor = _data.CurrentVisibility switch
                {
                    VisibilityState.Hidden => Color.black,
                    VisibilityState.Explored => Color.Lerp(Color.black, baseColor, 0.63f),
                    _ => baseColor
                };
            }

            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(ColorProperty, finalColor);
            _renderer.SetPropertyBlock(_propBlock);

            if (FogOfWarManager.Instance != null)
            {
                bool shouldHaveFog = !IsDebugMode &&
                    _data.CurrentVisibility == VisibilityState.Hidden;
                FogOfWarManager.Instance.SetFog(this, shouldHaveFog);
            }
        }

        // Segédfüggvény a Debug Mode váltásához kódból
        public static void SetDebugMode(bool enabled)
        {
            IsDebugMode = enabled;
            // Frissíteni kell minden látható mezõt a váltáskor
            var allVisuals = Object.FindObjectsByType<CellVisual>(FindObjectsSortMode.None);
            foreach (var v in allVisuals) v.UpdateVisual();
        }
    }
}