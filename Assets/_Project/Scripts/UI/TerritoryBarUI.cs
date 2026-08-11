using System.Collections.Generic;
using System.Linq;
using GridEmpire.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GridEmpire.UI
{
    public class TerritoryBarUI : MonoBehaviour
    {
        [Header("Bar Setup")]
        [SerializeField] private RectTransform barContainer;
        [SerializeField] private Image segmentImagePrefab;
        [SerializeField] private Color unclaimedColor = new Color(0.4f, 0.4f, 0.4f);

        [Header("VFX Divider")]
        [SerializeField] private ParticleSystem dividerVfxPrefab;

        private readonly List<Image> _segments = new List<Image>();
        private readonly List<RectTransform> _dividers = new List<RectTransform>();

        private float[] _fromFractions;
        private float[] _targetFractions;
        private int _totalCellCount = -1;
        private float _tickTimer;
        private bool _built = false;

        private void OnEnable()
        {
            TurnManager.OnTurnCompleted += HandleTurnCompleted;
        }

        private void OnDisable()
        {
            TurnManager.OnTurnCompleted -= HandleTurnCompleted;
        }

        private void Update()
        {
            if (!_built)
            {
                TryBuild();
                return;
            }

            float duration = GetDuration();
            _tickTimer = Mathf.Min(_tickTimer + Time.deltaTime, duration);
            float t = duration > 0f ? _tickTimer / duration : 1f;

            ApplyFractions(t);
        }

        private void TryBuild()
        {
            if (GameController.Instance == null || GridManager.Instance == null || !GridManager.Instance.IsReady) return;

            var players = GameController.Instance.Players;
            if (players == null || players.Count == 0) return;

            _totalCellCount = GridManager.Instance.GetAllCells().Count();
            if (_totalCellCount <= 0) return;

            int segmentCount = players.Count + 1;
            _fromFractions = new float[segmentCount];
            _targetFractions = new float[segmentCount];

            foreach (Transform child in barContainer) Destroy(child.gameObject);
            _segments.Clear();
            _dividers.Clear();

            for (int i = 0; i < players.Count; i++)
            {
                Image seg = Instantiate(segmentImagePrefab, barContainer);
                seg.color = players[i].Color;
                seg.gameObject.SetActive(true);
                _segments.Add(seg);
            }

            Image unclaimedSeg = Instantiate(segmentImagePrefab, barContainer);
            unclaimedSeg.color = unclaimedColor;
            unclaimedSeg.gameObject.SetActive(true);
            _segments.Add(unclaimedSeg);

            for (int i = 0; i < segmentCount - 1; i++)
            {
                var vfx = Instantiate(dividerVfxPrefab, barContainer);
                var rt = vfx.GetComponent<RectTransform>();
                if (rt == null)
                {
                    Debug.LogError("TerritoryBarUI: dividerVfxPrefab-on nincs RectTransform! UI elemként kell létrehozni.");
                    Destroy(gameObject);
                    return;
                }
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition3D = Vector3.zero;
                _dividers.Add(rt);
            }

            RecalculateTargets();
            for (int i = 0; i < segmentCount; i++) _fromFractions[i] = _targetFractions[i];

            _tickTimer = GetDuration();
            ApplyFractions(1f);

            _built = true;
        }

        private void HandleTurnCompleted()
        {
            if (!_built) return;

            float t = Mathf.Clamp01(_tickTimer / GetDuration());
            for (int i = 0; i < _fromFractions.Length; i++)
                _fromFractions[i] = Mathf.Lerp(_fromFractions[i], _targetFractions[i], t);

            RecalculateTargets();
            _tickTimer = 0f;
        }

        private float GetDuration() => TurnManager.Instance != null ? TurnManager.Instance.TickDuration : 1f;

        private void RecalculateTargets()
        {
            var players = GameController.Instance.Players;
            int ownedTotal = 0;
            for (int i = 0; i < players.Count; i++)
            {
                _targetFractions[i] = Mathf.Max(0.01f, (float)players[i].OwnedCellCount / _totalCellCount);
                ownedTotal += players[i].OwnedCellCount;
            }
            _targetFractions[players.Count] = Mathf.Max(0.01f, (float)Mathf.Max(0, _totalCellCount - ownedTotal) / _totalCellCount);

            float sum = _targetFractions.Sum();
            for (int i = 0; i < _targetFractions.Length; i++)
                _targetFractions[i] /= sum;
        }

        private void ApplyFractions(float t)
        {
            float cumulative = 0f;
            for (int i = 0; i < _segments.Count; i++)
            {
                float frac = Mathf.Lerp(_fromFractions[i], _targetFractions[i], t);
                var rt = _segments[i].rectTransform;
                rt.anchorMin = new Vector2(cumulative, 0f);
                cumulative += frac;
                rt.anchorMax = new Vector2(cumulative, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                if (i < _dividers.Count)
                    _dividers[i].anchoredPosition = new Vector2((cumulative - 0.5f) * barContainer.rect.width, 0f);
            }
        }
    }
}