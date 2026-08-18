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
        [SerializeField] private TextMeshProUGUI goldPerTurnText;
        [SerializeField] private TextMeshProUGUI turnText;

        [Header("Spawn Buttons")]
        [SerializeField] private Transform actionPanelContainer;
        [SerializeField] private UnitSpawnButtonTrigger spawnButtonPrefab;
        [SerializeField] private UnitRoster unitRoster;

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

        [Header("Settings Panel")]
        [SerializeField] private GameObject settingsDropdown;
        [SerializeField] private RectTransform settingsDropdownRect;
        [SerializeField] private CanvasGroup settingsCanvasGroup;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider effectsSlider;

        [SerializeField] private float dropdownHeight = 200f;
        [SerializeField] private float animationSpeed = 8f;

        private bool _settingsOpen = false;
        private float _targetHeight = 0f;

        [Header("Unit Description Panel (Slide)")]
        [SerializeField] private GameObject descPanelRoot;
        [SerializeField] private RectTransform descPanelRect;
        [SerializeField] private CanvasGroup descCanvasGroup;
        [SerializeField] private float descPanelTargetHeight = 450f;
        [SerializeField] private UnitDescriptionPanelUI descPanelUI;
        [SerializeField] private Button closeDescBtn;

        private bool _descOpen = false;
        private float _targetDescHeight = 0f;
        private int _currentDisplayedUnitIndex = -1;

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
            public Outline borderOutline;
        }

        private void Start()
        {
            UnitSpawnButtonTrigger.OnSpawnRequested += RequestSpawn;
            UnitSpawnButtonTrigger.OnUnitDescriptionRequested += ShowUnitDescription; 
            UnitDescriptionPanelUI.OnUpgradeRequested += RequestUpgrade;

            BuildActionPanel();

            if (closeDescBtn != null)
                closeDescBtn.onClick.AddListener(HideUnitDescription);

            if (descPanelRect != null)                
                descPanelRect.sizeDelta = new Vector2(descPanelRect.sizeDelta.x, 0f);

            if (descCanvasGroup != null)
            {
                descCanvasGroup.alpha = 0f;
                descCanvasGroup.interactable = false;
                descCanvasGroup.blocksRaycasts = false;
            }
            if (clearQueueBtn != null) clearQueueBtn.onClick.AddListener(HandleClearQueue);

            if (settingsDropdownRect != null)
                settingsDropdownRect.sizeDelta = new Vector2(settingsDropdownRect.sizeDelta.x, 0f);

            if (settingsCanvasGroup != null)
            {
                settingsCanvasGroup.alpha = 0f;
                settingsCanvasGroup.interactable = false;
                settingsCanvasGroup.blocksRaycasts = false;
            }
            InitSlider(masterSlider, AudioManager.MasterKey, v => AudioManager.Instance?.SetMasterVolume(v));
            InitSlider(musicSlider, AudioManager.MusicKey, v => AudioManager.Instance?.SetMusicVolume(v));
            InitSlider(effectsSlider, AudioManager.EffectsKey, v => AudioManager.Instance?.SetEffectsVolume(v));
        }

        private void BuildActionPanel()
        {
            if (actionPanelContainer == null || spawnButtonPrefab == null || unitRoster == null) return;

            foreach (Transform child in actionPanelContainer) Destroy(child.gameObject);

            var units = unitRoster.Units;
            for (int slot = 0; slot < units.Count; slot++)
            {
                var data = units[slot];
                var trigger = Instantiate(spawnButtonPrefab, actionPanelContainer);
                trigger.SetSlot(slot);

                var icon = trigger.GetComponentInChildren<Image>();
                if (icon != null) icon.sprite = data.icon;
            }
        }

        private void OnDestroy()
        {
            UnitSpawnButtonTrigger.OnSpawnRequested -= RequestSpawn;
            UnitSpawnButtonTrigger.OnUnitDescriptionRequested -= ShowUnitDescription;
            UnitDescriptionPanelUI.OnUpgradeRequested -= RequestUpgrade;

        }
        private void OnEnable()
        {
            TurnManager.OnTurnCompleted += ResetVisualTimer;
            TurnManager.OnTurnCompleted += RefreshGoldDisplay;
            GameController.OnUnitSelected += HandleUnitSelectionChanged;
            UnitSpawner.OnUpgradeStateChanged += RefreshGoldDisplay;
        }

        private void OnDisable()
        {
            TurnManager.OnTurnCompleted -= ResetVisualTimer;
            TurnManager.OnTurnCompleted -= RefreshGoldDisplay;
            GameController.OnUnitSelected -= HandleUnitSelectionChanged;
            UnitSpawner.OnUpgradeStateChanged -= RefreshGoldDisplay;
        }

        private void InitSlider(Slider slider, string key, UnityEngine.Events.UnityAction<float> onValueChanged)
        {
            if (slider == null || AudioManager.Instance == null) return;

            slider.SetValueWithoutNotify(AudioManager.Instance.GetVolume(key));
            slider.onValueChanged.AddListener(onValueChanged);
        }

        private void RefreshGoldDisplay()
        {
            if (this == null || goldText == null || _localSpawner == null) return;
            goldText.text = _localPlayer.Gold.ToString();

            if (goldPerTurnText != null)
                goldPerTurnText.text = $"+{_localPlayer.GoldIncome:F1}";

            if (_descOpen && _currentDisplayedUnitIndex >= 0)
                ShowUnitDescription(_currentDisplayedUnitIndex);
        }

        private void RequestUpgrade(int unitIndex, StatType statType)
        {
            _localSpawner?.RequestUpgrade(unitIndex, statType);
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
                    unitHpText.text = $"HP: {Mathf.CeilToInt(_selectedUnit.GetCurrentHP())} / {_selectedUnit.Stats.MaxHp}";
                    unitStaminaText.text = $"Stamina: {_selectedUnit.GetCurrentStamina():F1} / {_selectedUnit.Stats.MaxStamina}";
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

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                ToggleSettings();

            if (settingsDropdownRect != null)
            {
                float currentHeight = settingsDropdownRect.sizeDelta.y;
                float nextHeight = Mathf.Lerp(currentHeight, _targetHeight, Time.deltaTime * animationSpeed);
                settingsDropdownRect.sizeDelta = new Vector2(settingsDropdownRect.sizeDelta.x, nextHeight);

                float progress = Mathf.Clamp01(nextHeight / dropdownHeight);

                if (settingsCanvasGroup != null)
                {
                    settingsCanvasGroup.alpha = progress;
                    bool isVisible = progress > 0.1f;
                    settingsCanvasGroup.interactable = isVisible;
                    settingsCanvasGroup.blocksRaycasts = isVisible;
                }
                settingsDropdown.SetActive(progress > 0.01f);
            }

            if (descPanelRect != null)
            {
                float currentHeight = descPanelRect.sizeDelta.y;
                float nextHeight = Mathf.Lerp(currentHeight, _targetDescHeight, Time.deltaTime * animationSpeed);
                descPanelRect.sizeDelta = new Vector2(descPanelRect.sizeDelta.x, nextHeight);

                float progress = Mathf.Clamp01(nextHeight / descPanelTargetHeight);

                if (descCanvasGroup != null)
                {
                    descCanvasGroup.alpha = progress;
                    bool isVisible = progress > 0.1f;
                    descCanvasGroup.interactable = isVisible;
                    descCanvasGroup.blocksRaycasts = isVisible;
                }
                if (descPanelRoot != null) descPanelRoot.SetActive(progress > 0.01f);
            }
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
            _localSpawner.ClearQueue();
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
                    iconButton = newIcon.GetComponent<Button>(),
                    borderOutline = newIcon.GetComponent<Outline>(),
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
            if (queue.Count == 0 || _iconRefs.Count == 0) return;

            bool isReadyToSpawn = queue[0].IsWaitingToSpawn || queue[0].RemainingTicks <= 0;

            if (isReadyToSpawn)
            {
                if (_iconRefs[0].fillImage != null)
                    _iconRefs[0].fillImage.fillAmount = 0f;

                if (_iconRefs[0].borderOutline != null)
                    _iconRefs[0].borderOutline.enabled = true;
            }
            else
            {
                float totalTicks = queue[0].Data.recruitmentTime;
                float remainingTicks = queue[0].RemainingTicks;
                float baseFill = (totalTicks - remainingTicks) / totalTicks;
                float tickProgress = _tickTimer / duration;

                if (_iconRefs[0].fillImage != null)
                    _iconRefs[0].fillImage.fillAmount = 1f - Mathf.Clamp01(baseFill + (tickProgress / totalTicks));

                if (_iconRefs[0].borderOutline != null)
                    _iconRefs[0].borderOutline.enabled = false;
            }

            for (int i = 1; i < _iconRefs.Count; i++)
            {
                if (_iconRefs[i].fillImage != null)
                    _iconRefs[i].fillImage.fillAmount = 0f;

                if (_iconRefs[i].borderOutline != null)
                    _iconRefs[i].borderOutline.enabled = false;
            }
        }

        public void ShowUnitInfo(UnitController unit)
        {
            if (unit == null) return;
            _selectedUnit = unit;
            if (infoPanelRoot != null) infoPanelRoot.SetActive(true);
            unitNameText.text = unit.Data.unitName;
            unitOwnerText.text = $"Player {unit.OwnerId}";
            unitDamageText.text = $"Damage: {unit.Stats.BaseDamage} (+{unit.Stats.BonusDamage} vs {unit.Data.strongAgainst})";
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
            _selectedUnit.DestroyUnit();
        }

        public void ToggleSettings()
        {
            _settingsOpen = !_settingsOpen;
            _targetHeight = _settingsOpen ? dropdownHeight : 0f;
            if (_settingsOpen) settingsDropdown.SetActive(true);
        }

        public void ShowUnitDescription(int unitIndex)
        {
            var data = unitRoster?.GetBySlot(unitIndex);
            if (data == null) return;

            _currentDisplayedUnitIndex = unitIndex;

            if (descPanelUI != null)
                descPanelUI.RefreshPanel(data, GetUpgradesForUnit(data), _localPlayer != null ? (int)_localPlayer.Gold : 0);

            Canvas.ForceUpdateCanvases();
            if (descPanelRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(descPanelRect);

            _descOpen = true;
            _targetDescHeight = descPanelTargetHeight;
            if (descPanelRoot != null) descPanelRoot.SetActive(true);
        }

        private Dictionary<StatType, StatUpgradeState> GetUpgradesForUnit(UnitData data)
        {
            var dict = new Dictionary<StatType, StatUpgradeState>();
            if (data?.upgradeSettings == null) return dict;

            foreach (var settings in data.upgradeSettings)
            {
                if (!settings.enabled) continue;
                int level = _localPlayer != null ? _localPlayer.GetUpgradeLevel(data.index, (int)settings.statType) : 0;
                dict[settings.statType] = settings.ToState(level);
            }
            return dict;
        }

        public void HideUnitDescription()
        {
            _descOpen = false;
            _targetDescHeight = 0f;
            _currentDisplayedUnitIndex = -1;
        }

        private void CheckClickOutsideDescription()
        {
            Vector2 mousePos = UnityEngine.Input.mousePosition;
            bool isOverPanel = descPanelRect != null && RectTransformUtility.RectangleContainsScreenPoint(descPanelRect, mousePos);

            if (!isOverPanel)
            {
                HideUnitDescription();
            }
        }
    }
}