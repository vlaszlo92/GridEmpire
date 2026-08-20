using GridEmpire.Core;
using GridEmpire.Networking;
using GridEmpire.Shared;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GridEmpire.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject modeSelectorPanel;
        [SerializeField] private GameObject hostSettingsPanel;
        [SerializeField] private GameObject clientWaitingPanel;
        [SerializeField] private GameObject fogPanel;

        [Header("Mode Buttons")]
        [SerializeField] private Button goToHostBtn;
        [SerializeField] private Button goToClientBtn;
        [SerializeField] private Button reconnectBtn;
        [SerializeField] private Button throwSessionBtn;
        [SerializeField] private TextMeshProUGUI reconnectText;

        [Header("Host Action Buttons")]
        [SerializeField] private Button startHostFinalBtn;
        [SerializeField] private Button backToLobbyHostBtn;

        [Header("Client Action Buttons")]
        [SerializeField] private Button startClientConnectBtn;
        [SerializeField] private Button backToLobbyClientBtn;

        [Header("Settings")]
        [SerializeField] private string gameSceneName = "GameScene";

        [Header("General Settings UI")]
        public Slider totalPlayersSlider;
        public TMP_InputField totalPlayersInput;
        public Slider aiBotsSlider;
        public TMP_InputField aiBotsInput;
        public Slider turnSpeedSlider;
        public TMP_InputField turnSpeedInput;
        public Slider mapSizeSlider;
        public TMP_InputField mapSizeInput;
        public Toggle fogOfWarToggle;
        public Slider goldPerTurnPerCellSlider;
        public TMP_InputField goldPerTurnPerCellInput;

        [Header("Host Network UI")]
        [SerializeField] private TMP_InputField hostCodeDisplay;
        [SerializeField] private Button copyCodeBtn;
        [SerializeField] private TextMeshProUGUI hostPlayerCountText;
        [SerializeField] private TextMeshProUGUI hostLoadingText;
        [SerializeField] private Transform hostPlayerListContainer;
        [SerializeField] private TextMeshProUGUI hostPlayerListPrefab;

        [Header("Client Network UI")]
        [SerializeField] private TMP_InputField clientCodeInput;
        [SerializeField] private TextMeshProUGUI clientStatusText;
        [SerializeField] private TextMeshProUGUI clientPlayerCountText;
        [SerializeField] private TextMeshProUGUI clientTotalPlayersText;
        [SerializeField] private TextMeshProUGUI clientAiBotsText;
        [SerializeField] private TextMeshProUGUI clientMapSizeText;
        [SerializeField] private TextMeshProUGUI clientTurnSpeedText;
        [SerializeField] private Transform clientPlayerListContainer;
        [SerializeField] private TextMeshProUGUI clientPlayerListPrefab;
        [SerializeField] private TextMeshProUGUI clientGoldPerCellText;

        [Header("Player Identity UI - Host")]
        [SerializeField] private TMP_InputField hostPlayerNameInput;
        [SerializeField] private Transform hostColorSwatchContainer;
        [SerializeField] private Image hostSelectedColorPreview;
        [SerializeField] private TextMeshProUGUI hostColorPickStatusText;

        [Header("Player Identity UI - Client")]
        [SerializeField] private TMP_InputField clientPlayerNameInput;
        [SerializeField] private Transform clientColorSwatchContainer;
        [SerializeField] private Image clientSelectedColorPreview;
        [SerializeField] private TextMeshProUGUI clientColorPickStatusText;

        [SerializeField] private Button colorSwatchButtonPrefab;

        private readonly List<Button> _hostColorSwatchButtons = new List<Button>();
        private readonly List<Button> _clientColorSwatchButtons = new List<Button>();
        private string _pendingPlayerName = null;
        private int _pendingColorIndex = -1;
        private bool _identityFlushQueued = false;

        [System.Serializable]
        private class UnitStatsSection
        {
            public UnitData data;
            public Transform hostContainer;
            public Transform clientContainer;
        }

        [Header("Unit Stats UI (Dynamic)")]
        [SerializeField] private GameObject statRowPrefab;
        [SerializeField] private List<UnitStatsSection> unitStatsSections;

        private IEnumerable<UnitData> AllUnitData =>
            unitStatsSections.Where(s => s.data != null).Select(s => s.data);

        private System.Action _onUnitStatsSyncedHandler;
        private System.Action<List<(int, string)>> _onUnitStatsFieldsChangedHandler;

        // Player list cache
        private readonly List<TextMeshProUGUI> _hostPlayerListItems = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _clientPlayerListItems = new List<TextMeshProUGUI>();

        private readonly Dictionary<(int unitIndex, string fieldName), StatRowConnector> _clientRowLookup = new();

        private void Start()
        {
            RefreshReconnectUI();

            GameSettings savedSettings = GameSettingsStorage.Load();
            SetupGeneralUI(savedSettings);

            var savedUnitStats = UnitStatsStorage.Load();
            if (savedUnitStats != null) UnitStatsSnapshotUtil.Apply(AllUnitData, savedUnitStats);

            BuildUnitStatsUI(interactable: true);

            _onUnitStatsSyncedHandler = () => BuildUnitStatsUI(interactable: false);
            _onUnitStatsFieldsChangedHandler = FlashChangedRows;
            GlobalNetworkSettings.OnUnitStatsFieldsChanged += _onUnitStatsFieldsChangedHandler;
            GlobalNetworkSettings.OnUnitStatsSynced += _onUnitStatsSyncedHandler;

            SetHostLoading(false);
            if (startHostFinalBtn != null) startHostFinalBtn.interactable = false;

            SubscribeToNetworkEvents();

            goToHostBtn.onClick.AddListener(async () =>
            {
                if (NetworkLobbyController.Instance == null || NetworkLobbyController.Instance.IsSessionOperationInProgress) return;
                SetAllNavButtonsInteractable(false);

                ShowPanel(hostSettingsPanel);
                SetHostLoading(true);
                if (startHostFinalBtn != null) startHostFinalBtn.interactable = false;
                if (hostCodeDisplay != null) hostCodeDisplay.text = "...";
                await NetworkLobbyController.Instance.CreateHostSession((int)totalPlayersSlider.maxValue);

                SetAllNavButtonsInteractable(true);
            });

            goToClientBtn.onClick.AddListener(() =>
            {
                if (NetworkLobbyController.Instance != null && NetworkLobbyController.Instance.IsSessionOperationInProgress) return;
                ShowPanel(clientWaitingPanel);
            });

            startHostFinalBtn.onClick.AddListener(StartHostGame);

            if (startClientConnectBtn != null)
                startClientConnectBtn.onClick.AddListener(StartClientConnect);

            if (copyCodeBtn != null)
                copyCodeBtn.onClick.AddListener(() =>
                {
                    if (hostCodeDisplay != null && !string.IsNullOrEmpty(hostCodeDisplay.text))
                        GUIUtility.systemCopyBuffer = hostCodeDisplay.text;
                });

            if (backToLobbyHostBtn != null)
                backToLobbyHostBtn.onClick.AddListener(OnHostBackToLobby);

            if (backToLobbyClientBtn != null)
                backToLobbyClientBtn.onClick.AddListener(OnClientBackToLobby);
            if (reconnectBtn != null) reconnectBtn.onClick.AddListener(OnReconnectClicked);
            if (throwSessionBtn != null) throwSessionBtn.onClick.AddListener(OnThrowSessionClicked);

            BuildColorSwatches(hostColorSwatchContainer, _hostColorSwatchButtons);
            BuildColorSwatches(clientColorSwatchContainer, _clientColorSwatchButtons);

            if (hostPlayerNameInput != null)
                hostPlayerNameInput.onEndEdit.AddListener(RequestNameChange);
            if (clientPlayerNameInput != null)
                clientPlayerNameInput.onEndEdit.AddListener(RequestNameChange);

            GlobalNetworkSettings.OnColorRejected += HandleColorRejected;
            GlobalNetworkSettings.OnPlayerLobbyInfosChanged += HandleLobbyInfoChanged;

            totalPlayersSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());
            aiBotsSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());
            mapSizeSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());
            turnSpeedSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());
            goldPerTurnPerCellSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());

            StartCoroutine(WatchNetworkSettings());
        }

        private void OnDestroy()
        {
            if (_onUnitStatsSyncedHandler != null)
                GlobalNetworkSettings.OnUnitStatsSynced -= _onUnitStatsSyncedHandler;
            if (_onUnitStatsFieldsChangedHandler != null)
                GlobalNetworkSettings.OnUnitStatsFieldsChanged -= _onUnitStatsFieldsChangedHandler;

            GlobalNetworkSettings.OnColorRejected -= HandleColorRejected;
            GlobalNetworkSettings.OnPlayerLobbyInfosChanged -= HandleLobbyInfoChanged;

            var controller = NetworkLobbyController.Instance;
            if (controller == null) return;

            controller.OnHostSessionReady -= HandleHostSessionReady;
            controller.OnHostSessionFailed -= HandleHostSessionFailed;
            controller.OnSessionPlayersChanged -= HandleSessionPlayersChanged;
            controller.OnClientConnectResult -= HandleClientConnectResult;
            controller.OnHostConnectionLost -= OnClientBackToLobby;
        }

        // --- NETWORK EVENT SUBSCRIPTION ------------------------------------------------

        private void SubscribeToNetworkEvents()
        {
            var controller = NetworkLobbyController.Instance;
            if (controller == null) return;

            controller.OnHostSessionReady += HandleHostSessionReady;
            controller.OnHostSessionFailed += HandleHostSessionFailed;
            controller.OnSessionPlayersChanged += HandleSessionPlayersChanged;
            controller.OnClientConnectResult += HandleClientConnectResult;
            controller.OnHostConnectionLost += OnClientBackToLobby;
        }

        private void HandleHostSessionReady(string code)
        {
            if (hostCodeDisplay != null) hostCodeDisplay.text = code;
            SetHostLoading(false);
            UpdateHostPlayerList();
            UpdateStartButtonState();

            GlobalNetworkSettings.Instance?.SyncUnitStatsToClients(AllUnitData);
        }

        private void HandleHostSessionFailed(string error)
        {
            if (hostCodeDisplay != null) hostCodeDisplay.text = "ERROR";
            SetHostLoading(false);
        }

        private void HandleSessionPlayersChanged()
        {
            UpdateHostPlayerList();
            UpdateStartButtonState();
            UpdateAiBotSliderMax();
            UpdateTotalPlayersSliderMin();
        }

        private void HandleClientConnectResult(string message, bool success)
        {
            SetClientStatus(message, success ? Color.green : Color.red);
            if (startClientConnectBtn != null) startClientConnectBtn.interactable = !success;

            if (!success)
            {
                NetworkLobbyController.ClearSavedSession();
                RefreshReconnectUI();
            }
        }

        private void SetHostLoading(bool loading)
        {
            if (hostLoadingText != null)
                hostLoadingText.gameObject.SetActive(loading);
            if (hostCodeDisplay != null)
                hostCodeDisplay.gameObject.SetActive(!loading);
            if (copyCodeBtn != null)
                copyCodeBtn.gameObject.SetActive(!loading);
        }

        // --- SETTINGS CHANGE HANDLING -------------------------------------------------------

        private void OnSettingsChanged()
        {
            NetworkLobbyController.Instance?.SyncSettingsToClients(
                (int)totalPlayersSlider.value,
                (int)aiBotsSlider.value,
                (int)mapSizeSlider.value,
                turnSpeedSlider.value,
                goldPerTurnPerCellSlider != null ? goldPerTurnPerCellSlider.value : 0.1f
            );
            UpdateStartButtonState();
            UpdateHostPlayerList();
            UpdateAiBotSliderMax();
            UpdateTotalPlayersSliderMin();
        }

        private void UpdateStartButtonState()
        {
            if (startHostFinalBtn == null) return;
            var controller = NetworkLobbyController.Instance;

            if (controller == null || !controller.IsHostSessionActive)
            {
                startHostFinalBtn.interactable = false;
                if (hostPlayerCountText != null)
                    hostPlayerCountText.text = "Initializing lobby...";
                return;
            }

            if (!controller.IsHost)
            {
                startHostFinalBtn.interactable = false;
                return;
            }

            int connected = controller.ConnectedClientsCount;
            int totalPlayers = (int)totalPlayersSlider.value;
            int aiBots = (int)aiBotsSlider.value;
            int humanPlayers = totalPlayers - aiBots;

            if (hostPlayerCountText != null)
                hostPlayerCountText.text = $"PLAYER LIST - {connected} / {humanPlayers}";

            startHostFinalBtn.interactable = connected >= humanPlayers;
        }

        // --- JATEKOS LISTA -------------------------------------------------------------

        private void UpdateTotalPlayersSliderMin()
        {
            var controller = NetworkLobbyController.Instance;
            int connected = (controller != null && controller.IsHostSessionActive) ? controller.SessionPlayersCount : 1;
            connected = Mathf.Max(1, connected);

            totalPlayersSlider.minValue = connected;

            if (totalPlayersSlider.value < connected)
            {
                totalPlayersSlider.value = connected;
                totalPlayersInput.text = connected.ToString();
            }
        }

        private void UpdateHostPlayerList()
        {
            if (hostPlayerListContainer == null || hostPlayerListPrefab == null) return;

            int totalPlayers = (int)totalPlayersSlider.value;
            int aiBots = (int)aiBotsSlider.value;
            int humanPlayers = totalPlayers - aiBots;

            RebuildPlayerList(hostPlayerListContainer, _hostPlayerListItems, hostPlayerListPrefab, humanPlayers, aiBots);
        }

        private void UpdateClientPlayerList()
        {
            if (clientPlayerListContainer == null || clientPlayerListPrefab == null) return;

            var gns = GlobalNetworkSettings.Instance;
            if (gns == null) return;

            int totalPlayers = gns.TotalPlayers.Value;
            int aiBots = gns.TotalAIBots.Value;
            int humanPlayers = totalPlayers - aiBots;

            RebuildPlayerList(clientPlayerListContainer, _clientPlayerListItems, clientPlayerListPrefab, humanPlayers, aiBots);
        }

        private void UpdateAiBotSliderMax()
        {
            int totalPlayers = GlobalNetworkSettings.Instance.TotalPlayers.Value;
            totalPlayersSlider.value = totalPlayers;
            var controller = NetworkLobbyController.Instance;
            int connectedHumans = (controller != null && controller.IsListening) ? controller.ConnectedClientsCount : 1;

            int maxBots = Mathf.Max(0, totalPlayers - connectedHumans);

            aiBotsSlider.maxValue = maxBots;

            if (aiBotsSlider.value > maxBots)
            {
                aiBotsSlider.value = maxBots;
                aiBotsInput.text = maxBots.ToString();
            }
        }

        private void RebuildPlayerList(Transform container, List<TextMeshProUGUI> items, TextMeshProUGUI prefab, int humanPlayers, int aiBots)
        {
            Transform targetParent = container;
            ScrollRect scrollRect = container.GetComponent<ScrollRect>();
            if (scrollRect != null && scrollRect.content != null)
                targetParent = scrollRect.content;

            int totalSlots = humanPlayers + aiBots;

            while (items.Count < totalSlots)
                items.Add(Instantiate(prefab, targetParent));

            for (int i = 0; i < items.Count; i++)
                items[i].gameObject.SetActive(i < totalSlots);

            var gns = GlobalNetworkSettings.Instance;
            int myPlayerId = (gns != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                ? gns.GetPlayerIdForClient(NetworkManager.Singleton.LocalClientId)
                : -1;

            var sortedInfos = new List<PlayerLobbyInfo>();
            if (gns != null)
            {
                foreach (var info in gns.PlayerLobbyInfos)
                    sortedInfos.Add(info);
                sortedInfos.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
            }

            for (int i = 0; i < humanPlayers; i++)
            {
                if (i < sortedInfos.Count)
                {
                    var info = sortedInfos[i];
                    string name = info.Name.IsEmpty ? $"Human {i + 1}" : info.Name.ToString();
                    bool isMe = info.PlayerId == myPlayerId;
                    string suffix = isMe ? " (Te)" : (i == 0 ? " (Host)" : "");

                    items[i].text = $"● {name}{suffix}";
                    items[i].color = info.ColorIndex >= 0 && info.ColorIndex < PredefinedColors.Colors.Length
                        ? (Color)PredefinedColors.Colors[info.ColorIndex]
                        : Color.white;
                }
                else
                {
                    items[i].text = $"○ Human {i + 1} (var...)";
                    items[i].color = Color.gray;
                }
            }

            for (int i = 0; i < aiBots; i++)
            {
                int slotIdx = humanPlayers + i;
                items[slotIdx].text = $"● AI Bot {i + 1}";
                items[slotIdx].color = new Color(0.6f, 0.8f, 1f);
            }
        }

        private bool TryGetLobbyInfo(GlobalNetworkSettings gns, int playerId, out PlayerLobbyInfo info)
        {
            foreach (var i in gns.PlayerLobbyInfos)
            {
                if (i.PlayerId == playerId) { info = i; return true; }
            }
            info = default;
            return false;
        }

        // --- BACK TO LOBBY ------------------------------------------------------------

        private async void OnHostBackToLobby()
        {
            if (NetworkLobbyController.Instance == null || NetworkLobbyController.Instance.IsSessionOperationInProgress) return;
            SetAllNavButtonsInteractable(false);

            await NetworkLobbyController.Instance.LeaveHostSession();

            if (startHostFinalBtn != null) startHostFinalBtn.interactable = false;
            if (hostCodeDisplay != null) hostCodeDisplay.text = "";
            if (hostPlayerCountText != null) hostPlayerCountText.text = "";

            ClearPlayerList(_hostPlayerListItems);
            ShowPanel(modeSelectorPanel);
            SetAllNavButtonsInteractable(true);
        }

        private async void OnClientBackToLobby()
        {
            if (NetworkLobbyController.Instance == null || NetworkLobbyController.Instance.IsSessionOperationInProgress) return;
            SetAllNavButtonsInteractable(false);

            await NetworkLobbyController.Instance.LeaveClientSession();

            SetClientStatus("", Color.white);
            ClearPlayerList(_clientPlayerListItems);
            ShowPanel(modeSelectorPanel);
            SetAllNavButtonsInteractable(true);
        }

        private void ClearPlayerList(List<TextMeshProUGUI> items)
        {
            foreach (var item in items)
                if (item != null) Destroy(item.gameObject);
            items.Clear();
        }

        // --- HOST JATEK INDITASA ------------------------------------------------------

        private void StartHostGame()
        {
            Debug.Log("[MMC] StartHostGame elindult.");
            GameSettings settings = new GameSettings
            {
                totalPlayers = GlobalNetworkSettings.Instance.TotalPlayers.Value, //MINDEN SLOT OPEN
                aiBots = (int)aiBotsSlider.value,
                mapRadius = (int)mapSizeSlider.value,
                turnSpeedMultiplier = turnSpeedSlider.value,
                fogOfWarEnabled = fogOfWarToggle != null ? fogOfWarToggle.isOn : true,
                goldPerTurnPerCell = goldPerTurnPerCellSlider != null ? goldPerTurnPerCellSlider.value : 0.1f
            };
            GameSettingsStorage.Save(settings);

            var unitStats = UnitStatsSnapshotUtil.Collect(AllUnitData);
            Debug.Log($"[MMC] UnitStats collect kesz, units={unitStats.units.Count}");

            UnitStatsStorage.Save(unitStats);
            Debug.Log("[MMC] UnitStatsStorage.Save kesz.");

            Debug.Log($"[MMC] GNS.Instance null? {GlobalNetworkSettings.Instance == null}");
            GlobalNetworkSettings.Instance?.SyncUnitStatsToClients(AllUnitData);

            startHostFinalBtn.interactable = false;
            if (backToLobbyHostBtn != null) backToLobbyHostBtn.interactable = false;

            int expectedHumans = (int)totalPlayersSlider.value - (int)aiBotsSlider.value;
            NetworkLobbyController.Instance?.StartHostGame(settings, gameSceneName, expectedHumans);
        }

        // --- CLIENT -------------------------------------------------------------------

        private async void StartClientConnect()
        {
            if (NetworkLobbyController.Instance == null || NetworkLobbyController.Instance.IsSessionOperationInProgress) return;
            if (clientCodeInput == null || string.IsNullOrEmpty(clientCodeInput.text))
            {
                SetClientStatus("Please enter the join code!", Color.red);
                return;
            }

            string joinCode = clientCodeInput.text.Trim().ToUpper();
            SetClientStatus("Connecting...", Color.yellow);
            SetAllNavButtonsInteractable(false);

            await NetworkLobbyController.Instance.JoinSession(joinCode);

            SetAllNavButtonsInteractable(true);
            if (startClientConnectBtn != null)
                startClientConnectBtn.interactable = !NetworkLobbyController.Instance.IsClient;
        }

        private void SetClientStatus(string msg, Color color)
        {
            if (clientStatusText == null) return;
            clientStatusText.text = msg;
            clientStatusText.color = color;
        }

        // --- WATCH NETWORK SETTINGS ---------------------------------------------------

        private IEnumerator WatchNetworkSettings()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.5f);

                var controller = NetworkLobbyController.Instance;
                if (controller == null) continue;

                if (controller.IsHost)
                {
                    controller.UpdateHostConnectedPlayerCount();
                    UpdateStartButtonState();
                    UpdateAiBotSliderMax();
                    UpdateTotalPlayersSliderMin();
                    UpdateHostPlayerList();
                }

                if (controller.IsClient && !controller.IsHost && GlobalNetworkSettings.Instance != null)
                {
                    UpdateClientLobbyInfo();
                    UpdateClientPlayerList();
                }
            }
        }

        private void UpdateClientLobbyInfo()
        {
            var gns = GlobalNetworkSettings.Instance;
            if (gns == null) return;

            int connected = gns.ConnectedPlayerCount.Value;
            int totalPlayers = gns.TotalPlayers.Value;
            int aiBots = gns.TotalAIBots.Value;
            int humanPlayers = totalPlayers - aiBots;

            if (clientPlayerCountText != null)
                clientPlayerCountText.text = $"{connected} / {humanPlayers} players";
            if (clientTotalPlayersText != null)
                clientTotalPlayersText.text = $"Players: {totalPlayers}";
            if (clientAiBotsText != null)
                clientAiBotsText.text = $"AI: {aiBots}";
            if (clientMapSizeText != null)
                clientMapSizeText.text = $"Map Size: {gns.NetworkMapRadius.Value}";
            if (clientTurnSpeedText != null)
                clientTurnSpeedText.text = $"Turn Speed: {gns.TurnSpeed.Value:F1}";
            if (clientGoldPerCellText != null)
                clientGoldPerCellText.text = $"Gold/Cell: {gns.GoldPerTurnPerCell.Value:F2}";
        }

        // --- SETTINGS UI -------------------------------------------------------------

        private void SetupGeneralUI(GameSettings settings)
        {
            BindElement(totalPlayersSlider, totalPlayersInput, settings.totalPlayers, 1, 6, true, (v) => { });
            BindElement(aiBotsSlider, aiBotsInput, settings.aiBots, 0, 6, true, (v) => { });
            BindElement(turnSpeedSlider, turnSpeedInput, settings.turnSpeedMultiplier, 0.2f, 5f, false, (v) => { });
            BindElement(mapSizeSlider, mapSizeInput, settings.mapRadius, 1, 25, true, (v) => { });
            BindElement(goldPerTurnPerCellSlider, goldPerTurnPerCellInput, settings.goldPerTurnPerCell, 0.1f, 2.0f, false, (v) => { });

            if (fogOfWarToggle != null)
            {
                fogOfWarToggle.isOn = settings.fogOfWarEnabled;
                fogOfWarToggle.onValueChanged.AddListener(value =>
                {
                    settings.fogOfWarEnabled = value;
                    NetworkLobbyController.Instance?.SetFogOfWar(value);
                });
            }
        }

        private void ShowPanel(GameObject panelToShow)
        {
            modeSelectorPanel.SetActive(panelToShow == modeSelectorPanel);
            fogPanel.SetActive(panelToShow == modeSelectorPanel);

            hostSettingsPanel.SetActive(panelToShow == hostSettingsPanel);
            clientWaitingPanel.SetActive(panelToShow == clientWaitingPanel);
        }

        // --- IDENTITY REQUEST QUEUE (ensures that the name/color selection always reaches its target) ---

        private bool IsIdentityReady()
        {
            return GlobalNetworkSettings.Instance != null
                && GlobalNetworkSettings.Instance.IsSpawned
                && NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening;
        }

        private void RequestNameChange(string name)
        {
            _pendingPlayerName = name;

            if (IsIdentityReady())
                GlobalNetworkSettings.Instance.RequestNameServerRpc(name);
            else
                EnsureIdentityFlushQueued();
        }

        private void RequestColorChange(int colorIndex)
        {
            _pendingColorIndex = colorIndex;

            if (IsIdentityReady())
            {
                Debug.Log($"[MMC] Color request sent immediately: {colorIndex}");
                GlobalNetworkSettings.Instance.RequestColorServerRpc(colorIndex);
            }
            else
            {
                Debug.Log($"[MMC] Color request queued, network not ready: {colorIndex}");
                EnsureIdentityFlushQueued();
            }
        }

        private void EnsureIdentityFlushQueued()
        {
            if (_identityFlushQueued) return;
            _identityFlushQueued = true;
            StartCoroutine(FlushPendingIdentityWhenReady());
        }

        private IEnumerator FlushPendingIdentityWhenReady()
        {
            yield return new WaitUntil(IsIdentityReady);

            if (_pendingPlayerName != null)
                GlobalNetworkSettings.Instance.RequestNameServerRpc(_pendingPlayerName);
            if (_pendingColorIndex != -1)
                GlobalNetworkSettings.Instance.RequestColorServerRpc(_pendingColorIndex);

            _identityFlushQueued = false;
        }

        // --- PLAYER IDENTITY (NAME + COLOR SWATCH) ------------------------------------

        private void BuildColorSwatches(Transform container, List<Button> buttonList)
        {
            if (container == null || colorSwatchButtonPrefab == null) return;

            for (int i = 0; i < PredefinedColors.Colors.Length; i++)
            {
                var btn = Instantiate(colorSwatchButtonPrefab, container);
                var img = btn.GetComponent<Image>();
                if (img != null) img.color = PredefinedColors.Colors[i];

                int colorIndex = i;
                btn.onClick.AddListener(() => RequestColorChange(colorIndex));
                buttonList.Add(btn);
            }
        }

        /// <summary>A PlayerLobbyInfos NetworkList can run on any change - updates both panels and the player lists.</summary>
        private void HandleLobbyInfoChanged()
        {
            RefreshColorSwatchInteractivity(_hostColorSwatchButtons);
            RefreshColorSwatchInteractivity(_clientColorSwatchButtons);
            UpdateSelectedColorPreview();
            UpdateHostPlayerList();
            UpdateClientPlayerList();
        }

        private void RefreshColorSwatchInteractivity(List<Button> buttons)
        {
            var gns = GlobalNetworkSettings.Instance;
            if (gns == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

            int myPlayerId = gns.GetPlayerIdForClient(NetworkManager.Singleton.LocalClientId);
            int mySelected = -1;

            foreach (var info in gns.PlayerLobbyInfos)
            {
                if (info.PlayerId == myPlayerId && info.ColorIndex >= 0)
                {
                    mySelected = info.ColorIndex;
                    break;
                }
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                var outline = buttons[i].GetComponent<Outline>();
                if (outline != null) outline.enabled = (i == mySelected);
            }
        }

        private void UpdateSelectedColorPreview()
        {
            var gns = GlobalNetworkSettings.Instance;
            if (gns == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

            int myPlayerId = gns.GetPlayerIdForClient(NetworkManager.Singleton.LocalClientId);
            Color myColor = Color.gray;
            bool found = false;

            foreach (var info in gns.PlayerLobbyInfos)
            {
                if (info.PlayerId == myPlayerId && info.ColorIndex >= 0)
                {
                    myColor = PredefinedColors.Colors[info.ColorIndex];
                    found = true;
                    break;
                }
            }

            if (hostSelectedColorPreview != null) hostSelectedColorPreview.color = myColor;
            if (clientSelectedColorPreview != null) clientSelectedColorPreview.color = myColor;

            if (found)
            {
                if (hostColorPickStatusText != null) hostColorPickStatusText.gameObject.SetActive(false);
                if (clientColorPickStatusText != null) clientColorPickStatusText.gameObject.SetActive(false);
            }
        }

        private void HandleColorRejected(int colorIndex)
        {
            const string msg = "This color is already taken – please choose another!";
            if (hostColorPickStatusText != null)
            {
                hostColorPickStatusText.text = msg;
                hostColorPickStatusText.gameObject.SetActive(true);
            }
            if (clientColorPickStatusText != null)
            {
                clientColorPickStatusText.text = msg;
                clientColorPickStatusText.gameObject.SetActive(true);
            }
        }

        // --- UNIT STATS UI -----------------------------------------------------------

        private void FlashChangedRows(List<(int unitIndex, string fieldName)> changed)
        {
            foreach (var key in changed)
            {
                if (_clientRowLookup.TryGetValue(key, out var row))
                    row.Flash();
            }
        }

        private void BuildUnitStatsUI(bool interactable)
        {
            if (!interactable) _clientRowLookup.Clear();

            foreach (var section in unitStatsSections)
            {
                Transform container = interactable ? section.hostContainer : section.clientContainer;
                if (section.data == null || container == null) continue;

                foreach (Transform child in container)
                    Destroy(child.gameObject);

                foreach (var field in UnitDataFieldUtil.GetEditableFields(section.data))
                {
                    var row = Instantiate(statRowPrefab, container).GetComponent<StatRowConnector>();
                    var data = section.data;
                    var f = field;

                    row.Init(f.Name, UnitDataFieldUtil.GetValue(data, f), interactable);

                    if (interactable)
                    {
                        row.inputField.onEndEdit.RemoveAllListeners();
                        row.inputField.onEndEdit.AddListener(val =>
                        {
                            if (float.TryParse(val, out float result))
                            {
                                UnitDataFieldUtil.SetValue(data, f, result);
                                GlobalNetworkSettings.Instance?.SyncUnitStatsToClients(AllUnitData);
                            }
                        });
                    }
                    else
                    {
                        _clientRowLookup[(data.index, f.Name)] = row;
                    }
                }
            }
        }

        public void OnStartButtonClick()
        {
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.SaveAssets();
#endif
            SceneManager.LoadScene(1);
        }

        private void BindElement(Slider s, TMP_InputField i, float val, float min, float max, bool isInt, System.Action<float> onUpdate)
        {
            if (s == null || i == null) return;
            s.minValue = min; s.maxValue = max; s.value = val;
            i.text = isInt ? ((int)val).ToString() : val.ToString("F1");
            s.onValueChanged.AddListener(v =>
            {
                i.text = isInt ? ((int)v).ToString() : v.ToString("F1");
                onUpdate(v);
            });
            i.onEndEdit.AddListener(txt =>
            {
                if (float.TryParse(txt, out float res))
                {
                    res = Mathf.Clamp(res, min, max);
                    s.value = res;
                    i.text = isInt ? ((int)res).ToString() : res.ToString("F1");
                    onUpdate(res);
                }
            });
        }

        private void SetAllNavButtonsInteractable(bool value)
        {
            if (goToHostBtn != null) goToHostBtn.interactable = value;
            if (goToClientBtn != null) goToClientBtn.interactable = value;
            if (backToLobbyHostBtn != null) backToLobbyHostBtn.interactable = value;
            if (backToLobbyClientBtn != null) backToLobbyClientBtn.interactable = value;
            if (startClientConnectBtn != null) startClientConnectBtn.interactable = value;
        }

        private void RefreshReconnectUI()
        {
            Debug.Log($"[DIAG] HasSavedSession={NetworkLobbyController.HasSavedSession}, savedCode='{NetworkLobbyController.GetSavedJoinCode()}'");

            bool hasSaved = NetworkLobbyController.HasSavedSession;

            if (reconnectBtn != null) reconnectBtn.gameObject.SetActive(hasSaved);
            if (throwSessionBtn != null) throwSessionBtn.gameObject.SetActive(hasSaved);
            if (goToHostBtn != null) goToHostBtn.gameObject.SetActive(!hasSaved);
            if (goToClientBtn != null) goToClientBtn.gameObject.SetActive(!hasSaved);
            if (reconnectText != null)
            {
                reconnectText.gameObject.SetActive(hasSaved);
                if (hasSaved)
                    reconnectText.text = "You have an active game in progress. Reconnect?";
            }

            ShowPanel(modeSelectorPanel);
        }

        private void OnReconnectClicked()
        {
            if (reconnectBtn != null) reconnectBtn.interactable = false;
            if (throwSessionBtn != null) throwSessionBtn.interactable = false;
            if (reconnectText != null) reconnectText.text = "Reconnecting...";

            NetworkLobbyController.Instance?.ReconnectToSession();
            ShowPanel(clientWaitingPanel);
        }

        private void OnThrowSessionClicked()
        {
            NetworkLobbyController.ClearSavedSession();
            RefreshReconnectUI();
        }
    }
}