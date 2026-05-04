using GridEmpire.Core;
using GridEmpire.Networking;
using GridEmpire.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
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

        [Header("Unit Assets")]
        [SerializeField] private List<UnitData> unitDataList;

        [Header("Networking")]
        [SerializeField] private GameObject globalSettingsPrefab;
        [SerializeField] private GameObject gameControllerPrefab;

        private bool _servicesInitialized = false;
        private ISession _currentSession;
        private bool _sessionExists = false;

        // Játékos lista cache – host oldalon frissítjük
        private readonly List<TextMeshProUGUI> _hostPlayerListItems = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _clientPlayerListItems = new List<TextMeshProUGUI>();

        private async void Start()
        {
            ShowPanel(modeSelectorPanel);

            GameSettings savedSettings = GameSettings.Load();
            SetupGeneralUI(savedSettings);
            InitializeUnitStatsUI();

            SetHostLoading(false);
            if (startHostFinalBtn != null) startHostFinalBtn.interactable = false;

            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                _servicesInitialized = true;
                Debug.Log("[UGS] Inicializálva és bejelentkezve.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[UGS] Inicializálás sikertelen: {e.Message}");
            }

            // Host panelre lépéskor automatikusan generálódik a lobby
            goToHostBtn.onClick.AddListener(async () =>
            {
                ShowPanel(hostSettingsPanel);
                await CreateHostSession();
            });

            goToClientBtn.onClick.AddListener(() => ShowPanel(clientWaitingPanel));

            startHostFinalBtn.onClick.AddListener(StartHostGame);

            if (startClientConnectBtn != null)
                startClientConnectBtn.onClick.AddListener(StartClientConnect);

            if (copyCodeBtn != null)
                copyCodeBtn.onClick.AddListener(() =>
                {
                    if (hostCodeDisplay != null && !string.IsNullOrEmpty(hostCodeDisplay.text))
                        GUIUtility.systemCopyBuffer = hostCodeDisplay.text;
                });

            // Back to lobby gombok
            if (backToLobbyHostBtn != null)
                backToLobbyHostBtn.onClick.AddListener(OnHostBackToLobby);

            if (backToLobbyClientBtn != null)
                backToLobbyClientBtn.onClick.AddListener(OnClientBackToLobby);

            // Beállítás változáskor frissítés
            totalPlayersSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());
            aiBotsSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());
            mapSizeSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());
            turnSpeedSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());

            StartCoroutine(WatchNetworkSettings());
        }

        // ─── SESSION GENERÁLÁS ────────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task CreateHostSession()
        {
            if (!_servicesInitialized) return;

            // Ha már van session, zárjuk le
            if (_sessionExists && _currentSession != null)
            {
                try { await _currentSession.LeaveAsync(); }
                catch (Exception e) { Debug.LogWarning($"[Host] Session lezárás: {e.Message}"); }
                _sessionExists = false;
                _currentSession = null;
            }

            SetHostLoading(true);
            if (startHostFinalBtn != null) startHostFinalBtn.interactable = false;
            if (hostCodeDisplay != null) hostCodeDisplay.text = "...";

            try
            {
                int maxPlayers = (int)totalPlayersSlider.value;
                var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();

                _currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                _sessionExists = true;

                // Kliens csatlakozásakor szinkronizáljuk a beállításokat
                _currentSession.PlayerJoined += playerId =>
                {
                    Debug.Log($"[Host] Játékos csatlakozott: {playerId}");
                    SyncSettingsToClients();
                    UpdateHostPlayerList();
                    UpdateStartButtonState();
                    UpdateAiBotSliderMax();
                };

                // Kliens kilépésekor frissítés
                _currentSession.PlayerLeaving += playerId =>
                {
                    Debug.Log($"[Host] Játékos kilépett: {playerId}");
                    // TODO: UI értesítés hogy ki lépett ki
                    UpdateHostPlayerList();
                    UpdateStartButtonState();
                    UpdateAiBotSliderMax();
                };

                if (hostCodeDisplay != null)
                    hostCodeDisplay.text = _currentSession.Code;

                Debug.Log($"[Host] Session kész. Kód: {_currentSession.Code}");

                UpdateHostPlayerList();
                UpdateStartButtonState();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Host] Session hiba: {e.Message}");
                if (hostCodeDisplay != null) hostCodeDisplay.text = "HIBA";
            }
            finally
            {
                SetHostLoading(false);
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

        // ─── BEÁLLÍTÁS VÁLTOZÁS ───────────────────────────────────────────────────────

        private void OnSettingsChanged()
        {
            SyncSettingsToClients();
            UpdateStartButtonState();
            UpdateHostPlayerList();
            UpdateAiBotSliderMax();
        }

        private void SyncSettingsToClients()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost &&
                GlobalNetworkSettings.Instance != null)
            {
                GlobalNetworkSettings.Instance.UpdateSettings(
                    (int)totalPlayersSlider.value,
                    (int)aiBotsSlider.value,
                    (int)mapSizeSlider.value,
                    turnSpeedSlider.value
                );
            }
        }

        private void UpdateStartButtonState()
        {
            if (startHostFinalBtn == null) return;

            if (!_sessionExists)
            {
                startHostFinalBtn.interactable = false;
                if (hostPlayerCountText != null)
                    hostPlayerCountText.text = "Lobby generálása...";
                return;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            {
                startHostFinalBtn.interactable = false;
                return;
            }

            int connected = NetworkManager.Singleton.ConnectedClientsIds.Count;
            int totalPlayers = (int)totalPlayersSlider.value;
            int aiBots = (int)aiBotsSlider.value;
            int humanPlayers = totalPlayers - aiBots;

            if (hostPlayerCountText != null)
                hostPlayerCountText.text = $"{connected} / {humanPlayers} human játékos csatlakozott";

            startHostFinalBtn.interactable = connected >= humanPlayers;
        }

        // ─── JÁTÉKOS LISTA ────────────────────────────────────────────────────────────

        private void UpdateHostPlayerList()
        {
            if (hostPlayerListContainer == null || hostPlayerListPrefab == null) return;

            int totalPlayers = (int)totalPlayersSlider.value;
            int aiBots = (int)aiBotsSlider.value;
            int humanPlayers = totalPlayers - aiBots;
            int connected = _currentSession?.Players?.Count ?? 0;

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
            int totalPlayers = (int)totalPlayersSlider.value;
            int connectedHumans = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
                ? NetworkManager.Singleton.ConnectedClientsIds.Count
                : 1;

            int maxBots = Mathf.Max(0, totalPlayers - connectedHumans);

            aiBotsSlider.maxValue = maxBots;

            if (aiBotsSlider.value > maxBots)
            {
                aiBotsSlider.value = maxBots;
                aiBotsInput.text = maxBots.ToString();
            }
        }

        /// <summary>
        /// Újraépíti a játékos listát.
        /// Formátum:
        ///   ● Human 1 (Te - Host)   ← ha host
        ///   ● Human 2               ← csatlakozott
        ///   ○ Human 3               ← vár
        ///   ○ AI Bot 1              ← bot
        /// </summary>
        private void RebuildPlayerList(
            Transform container,
            List<TextMeshProUGUI> items,
            TextMeshProUGUI prefab,
            int humanPlayers,
            int aiBots,
            int connected,
            bool isHost)
        {
            int totalSlots = humanPlayers + aiBots;

            // Bővítés ha kell
            while (items.Count < totalSlots)
            {
                var newItem = Instantiate(prefab, container);
                items.Add(newItem);
            }

            // Elrejtés ha kevesebb kell
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

        // ─── BACK TO LOBBY ────────────────────────────────────────────────────────────

        private async void OnHostBackToLobby()
        {
            if (_sessionExists && _currentSession != null)
            {
                try { await _currentSession.LeaveAsync(); }
                catch (Exception e) { Debug.LogWarning($"[Host] Session lezárás: {e.Message}"); }
                _sessionExists = false;
                _currentSession = null;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            if (startHostFinalBtn != null) startHostFinalBtn.interactable = false;
            if (hostCodeDisplay != null) hostCodeDisplay.text = "";
            if (hostPlayerCountText != null) hostPlayerCountText.text = "";

            ClearPlayerList(_hostPlayerListItems);
            ShowPanel(modeSelectorPanel);
        }

        private async void OnClientBackToLobby()
        {
            if (_currentSession != null)
            {
                try
                {
                    await _currentSession.LeaveAsync();
                    Debug.Log("[Client] Kilépett a sessionből.");
                }
                catch (Exception e) { Debug.LogWarning($"[Client] Session elhagyás: {e.Message}"); }
                _currentSession = null;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            SetClientStatus("", Color.white);
            ClearPlayerList(_clientPlayerListItems);
            ShowPanel(modeSelectorPanel);
        }

        private void ClearPlayerList(List<TextMeshProUGUI> items)
        {
            foreach (var item in items)
                if (item != null) Destroy(item.gameObject);
            items.Clear();
        }

        // ─── HOST JÁTÉK INDÍTÁSA ──────────────────────────────────────────────────────

        private void StartHostGame()
        {
            if (_currentSession == null) { Debug.LogError("[Host] Nincs aktív session."); return; }

            GameSettings settings = new GameSettings
            {
                totalPlayers = (int)totalPlayersSlider.value,
                aiBots = (int)aiBotsSlider.value,
                mapRadius = (int)mapSizeSlider.value,
                turnSpeedMultiplier = turnSpeedSlider.value
            };
            settings.Save();

            startHostFinalBtn.interactable = false;
            if (backToLobbyHostBtn != null) backToLobbyHostBtn.interactable = false;

            var globalSettings = FindAnyObjectByType<GlobalNetworkSettings>();
            if (globalSettings != null)
                globalSettings.InitializeFromSettings(settings);

            if (!NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.StartHost();

            StartCoroutine(LoadGameSceneSafe());
        }

        // ─── CLIENT ───────────────────────────────────────────────────────────────────

        private async void StartClientConnect()
        {
            if (!_servicesInitialized) { SetClientStatus("Szolgáltatások nem elérhetők!", Color.red); return; }
            if (clientCodeInput == null || string.IsNullOrEmpty(clientCodeInput.text))
            {
                SetClientStatus("Add meg a csatlakozási kódot!", Color.red);
                return;
            }

            string joinCode = clientCodeInput.text.Trim().ToUpper();
            SetClientStatus("Csatlakozás...", Color.yellow);
            if (startClientConnectBtn != null) startClientConnectBtn.interactable = false;

            try
            {
                _currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
                Debug.Log($"[Client] Session OK.");

                if (!NetworkManager.Singleton.IsListening)
                    NetworkManager.Singleton.StartClient();

                SetClientStatus("Csatlakozva! Várakozás a hostra...", Color.green);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Client] Session hiba: {e.Message}");
                SetClientStatus($"Hiba: {e.Message}", Color.red);
                if (startClientConnectBtn != null) startClientConnectBtn.interactable = true;
            }
        }

        private void SetClientStatus(string msg, Color color)
        {
            if (clientStatusText == null) return;
            clientStatusText.text = msg;
            clientStatusText.color = color;
        }

        // ─── WATCH NETWORK SETTINGS ───────────────────────────────────────────────────

        private IEnumerator WatchNetworkSettings()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.5f);

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
                {
                    if (GlobalNetworkSettings.Instance != null)
                        GlobalNetworkSettings.Instance.ConnectedPlayerCount.Value =
                            NetworkManager.Singleton.ConnectedClientsIds.Count;
                    UpdateStartButtonState();
                    UpdateAiBotSliderMax();
                }

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient &&
                    !NetworkManager.Singleton.IsHost &&
                    GlobalNetworkSettings.Instance != null)
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

        // ─── SCENE LOAD ───────────────────────────────────────────────────────────────

        private IEnumerator LoadGameSceneSafe()
        {
            while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
                yield return null;

            int expectedHumans = (int)totalPlayersSlider.value - (int)aiBotsSlider.value;

            while (NetworkManager.Singleton.ConnectedClientsIds.Count < expectedHumans)
            {
                yield return new WaitForSeconds(0.5f);
            }

            Debug.Log("[Host] Minden kliens csatlakozott, scene load indul.");
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }

        // ─── SETTINGS UI ─────────────────────────────────────────────────────────────

        private void SetupGeneralUI(GameSettings settings)
        {
            BindElement(totalPlayersSlider, totalPlayersInput, settings.totalPlayers, 1, 6, true, (v) => { });
            BindElement(aiBotsSlider, aiBotsInput, settings.aiBots, 0, 6, true, (v) => { });
            BindElement(turnSpeedSlider, turnSpeedInput, settings.turnSpeedMultiplier, 0.5f, 250f, false, (v) => { });
            BindElement(mapSizeSlider, mapSizeInput, settings.mapRadius, 8, 25, true, (v) => { });
        }

        private void ShowPanel(GameObject panelToShow)
        {
            modeSelectorPanel.SetActive(panelToShow == modeSelectorPanel);
            hostSettingsPanel.SetActive(panelToShow == hostSettingsPanel);
            clientWaitingPanel.SetActive(panelToShow == clientWaitingPanel);
        }

        private void InitializeUnitStatsUI()
        {
            StatRowConnector[] allRows = GetComponentsInChildren<StatRowConnector>(true);
            foreach (var row in allRows)
            {
                UnitData targetData = unitDataList.Find(d => d.index == row.unitIndex);
                if (targetData == null) continue;
                row.Init(row.type.ToString(), GetAssetStatValue(targetData, row.type));
                row.inputField.onEndEdit.RemoveAllListeners();
                row.inputField.onEndEdit.AddListener(val =>
                {
                    if (float.TryParse(val, out float result))
                        SetAssetStatValue(targetData, row.type, result);
                });
            }
        }

        private float GetAssetStatValue(UnitData d, StatRowConnector.StatType t) => t switch
        {
            StatRowConnector.StatType.Cost => d.cost,
            StatRowConnector.StatType.CostPerTurn => d.costPerTurn,
            StatRowConnector.StatType.MaxHp => d.maxHp,
            StatRowConnector.StatType.StaminaPerTurn => d.staminaPerTurn,
            StatRowConnector.StatType.MaxStamina => d.maxStamina,
            StatRowConnector.StatType.ConquerSpeed => d.conquerSpeed,
            StatRowConnector.StatType.ExploreSpeed => d.exploreSpeed,
            StatRowConnector.StatType.BaseDamage => d.baseDamage,
            StatRowConnector.StatType.BonusDamage => d.bonusDamage,
            _ => 0
        };

        private void SetAssetStatValue(UnitData d, StatRowConnector.StatType t, float val)
        {
            switch (t)
            {
                case StatRowConnector.StatType.Cost: d.cost = (int)val; break;
                case StatRowConnector.StatType.CostPerTurn: d.costPerTurn = (int)val; break;
                case StatRowConnector.StatType.MaxHp: d.maxHp = (int)val; break;
                case StatRowConnector.StatType.StaminaPerTurn: d.staminaPerTurn = val; break;
                case StatRowConnector.StatType.MaxStamina: d.maxStamina = val; break;
                case StatRowConnector.StatType.ConquerSpeed: d.conquerSpeed = val; break;
                case StatRowConnector.StatType.ExploreSpeed: d.exploreSpeed = val; break;
                case StatRowConnector.StatType.BaseDamage: d.baseDamage = (int)val; break;
                case StatRowConnector.StatType.BonusDamage: d.bonusDamage = (int)val; break;
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(d);
#endif
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
    }
}