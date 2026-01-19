using GridEmpire.Core;
using GridEmpire.Gameplay;
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

        [Header("Queue UI References")]
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

        private PlayerProfile _localPlayer;
        private UnitSpawner _localSpawner;
        private UnitController _selectedUnit;
        private List<QueueIconRefs> _iconRefs = new List<QueueIconRefs>();
        private float _tickTimer = 0f;

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

            if (clearQueueBtn != null)
                clearQueueBtn.onClick.AddListener(HandleClearQueue);
        }

        private void OnEnable()
        {
            TurnManager.OnTurnCompleted += ResetVisualTimer;
            GameController.OnUnitSelected += HandleUnitSelectionChanged;
        }

        private void OnDisable()
        {
            TurnManager.OnTurnCompleted -= ResetVisualTimer;
            GameController.OnUnitSelected -= HandleUnitSelectionChanged;
        }

        private void HandleUnitSelectionChanged(IUnit unit)
        {
            if (unit == null)
            {
                HideUnitInfo();
            }
            else
            {
                ShowUnitInfo(unit as UnitController);
            }
        }
        private void ResetVisualTimer() => _tickTimer = 0f;

        private void Update()
        {
            if (_localPlayer == null || _localSpawner == null)
            {
                SetupLocalReferences();
                return;
            }

            // --- UNIT INFO PANEL FRISSÍTÉSE ---
            if (_selectedUnit != null)
            {
                if (_selectedUnit.IsDead)
                {
                    HideUnitInfo();
                }
                else
                {
                    unitHpText.text = $"HP: {Mathf.CeilToInt(_selectedUnit.GetCurrentHP())} / {_selectedUnit.Data.maxHp}";
                    unitStaminaText.text = $"Stamina: {_selectedUnit.GetCurrentStamina():F1} / {_selectedUnit.Data.maxStamina}";
                }
            }

            // --- IDÕZÍTÕ ÉS TURN INFO ---
            _tickTimer += Time.deltaTime;
            float currentDuration = (TurnManager.Instance != null) ? TurnManager.Instance.TickDuration : 1f;
            _tickTimer = Mathf.Min(_tickTimer, currentDuration);

            if (TurnManager.Instance != null)
                turnText.text = $"Turn: {TurnManager.Instance.TurnCount}";

            // --- DEBUG GAZDASÁGI PANEL (MINDEN JÁTÉKOS) ---
            System.Text.StringBuilder debugBuilder = new System.Text.StringBuilder();
            var allPlayers = GameController.Instance.GetPlayers();

            foreach (var p in allPlayers)
            {
                string incomeSign = p.GoldIncome >= 0 ? "+" : "";
                debugBuilder.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(p.Color)}><b>Player {p.Id}</b></color>");
                debugBuilder.AppendLine($"Gold: {(int)p.Gold} ({incomeSign}{p.GoldIncome:F1})");
                debugBuilder.AppendLine($"Units: {p.ActiveUnits.Count}");
                debugBuilder.AppendLine($"Cells: {p.OwnedCellCount}");
                debugBuilder.AppendLine("------------------");
            }

            goldText.text = debugBuilder.ToString();

            // --- SPAWN QUEUE KEZELÉSE ---
            var queue = _localSpawner.GetQueue();
            SyncQueueIcons(queue);
            HandleSmoothFill(queue, currentDuration);

            if (clearQueueBtn != null)
                clearQueueBtn.gameObject.SetActive(queue.Count >= 2);
        }

        private void SetupLocalReferences()
        {
            _localPlayer = GameController.Instance?.GetLocalPlayer();
            if (_localPlayer == null) return;

            var spawners = Object.FindObjectsByType<UnitSpawner>(FindObjectsSortMode.None);
            foreach (var s in spawners)
            {
                if (s.OwnerId == _localPlayer.Id)
                {
                    _localSpawner = s;
                    break;
                }
            }
        }

        private void RequestSpawn(int unitSlot)
        {
            if (_localPlayer != null)
                UnitSpawner.OnRequestUnitSpawn?.Invoke(_localPlayer.Id, unitSlot, _localPlayer.SelectedCell);
        }

        private void HandleClearQueue()
        {
            if (_localSpawner == null) return;
            var queue = _localSpawner.GetQueue();
            while (queue.Count > 1) _localSpawner.RemoveUnitFromQueue(1);
        }

        private void SyncQueueIcons(List<QueuedUnit> queue)
        {
            int displayCount = Mathf.Min(queue.Count, 6);

            // Törlés
            while (_iconRefs.Count > displayCount)
            {
                Destroy(_iconRefs[_iconRefs.Count - 1].root);
                _iconRefs.RemoveAt(_iconRefs.Count - 1);
            }

            // Létrehozás (Segédszkript alapú)
            while (_iconRefs.Count < displayCount)
            {
                int index = _iconRefs.Count;
                GameObject newIcon = Instantiate(queueIconPrefab, queueContainer);

                // Kikeressük a komponenseket az új logikád szerint:
                var refs = new QueueIconRefs
                {
                    root = newIcon,
                    // A Buttonon lévõ Image lesz az ikon (háttér)
                    iconImage = newIcon.GetComponent<Image>(),
                    // A legelsõ gyerek (Index 0) lesz a sötétítõ Fill réteg
                    fillImage = newIcon.transform.GetChild(0).GetComponent<Image>(),
                    tickText = newIcon.transform.GetChild(1).GetComponent<TextMeshProUGUI>(),
                    nameText = newIcon.transform.GetChild(2).GetComponent<TextMeshProUGUI>(),
                    iconButton = newIcon.GetComponent<Button>()
                };

                refs.iconButton.onClick.AddListener(() => _localSpawner.RemoveUnitFromQueue(index));
                _iconRefs.Add(refs);
            }

            // Frissítés
            for (int i = 0; i < _iconRefs.Count; i++)
            {
                var data = queue[i].data;
                if (_iconRefs[i].iconImage != null) _iconRefs[i].iconImage.sprite = data.icon;
                if (_iconRefs[i].tickText != null) _iconRefs[i].tickText.text = Mathf.Max(0, queue[i].remainingTicks).ToString();
                if (_iconRefs[i].nameText != null) _iconRefs[i].nameText.text = data.unitName;

                _iconRefs[i].iconButton.interactable = (i > 0);
            }
        }

        private void HandleSmoothFill(List<QueuedUnit> queue, float duration)
        {
            if (queue.Count == 0 || _iconRefs.Count == 0 || _iconRefs[0].fillImage == null) return;

            float totalTicks = (float)queue[0].data.recruitmentTime;
            float remainingTicks = (float)queue[0].remainingTicks + 1;
            float baseFill = (totalTicks - remainingTicks) / totalTicks;
            float currentTickProgress = _tickTimer / duration;

            float fillAmount = baseFill + (currentTickProgress * (1f / totalTicks));
            _iconRefs[0].fillImage.fillAmount = 1f - Mathf.Clamp01(fillAmount);

            for (int i = 1; i < _iconRefs.Count; i++)
                if (_iconRefs[i].fillImage != null) _iconRefs[i].fillImage.fillAmount = 0.0f;
        }

        public void ShowUnitInfo(UnitController unit)
        {
            _selectedUnit = unit;

            if (infoPanelRoot != null) infoPanelRoot.SetActive(true);

            unitNameText.text = unit.Data.unitName;
            unitOwnerText.text = $"Player {unit.OwnerId}";
            unitDamageText.text = $"Damage: {_selectedUnit.Data.baseDamage} (+{_selectedUnit.Data.bonusDamage} vs {_selectedUnit.Data.strongAgainst})";
        }

        public void HideUnitInfo()
        {
            _selectedUnit = null;
            if (infoPanelRoot != null) infoPanelRoot.SetActive(false);
        }

    }
}