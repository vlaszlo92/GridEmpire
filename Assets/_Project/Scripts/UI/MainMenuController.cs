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
        [SerializeField] private Button generateLobbyBtn;   // ÚJ: lobby generálás
        [SerializeField] private Button startHostFinalBtn;  // Start – csak ha elég játékos

        [Header("Client Action Buttons")]
        [SerializeField] private Button startClientConnectBtn;

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
        [SerializeField] private TextMeshProUGUI hostPlayerCountText; // pl. "2 / 3 játékos"

        [Header("Client Network UI")]
        [SerializeField] private TMP_InputField clientCodeInput;
        [SerializeField] private TextMeshProUGUI clientStatusText;

        // Kliens lobby panel elemek
        [Header("Client Lobby Info UI")]
        [SerializeField] private TextMeshProUGUI clientPlayerCountText;   // "2 / 3 játékos"
        [SerializeField] private TextMeshProUGUI clientTotalPlayersText;  // "Játékosok: 3"
        [SerializeField] private TextMeshProUGUI clientAiBotsText;        // "AI: 1"
        [SerializeField] private TextMeshProUGUI clientMapSizeText;       // "Pálya: 15"
        [SerializeField] private TextMeshProUGUI clientTurnSpeedText;     // "Sebesség: 1.0"

        [Header("Unit Assets")]
        [SerializeField] private List<UnitData> unitDataList;

        [Header("Networking")]
        [SerializeField] private GameObject globalSettingsPrefab;
        [SerializeField] private GameObject gameControllerPrefab;

        private bool _servicesInitialized = false;
        private ISession _currentSession;
        private bool _sessionExists = false;

        private async void Start()
        {
            ShowPanel(modeSelectorPanel);

            GameSettings savedSettings = GameSettings.Load();
            SetupGeneralUI(savedSettings);
            InitializeUnitStatsUI();

            // Gombok kezdeti állapota
            if (generateLobbyBtn != null) generateLobbyBtn.interactable = false;
            if (startHostFinalBtn != null) startHostFinalBtn.interactable = false;

            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                _servicesInitialized = true;
                Debug.Log("[UGS] Inicializálva és bejelentkezve.");

                // UGS kész → GenerateLobby gomb aktiválható
                if (generateLobbyBtn != null) generateLobbyBtn.interactable = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[UGS] Inicializálás sikertelen: {e.Message}");
            }

            goToHostBtn.onClick.AddListener(() => ShowPanel(hostSettingsPanel));
            goToClientBtn.onClick.AddListener(() => ShowPanel(clientWaitingPanel));

            // ÚJ: GenerateLobby gomb
            if (generateLobbyBtn != null)
                generateLobbyBtn.onClick.AddListener(OnGenerateLobbyClicked);

            startHostFinalBtn.onClick.AddListener(StartHostGame);

            if (startClientConnectBtn != null)
                startClientConnectBtn.onClick.AddListener(StartClientConnect);

            if (copyCodeBtn != null)
                copyCodeBtn.onClick.AddListener(() =>
                {
                    if (hostCodeDisplay != null && !string.IsNullOrEmpty(hostCodeDisplay.text))
                        GUIUtility.systemCopyBuffer = hostCodeDisplay.text;
                });

            // Beállítás slidereken változás → frissítsük a lobby-t ha már létezik
            totalPlayersSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());
            aiBotsSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());
            mapSizeSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());
            turnSpeedSlider?.onValueChanged.AddListener(_ => OnSettingsChanged());

            // Kliens oldali NetworkVariable figyelés – ha már fut a network
            StartCoroutine(WatchNetworkSettings());
        }

        // ─── LOBBY GENERÁLÁS ─────────────────────────────────────────────────────────

        private async void OnGenerateLobbyClicked()
        {
            if (!_servicesInitialized) return;

            // Ha már van session, zárjuk le
            if (_sessionExists && _currentSession != null)
            {
                try
                {
                    await _currentSession.LeaveAsync();
                    Debug.Log("[Host] Régi session lezárva.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Host] Session lezárás hiba: {e.Message}");
                }
                _sessionExists = false;
                _currentSession = null;
            }

            await CreateHostSession();
        }

        private async System.Threading.Tasks.Task CreateHostSession()
        {
            if (generateLobbyBtn != null) generateLobbyBtn.interactable = false;
            if (startHostFinalBtn != null) startHostFinalBtn.interactable = false;
            if (hostCodeDisplay != null)
            {
                hostCodeDisplay.text = "...";
                hostCodeDisplay.interactable = false;
            }

            try
            {
                int maxPlayers = (int)totalPlayersSlider.value;
                var options = new SessionOptions
                {
                    MaxPlayers = maxPlayers
                }.WithRelayNetwork();

                _currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                _sessionExists = true;

                // Amikor kliens csatlakozik, küldjük újra az aktuális beállításokat
                _currentSession.PlayerJoined += playerId =>
                {
                    var globalSettings = FindAnyObjectByType<GlobalNetworkSettings>();
                    if (globalSettings != null)
                    {
                        globalSettings.UpdateSettings(
                            (int)totalPlayersSlider.value,
                            (int)aiBotsSlider.value,
                            (int)mapSizeSlider.value,
                            turnSpeedSlider.value
                        );
                        if (NetworkManager.Singleton != null)
                            globalSettings.ConnectedPlayerCount.Value =
                                NetworkManager.Singleton.ConnectedClientsIds.Count;
                    }
                    UpdateStartButtonState();
                };

                string joinCode = _currentSession.Code;
                Debug.Log($"[Host] Session kész. Kód: {joinCode}");

                if (hostCodeDisplay != null)
                    hostCodeDisplay.text = joinCode;

                if (generateLobbyBtn != null) generateLobbyBtn.interactable = true;
                UpdateStartButtonState();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Host] Session hiba: {e.Message}");
                if (hostCodeDisplay != null) hostCodeDisplay.text = "HIBA";
                if (generateLobbyBtn != null) generateLobbyBtn.interactable = true;
            }
        }

        // ─── BEÁLLÍTÁS VÁLTOZÁS ───────────────────────────────────────────────────────

        private void OnSettingsChanged()
        {
            // Ha NetworkManager fut (már van lobby és csatlakoztak), frissítjük élőben
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

            UpdateStartButtonState();
        }

        private void UpdateStartButtonState()
        {
            if (startHostFinalBtn == null) return;

            if (!_sessionExists)
            {
                startHostFinalBtn.interactable = false;
                if (hostPlayerCountText != null)
                    hostPlayerCountText.text = "Először generálj lobby kódot!";
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
                hostPlayerCountText.text = $"{connected} / {humanPlayers} játékos csatlakozott";

            // Start aktív ha elég human játékos van
            startHostFinalBtn.interactable = connected >= humanPlayers;
        }

        // ─── HOST JÁTÉK INDÍTÁSA ──────────────────────────────────────────────────────

        private void StartHostGame()
        {
            if (_currentSession == null)
            {
                Debug.LogError("[Host] Nincs aktív session.");
                return;
            }

            GameSettings settings = new GameSettings
            {
                totalPlayers = (int)totalPlayersSlider.value,
                aiBots = (int)aiBotsSlider.value,
                mapRadius = (int)mapSizeSlider.value,
                turnSpeedMultiplier = turnSpeedSlider.value
            };
            settings.Save();

            startHostFinalBtn.interactable = false;
            generateLobbyBtn.interactable = false;

            var globalSettings = FindAnyObjectByType<GlobalNetworkSettings>();
            if (globalSettings != null)
                globalSettings.InitializeFromSettings(settings);

            if (!NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.StartHost();
                Debug.Log("[Host] StartHost() meghívva.");
            }

            StartCoroutine(LoadGameSceneSafe());
        }

        // ─── CLIENT ───────────────────────────────────────────────────────────────────

        private async void StartClientConnect()
        {
            if (!_servicesInitialized)
            {
                SetClientStatus("Szolgáltatások nem elérhetők!", Color.red);
                return;
            }

            if (clientCodeInput == null || string.IsNullOrEmpty(clientCodeInput.text))
            {
                SetClientStatus("Add meg a csatlakozási kódot!", Color.red);
                return;
            }

            string joinCode = clientCodeInput.text.Trim().ToUpper();
            SetClientStatus("Csatlakozás...", Color.yellow);

            if (startClientConnectBtn != null)
                startClientConnectBtn.interactable = false;

            try
            {
                _currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
                Debug.Log($"[Client] Session OK.");

                if (!NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.StartClient();
                    Debug.Log("[Client] StartClient() meghívva.");
                }

                SetClientStatus("Csatlakozva! Várakozás a hostra...", Color.green);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Client] Session hiba: {e.Message}");
                SetClientStatus($"Hiba: {e.Message}", Color.red);
                if (startClientConnectBtn != null)
                    startClientConnectBtn.interactable = true;
            }
        }

        private void SetClientStatus(string msg, Color color)
        {
            Debug.Log($"[Client Status] {msg}");
            if (clientStatusText == null) return;
            clientStatusText.text = msg;
            clientStatusText.color = color;
        }

        // ─── KLIENS LOBBY INFO FRISSÍTÉS ─────────────────────────────────────────────

        private IEnumerator WatchNetworkSettings()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.5f);

                // Host oldal: frissítjük a csatlakozott játékosok számát
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
                {
                    GlobalNetworkSettings.Instance.ConnectedPlayerCount.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
                    UpdateStartButtonState();
                }

                // Kliens oldal: mutatjuk a szerver által küldött beállításokat
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient &&
                    !NetworkManager.Singleton.IsHost &&
                    GlobalNetworkSettings.Instance != null)
                {
                    UpdateClientLobbyInfo();
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

            int expectedPlayers = (int)totalPlayersSlider.value;
            int expectedHumans = expectedPlayers - (int)aiBotsSlider.value;

            while (NetworkManager.Singleton.ConnectedClientsIds.Count < expectedHumans)
            {
                Debug.Log($"[Host] Kliensek: {NetworkManager.Singleton.ConnectedClientsIds.Count}/{expectedHumans}");
                yield return new WaitForSeconds(0.5f);
            }

            Debug.Log("[Host] Minden kliens csatlakozott, scene load indul.");
            var status = NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            Debug.Log($"[Host] Scene load status: {status}");
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