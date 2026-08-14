using System.Collections.Generic;
using System.Linq;
using GridEmpire.Core;
using TMPro;
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

        [Header("Label Setup")]
        [SerializeField] private RectTransform labelContainer;
        [SerializeField] private RectTransform labelPrefab;
        [SerializeField] private float minLabelWidth = 105f;
        [SerializeField] private float minSpacing = 10f;
        [SerializeField] private float rowHeight = 20f;

        [Header("VFX Divider")]
        [SerializeField] private ParticleSystem dividerVfxPrefab;

        private readonly List<Image> _segments = new List<Image>();
        private readonly List<RectTransform> _dividers = new List<RectTransform>();
        private readonly List<TextMeshProUGUI> _labels = new List<TextMeshProUGUI>();

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
            if (labelContainer != null)
            {
                foreach (Transform child in labelContainer) Destroy(child.gameObject);
            }

            _segments.Clear();
            _dividers.Clear();
            _labels.Clear();

            for (int i = 0; i < players.Count; i++)
            {
                Image seg = Instantiate(segmentImagePrefab, barContainer);
                seg.color = players[i].Color;
                seg.gameObject.SetActive(true);
                _segments.Add(seg);

                if (labelPrefab != null && labelContainer != null)
                {
                    RectTransform lblRt = Instantiate(labelPrefab, labelContainer);
                    lblRt.gameObject.SetActive(true);

                    TextMeshProUGUI lbl = lblRt.GetComponentInChildren<TextMeshProUGUI>();
                    if (lbl != null) _labels.Add(lbl);
                }
            }

            Image unclaimedSeg = Instantiate(segmentImagePrefab, barContainer);
            unclaimedSeg.color = unclaimedColor;
            unclaimedSeg.gameObject.SetActive(true);
            _segments.Add(unclaimedSeg);

            if (labelPrefab != null && labelContainer != null)
            {
                RectTransform unclaimedLblRt = Instantiate(labelPrefab, labelContainer);
                unclaimedLblRt.gameObject.SetActive(true);

                TextMeshProUGUI unclaimedLbl = unclaimedLblRt.GetComponentInChildren<TextMeshProUGUI>();
                if (unclaimedLbl != null) _labels.Add(unclaimedLbl);
            }

            for (int i = 0; i < segmentCount - 1; i++)
            {
                var vfx = Instantiate(dividerVfxPrefab, barContainer);
                var rt = vfx.GetComponent<RectTransform>();
                if (rt == null)
                {
                    Debug.LogError("TerritoryBarUI: dividerVfxPrefab-on nincs RectTransform! UI elemkent kell letrehozni.");
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
            if (GameController.Instance == null) return;

            float t = Mathf.Clamp01(_tickTimer / GetDuration());
            for (int i = 0; i < _fromFractions.Length; i++)
                _fromFractions[i] = Mathf.Lerp(_fromFractions[i], _targetFractions[i], t);

            RecalculateTargets();
            _tickTimer = 0f;
        }

        private float GetDuration() => TurnManager.Instance != null ? TurnManager.Instance.TickDuration : 1f;

        private void RecalculateTargets()
        {
            if (GameController.Instance == null) return;

            var players = GameController.Instance.Players;
            if (players.Count + 1 != _targetFractions.Length) return;

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
            var players = GameController.Instance != null ? GameController.Instance.Players : null;
            int ownedTotal = 0;

            float containerWidth = barContainer.rect.width;

            float[] lastRightEdgeByRow = new float[] { float.MinValue, float.MinValue };
            int lastAssignedRow = -1;

            for (int i = 0; i < _segments.Count; i++)
            {
                float frac = Mathf.Lerp(_fromFractions[i], _targetFractions[i], t);
                var rt = _segments[i].rectTransform;

                float startFrac = cumulative;
                cumulative += frac;

                rt.anchorMin = new Vector2(startFrac, 0f);
                rt.anchorMax = new Vector2(cumulative, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                if (i < _labels.Count)
                {
                    var label = _labels[i];
                    var mainRt = label.transform.parent as RectTransform;

                    label.horizontalAlignment = HorizontalAlignmentOptions.Left;

                    // Beállítjuk a szöveget előre, hogy le tudjuk kérni a tényleges szélességét
                    if (players != null && i < players.Count)
                    {
                        var player = players[i];
                        label.text = $"{player.Name}: {player.OwnedCellCount}";
                        ownedTotal += player.OwnedCellCount;
                    }
                    else
                    {
                        int unclaimedCount = Mathf.Max(0, _totalCellCount - ownedTotal);
                        label.text = $"Unoccupied: {unclaimedCount}";
                    }

                    // Kiszámoljuk a ténylegesen szükséges szélességet (szöveg + padding)
                    float preferredWidth = label.preferredWidth + 12f; // 12px padding
                    float currentLabelWidth = Mathf.Max(minLabelWidth, preferredWidth);

                    float idealCenterX = (startFrac + (frac * 0.5f) - 0.5f) * containerWidth;
                    float halfWidth = currentLabelWidth * 0.5f;
                    float idealLeft = idealCenterX - halfWidth;

                    int targetRow = 0;
                    float actualLeft = idealLeft;

                    bool fitsIdeallyInRow0 = (lastRightEdgeByRow[0] == float.MinValue || idealLeft >= lastRightEdgeByRow[0] + minSpacing);

                    if (fitsIdeallyInRow0 && lastAssignedRow != 0)
                    {
                        targetRow = 0;
                        actualLeft = idealLeft;
                    }
                    else
                    {
                        targetRow = (lastAssignedRow == 0) ? 1 : 0;

                        float minAllowedLeft = lastRightEdgeByRow[targetRow] == float.MinValue
                            ? idealLeft
                            : lastRightEdgeByRow[targetRow] + minSpacing;

                        actualLeft = Mathf.Max(idealLeft, minAllowedLeft);
                    }

                    lastAssignedRow = targetRow;

                    float actualCenterX = actualLeft + halfWidth;
                    float actualRight = actualLeft + currentLabelWidth;

                    lastRightEdgeByRow[targetRow] = actualRight;

                    if (mainRt != null)
                    {
                        mainRt.anchorMin = new Vector2(0.5f, 1f);
                        mainRt.anchorMax = new Vector2(0.5f, 1f);
                        mainRt.pivot = new Vector2(0.5f, 1f);
                        mainRt.sizeDelta = new Vector2(currentLabelWidth, rowHeight);

                        float posY = -targetRow * rowHeight;
                        mainRt.anchoredPosition = new Vector2(actualCenterX, posY);
                    }
                }

                if (i < _dividers.Count)
                    _dividers[i].anchoredPosition = new Vector2((cumulative - 0.5f) * containerWidth, 0f);
            }
        }
    }
}