using GridEmpire.Core;
using GridEmpire.Networking;
using GridEmpire.Shared;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
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

        [Header("Mode Buttons")]
        [SerializeField] private Button goToHostBtn;
        [SerializeField] private Button goToClientBtn;

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

        [Header("Host Network UI")]
        [SerializeField] private TMP_InputField hostCodeDisplay;
        [SerializeField] private Button copyCodeBtn;
        [SerializeField] private TextMeshProUGUI hostPlayerCountText;
        [SerializeField] private TextMeshProUGUI hostLoadingText;      // "Lobby generálása..."
        [SerializeField] private Transform hostPlayerListContainer;    // ScrollView Content
        [SerializeField] private TextMeshProUGUI hostPlayerListPrefab; // Prefab egy sorhoz

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

        // Játékos lista cache – host oldalon frissítjük
        private readonly List<TextMeshProUGUI> _hostPlayerListItems = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _clientPlayerListItems = new List<TextMeshProUGUI>();

        private readonly Dictionary<(int unitIndex, string fieldName), StatRowConnector> _clientRowLookup = new();

        private void Start()
        {
            ShowPanel(modeSelectorPanel);

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

            totalPlayersSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());
            aiBotsSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());
            mapSizeSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());
            turnSpeedSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());

            StartCoroutine(WatchNetworkSettings());
        }

        private void OnDestroy()
        {
            if (_onUnitStatsSyncedHandler != null)
                GlobalNetworkSettings.OnUnitStatsSynced -= _onUnitStatsSyncedHandler;
            if (_onUnitStatsFieldsChangedHandler != null)
                GlobalNetworkSettings.OnUnitStatsFieldsChanged -= _onUnitStatsFieldsChangedHandler;

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
            if (hostCodeDisplay != null) hostCodeDisplay.text = "HIBA";
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

        // --- BEÁLLÍTÁS VÁLTOZÁS -------------------------------------------------------

        private void OnSettingsChanged()
        {
            NetworkLobbyController.Instance?.SyncSettingsToClients(
                (int)totalPlayersSlider.value,
                (int)aiBotsSlider.value,
                (int)mapSizeSlider.value,
                turnSpeedSlider.value
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
                    hostPlayerCountText.text = "Lobby generálása...";
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
                hostPlayerCountText.text = $"{connected} / {humanPlayers} human játékos csatlakozott";

            startHostFinalBtn.interactable = connected >= humanPlayers;
        }

        // --- JÁTÉKOS LISTA ------------------------------------------------------------

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
            var controller = NetworkLobbyController.Instance;

            int totalPlayers = (int)totalPlayersSlider.value;
            int aiBots = (int)aiBotsSlider.value;
            int humanPlayers = totalPlayers - aiBots;
            int connected = controller != null ? controller.ConnectedClientsCount : 0;

            RebuildPlayerList(
                hostPlayerListContainer,
                _hostPlayerListItems,
                hostPlayerListPrefab,
                humanPlayers,
                aiBots,
                connected,
                isHost: true
            );
        }

        private void UpdateClientPlayerList()
        {
            if (clientPlayerListContainer == null || clientPlayerListPrefab == null) return;

            var gns = GlobalNetworkSettings.Instance;
            if (gns == null) return;

            int totalPlayers = gns.TotalPlayers.Value;
            int aiBots = gns.TotalAIBots.Value;
            int humanPlayers = totalPlayers - aiBots;
            int connected = gns.ConnectedPlayerCount.Value;

            RebuildPlayerList(
                clientPlayerListContainer,
                _clientPlayerListItems,
                clientPlayerListPrefab,
                humanPlayers,
                aiBots,
                connected,
                isHost: false
            );
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

        private void RebuildPlayerList(Transform container, List<TextMeshProUGUI> items, TextMeshProUGUI prefab, int humanPlayers, int aiBots, int connected, bool isHost)
        {
            Transform targetParent = container;
            ScrollRect scrollRect = container.GetComponent<ScrollRect>();
            if (scrollRect != null && scrollRect.content != null)
            {
                targetParent = scrollRect.content;
            }
            int totalSlots = humanPlayers + aiBots;

            while (items.Count < totalSlots)
            {
                var newItem = Instantiate(prefab, targetParent);
                items.Add(newItem);
            }

            for (int i = 0; i < items.Count; i++)
                items[i].gameObject.SetActive(i < totalSlots);

            // Human slotok
            for (int i = 0; i < humanPlayers; i++)
            {
                bool isConnected = i < connected;
                string dot = isConnected ? "●" : "○";
                string label;

                if (i == 0 && isHost)
                    label = $"{dot} Human {i + 1} (Te - Host)";
                else if (i == 0 && !isHost && isConnected)
                    label = $"{dot} Human {i + 1} (Host)";
                else
                    label = isConnected ? $"{dot} Human {i + 1}" : $"{dot} Human {i + 1} (vár...)";

                items[i].text = label;
                items[i].color = isConnected ? Color.white : Color.gray;
            }

            // AI slotok
            for (int i = 0; i < aiBots; i++)
            {
                int slotIdx = humanPlayers + i;
                items[slotIdx].text = $"● AI Bot {i + 1}";
                items[slotIdx].color = new Color(0.6f, 0.8f, 1f); // világoskék
            }
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

        // --- HOST JÁTÉK INDÍTÁSA ------------------------------------------------------

        private void StartHostGame()
        {
            Debug.Log("[MMC] StartHostGame elindult.");
            GameSettings settings = new GameSettings
            {
                totalPlayers = GlobalNetworkSettings.Instance.TotalPlayers.Value, //MINDEN SLOT OPEN
                aiBots = (int)aiBotsSlider.value,
                mapRadius = (int)mapSizeSlider.value,
                turnSpeedMultiplier = turnSpeedSlider.value,
                fogOfWarEnabled = fogOfWarToggle != null ? fogOfWarToggle.isOn : true
            };
            GameSettingsStorage.Save(settings);

            var unitStats = UnitStatsSnapshotUtil.Collect(AllUnitData);
            Debug.Log($"[MMC] UnitStats collect kész, units={unitStats.units.Count}");

            UnitStatsStorage.Save(unitStats);
            Debug.Log("[MMC] UnitStatsStorage.Save kész.");

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
                SetClientStatus("Add meg a csatlakozási kódot!", Color.red);
                return;
            }

            string joinCode = clientCodeInput.text.Trim().ToUpper();
            SetClientStatus("Csatlakozás...", Color.yellow);
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
                clientPlayerCountText.text = $"{connected} / {humanPlayers} játékos";
            if (clientTotalPlayersText != null)
                clientTotalPlayersText.text = $"Játékosok: {totalPlayers}";
            if (clientAiBotsText != null)
                clientAiBotsText.text = $"AI: {aiBots}";
            if (clientMapSizeText != null)
                clientMapSizeText.text = $"Pálya méret: {gns.NetworkMapRadius.Value}";
            if (clientTurnSpeedText != null)
                clientTurnSpeedText.text = $"Körsebesség: {gns.TurnSpeed.Value:F1}";
        }

        // --- SETTINGS UI -------------------------------------------------------------
        private void SetupGeneralUI(GameSettings settings)
        {
            BindElement(totalPlayersSlider, totalPlayersInput, settings.totalPlayers, 1, 6, true, (v) => { });
            BindElement(aiBotsSlider, aiBotsInput, settings.aiBots, 0, 6, true, (v) => { });
            BindElement(turnSpeedSlider, turnSpeedInput, settings.turnSpeedMultiplier, 0.5f, 250f, false, (v) => { });
            BindElement(mapSizeSlider, mapSizeInput, settings.mapRadius, 1, 25, true, (v) => { });
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
            hostSettingsPanel.SetActive(panelToShow == hostSettingsPanel);
            clientWaitingPanel.SetActive(panelToShow == clientWaitingPanel);
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
    }
}