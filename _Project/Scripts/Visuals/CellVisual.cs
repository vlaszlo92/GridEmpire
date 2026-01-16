using GridEmpire.Core;
using UnityEngine;

namespace GridEmpire.Visuals
{
    public class CellVisual : MonoBehaviour, ICellPresenter
    {
        [SerializeReference] private CellData _data;
        private Renderer _renderer;
        private MaterialPropertyBlock _propBlock;
        [SerializeField] private GameObject selectionEffect;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        public CellData Data => _data;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();
            if (selectionEffect != null) selectionEffect.SetActive(false);
        }

        public void Initialize(CellData data)
        {
            _data = data;
            _data.OnVisualUpdateRequired += UpdateVisual;
            UpdateVisual();
        }

        public void SetSelected(bool isSelected)
        {
            if (selectionEffect != null) selectionEffect.SetActive(isSelected);
        }

        public void UpdateVisual()
        {
            if (_data == null || _renderer == null) return;

            Color baseColor = _data.OwnerId switch { 0 => Color.blue, 1 => Color.red, _ => Color.gray };
            Color finalColor = _data.CurrentVisibility switch
            {
                VisibilityState.Hidden => Color.black,
                VisibilityState.Explored => Color.Lerp(Color.black, baseColor, 0.2f),
                _ => baseColor
            };

            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(ColorProperty, finalColor);
            _renderer.SetPropertyBlock(_propBlock);
        }

        public void DebugUpdateVisual()
        {
            if (_data == null || _renderer == null) return;

            // 1. Alapszín meghatározása a tulajdonos alapján
            // 0: Játékos (Kék), 1: AI (Piros), minden más (Szürke/Semleges)
            Color baseColor = _data.OwnerId switch
            {
                0 => new Color(0.2f, 0.4f, 1.0f), // Világosabb kék
                1 => new Color(1.0f, 0.2f, 0.2f), // Világosabb piros
                _ => new Color(0.3f, 0.3f, 0.3f)  // Sötétszürke az üres mezõknek
            };

            // 2. DEBUG MÓD: Figyelmen kívül hagyjuk a VisibilityState-et
            // Így nem lesz fekete (Hidden) vagy sötétített (Explored) mezõ.
            Color finalColor = baseColor;

            // 3. EXTRA: Ha a mezõ bázis, tegyük fényesebbé, hogy lássuk a térképen
            if (_data.IsBase)
            {
                finalColor *= 1.5f; // Kicsit "izzik" a bázis
            }

            // 4. Material Property Block frissítése (teljesítménybarát módon)
            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(ColorProperty, finalColor);
            _renderer.SetPropertyBlock(_propBlock);
        }
    }
}