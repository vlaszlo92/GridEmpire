// UIManager.cs - GridEmpire.UI namespace, UI mappa
using GridEmpire.Core;
using GridEmpire.Gameplay;
using GridEmpire.Input;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GridEmpire.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("Resources & Info")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI turnText;

        [Header("Spawn Buttons")]
        [SerializeField] private Button axemanBtn;
        [SerializeField] private Button spearmanBtn;
        [SerializeField] private Button cavalryBtn;
        [SerializeField] private Button scoutBtn;

        [Header("Queue UI")]
        [SerializeField] private Transform queueContainer;
        [SerializeField] private GameObject queueIconPrefab;
        [SerializeField] private Button clearQueueBtn;

        [Header("Unit Info Panel")]
        [SerializeField] private GameObject infoPanelRoot;
        [SerializeField] private TextMeshProUGUI unitNameText;
        [SerializeField] private TextMeshProUGUI unitOwnerText;
        [SerializeField] private TextMeshProUGUI unitHpText;
        [SerializeField] private TextMeshProUGUI unitDamageText;
        [SerializeField] private TextMeshProUGUI unitStaminaText;

        [Header("Selector Switch Panel")]
        [SerializeField] private GameObject selectorPanelRoot;
        [SerializeField] private Button selectorBtn;
        [SerializeField] private Sprite selectorFieldImage, selectorUnitImage;

        private PlayerProfile _localPlayer;
        private UnitSpawner _localSpawner;
        private UnitController _selectedUnit;
        private List<QueueIconRefs> _iconRefs = new List<QueueIconRefs>();
        private float _tickTimer = 0f;
        private bool _isFieldSelected = true;

        private class QueueIconRefs
        {
            public GameObject root;
            public Image fillImage;
            public Image iconImage;
            public TextMeshProUGUI tickText;
            public TextMeshProUGUI nameText;
            public Button iconButton;
        }

        private void Start()
        {
            axemanBtn.onClick.AddListener(() => RequestSpawn(0));
            spearmanBtn.onClick.AddListener(() => RequestSpawn(1));
            cavalryBtn.onClick.AddListener(() => RequestSpawn(2));
            scoutBtn.onClick.AddListener(() => RequestSpawn(3));
            if (clearQueueBtn != null) clearQueueBtn.onClick.AddListener(HandleClearQueue);
        }

        private void OnEnable()
        {
            TurnManager.OnTurnCompleted += ResetVisualTimer;
            TurnManager.OnTurnCompleted += RefreshGoldDisplay;
            GameController.OnUnitSelected += HandleUnitSelectionChanged;
        }

        private void OnDisable()
        {
            TurnManager.OnTurnCompleted -= ResetVisualTimer;
            TurnManager.OnTurnCompleted -= RefreshGoldDisplay;
            GameController.OnUnitSelected -= HandleUnitSelectionChanged;
        }

        private void RefreshGoldDisplay()
        {
            goldText.text = "Gold: " + _localPlayer.Gold.ToString();
            return;

            var sb = new System.Text.StringBuilder();
            foreach (var p in GameController.Instance.GetPlayers())
            {
                string sign = p.GoldIncome >= 0 ? "+" : "";
                sb.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(p.Color)}><b>Player {p.Id}</b></color>");
                sb.AppendLine($"Gold: {(int)p.Gold} ({sign}{p.GoldIncome:F1})");
                sb.AppendLine($"Units: {p.ActiveUnits.Count}");
                sb.AppendLine($"Cells: {p.OwnedCellCount}");
                sb.AppendLine("------------------");
            }
            goldText.text = sb.ToString();
        }

        private void HandleUnitSelectionChanged(IUnit unit)
        {
            if (unit == null) HideUnitInfo();
            else ShowUnitInfo(unit as UnitController);
        }

        private void ResetVisualTimer() => _tickTimer = 0f;

        private void Update()
        {
            if (_localPlayer == null || _localSpawner == null)
            {
                TrySetupLocalReferences();
                return;
            }

            if (_selectedUnit != null)
            {
                if (_selectedUnit.IsDead) HideUnitInfo();
                else
                {
                    unitHpText.text = $"HP: {Mathf.CeilToInt(_selectedUnit.GetCurrentHP())} / {_selectedUnit.Data.maxHp}";
                    unitStaminaText.text = $"Stamina: {_selectedUnit.GetCurrentStamina():F1} / {_selectedUnit.Data.maxStamina}";
                }
            }

            _tickTimer += Time.deltaTime;
            float currentDuration = TurnManager.Instance != null ? TurnManager.Instance.TickDuration : 1f;
            _tickTimer = Mathf.Min(_tickTimer, currentDuration);

            if (TurnManager.Instance != null)
                turnText.text = $"Turn: {TurnManager.Instance.TurnCount}";

            var queue = _localSpawner.GetQueue();
            SyncQueueIcons(queue);
            HandleSmoothFill(queue, currentDuration);
            if (clearQueueBtn != null) clearQueueBtn.gameObject.SetActive(queue.Count >= 2);
        }

        private void TrySetupLocalReferences()
        {
            if (_localPlayer == null)
                _localPlayer = GameController.Instance?.GetLocalPlayer();

            if (_localPlayer != null && _localSpawner == null)
            {
                var spawners = Object.FindObjectsByType<UnitSpawner>(FindObjectsSortMode.None);
                foreach (var s in spawners)
                    if (s.OwnerId == _localPlayer.Id) { _localSpawner = s; break; }
            }
        }

        private void RequestSpawn(int unitSlot)
        {
            if (_localPlayer == null || _localSpawner == null) return;
            if (_localSpawner.GetQueue().Count >= UnitSpawner.MaxQueueSize) return;
            UnitSpawner.OnRequestUnitSpawn?.Invoke(_localPlayer.Id, unitSlot, _localPlayer.SelectedCell);
        }

        private void HandleClearQueue()
        {
            if (_localSpawner == null) return;
            var queue = _localSpawner.GetQueue();
            while (queue.Count > 1) _localSpawner.RemoveUnitFromQueue(1);
        }

        private void SyncQueueIcons(IReadOnlyList<QueuedUnit> queue)
        {
            int displayCount = Mathf.Min(queue.Count, 6);

            while (_iconRefs.Count > displayCount)
            {
                Destroy(_iconRefs[_iconRefs.Count - 1].root);
                _iconRefs.RemoveAt(_iconRefs.Count - 1);
            }

            while (_iconRefs.Count < displayCount)
            {
                GameObject newIcon = Instantiate(queueIconPrefab, queueContainer);
                var refs = new QueueIconRefs
                {
                    root = newIcon,
                    iconImage = newIcon.GetComponent<Image>(),
                    fillImage = newIcon.transform.GetChild(0).GetComponent<Image>(),
                    tickText = newIcon.transform.GetChild(1).GetComponent<TextMeshProUGUI>(),
                    nameText = newIcon.transform.GetChild(2).GetComponent<TextMeshProUGUI>(),
                    iconButton = newIcon.GetComponent<Button>()
                };
                _iconRefs.Add(refs);
            }

            for (int i = 0; i < _iconRefs.Count; i++)
            {
                var refs = _iconRefs[i];
                refs.iconButton.onClick.RemoveAllListeners();
                int idx = i;
                refs.iconButton.onClick.AddListener(() => _localSpawner.RemoveUnitFromQueue(idx));
                var data = queue[i].Data;
                if (refs.iconImage != null) refs.iconImage.sprite = data.icon;
                if (refs.tickText != null) refs.tickText.text = Mathf.Max(0, queue[i].RemainingTicks).ToString();
                if (refs.nameText != null) refs.nameText.text = data.unitName;
                refs.iconButton.interactable = (i > 0);
            }
        }

        private void HandleSmoothFill(IReadOnlyList<QueuedUnit> queue, float duration)
        {
            if (queue.Count == 0 || _iconRefs.Count == 0 || _iconRefs[0].fillImage == null) return;
            float totalTicks = queue[0].Data.recruitmentTime;
            float remainingTicks = queue[0].RemainingTicks + 1;
            float baseFill = (totalTicks - remainingTicks) / totalTicks;
            float tickProgress = _tickTimer / duration;
            _iconRefs[0].fillImage.fillAmount = 1f - Mathf.Clamp01(baseFill + (tickProgress / totalTicks));
            for (int i = 1; i < _iconRefs.Count; i++)
                if (_iconRefs[i].fillImage != null) _iconRefs[i].fillImage.fillAmount = 0f;
        }

        public void ShowUnitInfo(UnitController unit)
        {
            if (unit == null) return;
            _selectedUnit = unit;
            if (infoPanelRoot != null) infoPanelRoot.SetActive(true);
            unitNameText.text = unit.Data.unitName;
            unitOwnerText.text = $"Player {unit.OwnerId}";
            unitDamageText.text = $"Damage: {unit.Data.baseDamage} (+{unit.Data.bonusDamage} vs {unit.Data.strongAgainst})";
        }

        public void HideUnitInfo()
        {
            _selectedUnit = null;
            if (infoPanelRoot != null) infoPanelRoot.SetActive(false);
        }

        public void SwitchSelection()
        {
            _isFieldSelected = !_isFieldSelected;
            if (selectorBtn != null)
                selectorBtn.image.sprite = _isFieldSelected ? selectorFieldImage : selectorUnitImage;

            InputManager.Instance?.SetSelectionType(_isFieldSelected);
        }
        public void DeleteSelectedUnit()
        {
            if (_selectedUnit == null) return;
            if (_localPlayer == null) return;
            if (_selectedUnit.OwnerId != _localPlayer.Id) return;
            _selectedUnit.ExecuteDeath();
        }
    }
}