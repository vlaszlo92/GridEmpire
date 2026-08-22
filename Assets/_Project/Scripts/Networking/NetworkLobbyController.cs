using System;
using System.Collections;
using System.Linq;
using GridEmpire.Shared;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

namespace GridEmpire.Networking
{
    /// <summary>
    /// A lobby network layer: UGS session management, NetworkManager Host/Client initialization,
    /// connection approval, disconnect monitoring, GlobalNetworkSettings synchronization.
    /// The MainMenuController accesses network functions exclusively through this controller.
    /// </summary>
    public class NetworkLobbyController : MonoBehaviour
    {
        public static NetworkLobbyController Instance { get; private set; }

        public event Action OnServicesInitialized;
        public event Action<string> OnHostSessionReady;      // join code
        public event Action<string> OnHostSessionFailed;     // error message
        public event Action OnSessionPlayersChanged;         // host: player joined/left
        public event Action<string, bool> OnClientConnectResult; // (message, success)
        public event Action OnHostConnectionLost;             // client disconnected from the host

        public bool ServicesReady { get; private set; }
        public bool IsHostSessionActive { get; private set; }
        public int SessionPlayersCount => _currentSession?.Players?.Count ?? 0;

        public bool IsListening => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        public bool IsClient => NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;
        public int ConnectedClientsCount => IsListening ? NetworkManager.Singleton.ConnectedClientsIds.Count : 0;

        private ISession _currentSession;
        private bool _isCreatingSession;
        private bool _isConnecting;
        private bool _disconnectCallbackSubscribed;

        private bool _sessionOperationInProgress;
        public bool IsSessionOperationInProgress => _sessionOperationInProgress;
        private const string LastJoinCodeKey = "LastJoinCode";
        private const string LastSessionIdKey = "LastSessionId";
        private const string IdentitySecretKey = "IdentitySecret";
        private const string IdentitySecretPropertyKey = "identity_secret";

        public static bool HasSavedSession => PlayerPrefs.HasKey(LastJoinCodeKey);
        public static string GetSavedJoinCode() => PlayerPrefs.GetString(LastJoinCodeKey, "");
        public static string GetSavedSessionId() => PlayerPrefs.GetString(LastSessionIdKey, "");
        public static void ClearSavedSession()
        {
            PlayerPrefs.DeleteKey(LastJoinCodeKey);
            PlayerPrefs.DeleteKey(LastSessionIdKey);
            PlayerPrefs.DeleteKey(IdentitySecretKey);
            PlayerPrefs.Save();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private async void Start()
        {
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                ServicesReady = true;
                Debug.Log("[NetworkLobbyController] UGS initialized and signed in.");
                OnServicesInitialized?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkLobbyController] UGS initialization failed: {e.Message}");
            }

            StartCoroutine(WatchClientDisconnect());
        }

        private IEnumerator WatchClientDisconnect()
        {
            while (!_disconnectCallbackSubscribed)
            {
                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
                    _disconnectCallbackSubscribed = true;
                }
                yield return null;
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            if (NetworkManager.Singleton.IsHost) return;
            if (clientId != NetworkManager.Singleton.LocalClientId) return;

            Debug.Log($"[NetworkLobbyController] Connection lost with the host. Reason: '{NetworkManager.Singleton.DisconnectReason}'");
            SceneManager.LoadScene("MainMenuScene");
            OnHostConnectionLost?.Invoke();
        }

        // --- IDENTITY ---------------------------------------------------------

        private async Task EnsureIdentitySecret()
        {
            if (_currentSession == null)
                throw new InvalidOperationException("No active session.");

            string secret = PlayerPrefs.GetString(IdentitySecretKey, "");
            if (string.IsNullOrEmpty(secret))
            {
                secret = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(IdentitySecretKey, secret);
                PlayerPrefs.Save();
            }

            _currentSession.CurrentPlayer.SetProperty(IdentitySecretPropertyKey, new PlayerProperty(secret));
            await _currentSession.SaveCurrentPlayerDataAsync();
        }

        private void SetConnectionIdentity()
        {
            string secret = GetOrCreateIdentitySecret();
            string payload = $"{AuthenticationService.Instance.PlayerId}|{secret}";
            NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(payload);
        }

        // --- HOST SESSION ---------------------------------------------------

        public async Task CreateHostSession(int maxPlayers)
        {
            if (!ServicesReady || _sessionOperationInProgress) return;
            _sessionOperationInProgress = true;
            try
            {
                if (IsHostSessionActive)
                    await LeaveCurrentSession();

                await ShutdownNetworkManagerIfNeeded();

                if (NetworkManager.Singleton != null)
                {
                    SetConnectionIdentity();
                    NetworkManager.Singleton.ConnectionApprovalCallback = ApproveConnection;
                }

                var options = new SessionOptions
                {
                    MaxPlayers = maxPlayers,
                    PlayerProperties = new System.Collections.Generic.Dictionary<string, PlayerProperty>
            {
                { IdentitySecretPropertyKey, new PlayerProperty(GetOrCreateIdentitySecret()) }
            }
                }.WithRelayNetwork();

                _currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                IsHostSessionActive = true;

                _currentSession.PlayerJoined += _ => OnSessionPlayersChanged?.Invoke();
                _currentSession.PlayerLeaving += _ => OnSessionPlayersChanged?.Invoke();

                OnHostSessionReady?.Invoke(_currentSession.Code);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkLobbyController] Session error: {e.Message}");
                OnHostSessionFailed?.Invoke(e.Message);
            }
            finally
            {
                _sessionOperationInProgress = false;
            }
        }

        public void SyncSettingsToClients(int totalPlayers, int aiBots, int mapRadius, float turnSpeed, float goldPerTurnPerCell)
        {
            if (IsHost && GlobalNetworkSettings.Instance != null)
                GlobalNetworkSettings.Instance.UpdateSettings(totalPlayers, aiBots, mapRadius, turnSpeed, goldPerTurnPerCell);
        }

        public void SetFogOfWar(bool enabled)
        {
            if (GlobalNetworkSettings.Instance != null)
                GlobalNetworkSettings.Instance.FogOfWarEnabled.Value = enabled;
        }

        public void UpdateHostConnectedPlayerCount()
        {
            if (IsHost && GlobalNetworkSettings.Instance != null)
                GlobalNetworkSettings.Instance.ConnectedPlayerCount.Value = ConnectedClientsCount;
        }

        public void StartHostGame(GameSettings settings, string sceneName, int expectedHumans)
        {
            if (GlobalNetworkSettings.Instance != null)
                GlobalNetworkSettings.Instance.InitializeFromSettings(settings);
            else
            {
                Debug.LogError("[NetworkLobbyController] GlobalNetworkSettings not found in the scene!");
                return;
            }

            NetworkDebugDump.DumpServerState(settings, sceneName, expectedHumans);
            GlobalNetworkSettings.Instance?.TriggerDebugDumpClientRpc();

            StartCoroutine(LoadGameSceneRoutine(sceneName, expectedHumans));
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;

            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                ConnectionManager.Instance?.RegisterConnection(request.ClientNetworkId, AuthenticationService.Instance.PlayerId);
                response.Approved = true;
                return;
            }
            response.Approved = false;

            if (_currentSession == null)
            {
                response.Reason = "Session unavailable.";
                return;
            }

            if (request.Payload == null || request.Payload.Length == 0)
            {
                response.Reason = "Missing identity payload.";
                return;
            }

            string payload = System.Text.Encoding.UTF8.GetString(request.Payload);
            string[] parts = payload.Split('|');
            if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
            {
                response.Reason = "Invalid identity payload.";
                return;
            }

            string authId = parts[0];
            string secret = parts[1];

            var player = _currentSession.AsHost().Players.FirstOrDefault(p => p.Id == authId);
            if (player == null)
            {
                response.Reason = "Player is not a session member.";
                return;
            }

            if (!player.Properties.TryGetValue(IdentitySecretPropertyKey, out var property) || property.Value != secret)
            {
                response.Reason = "Identity validation failed.";
                return;
            }

            ConnectionManager.Instance?.RegisterConnection(request.ClientNetworkId, authId);

            response.Approved = true;

            Debug.Log($"[NetworkLobbyController] Connection approved. AuthId={authId}, ClientId={request.ClientNetworkId}");
        }

        private IEnumerator LoadGameSceneRoutine(string sceneName, int expectedHumans)
        {
            while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
                yield return null;

            while (NetworkManager.Singleton.ConnectedClientsIds.Count < expectedHumans)
                yield return new WaitForSeconds(0.5f);

            Debug.Log("[NetworkLobbyController] All clients connected, scene load starting.");
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        public async Task LeaveHostSession()
        {
            if (_sessionOperationInProgress) return;
            _sessionOperationInProgress = true;
            try
            {
                await LeaveCurrentSession();
                await ShutdownNetworkManagerIfNeeded();
            }
            finally
            {
                _sessionOperationInProgress = false;
            }
            ClearSavedSession();
        }

        // --- CLIENT ---------------------------------------------------------

        public async Task JoinSession(string joinCode)
        {
            if (!ServicesReady || _sessionOperationInProgress) return;
            _sessionOperationInProgress = true;
            try
            {
                await ShutdownNetworkManagerIfNeeded();

                if (NetworkManager.Singleton != null)
                    SetConnectionIdentity();

                var joinOptions = new JoinSessionOptions
                {
                    PlayerProperties = new System.Collections.Generic.Dictionary<string, PlayerProperty>
                    {
                        { IdentitySecretPropertyKey, new PlayerProperty(GetOrCreateIdentitySecret()) }
                    }
                };

                try
                {
                    _currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode, joinOptions);

                    PlayerPrefs.SetString(LastJoinCodeKey, joinCode);
                    PlayerPrefs.SetString(LastSessionIdKey, _currentSession.Id);
                    PlayerPrefs.Save();
                }
                catch (Exception joinEx) when (joinEx.Message.Contains("already a member"))
                {
                    Debug.LogWarning($"[NetworkLobbyController] Already a member of a session. joinCode={joinCode}");
                    throw;
                }

                OnClientConnectResult?.Invoke("Connected! Waiting for the host...", true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NetworkLobbyController] Session error: {e.Message}");
                OnClientConnectResult?.Invoke($"Error: {e.Message}", false);
            }
            finally
            {
                _sessionOperationInProgress = false;
            }
        }

        public async Task LeaveClientSession()
        {
            if (_sessionOperationInProgress) return;
            _sessionOperationInProgress = true;
            try
            {
                await LeaveCurrentSession();
                await ShutdownNetworkManagerIfNeeded();
            }
            finally
            {
                _sessionOperationInProgress = false;
            }
            ClearSavedSession();
        }

        public async void ReconnectToSession()
        {
            if (!ServicesReady || _sessionOperationInProgress) return;

            string sessionId = GetSavedSessionId();
            if (string.IsNullOrEmpty(sessionId))
            {
                string savedJoinCode = GetSavedJoinCode();
                if (!string.IsNullOrEmpty(savedJoinCode)) await JoinSession(savedJoinCode);
                return;
            }

            _sessionOperationInProgress = true;
            try
            {
                await ShutdownNetworkManagerIfNeeded();

                if (NetworkManager.Singleton != null)
                    SetConnectionIdentity();

                _currentSession = await MultiplayerService.Instance.ReconnectToSessionAsync(sessionId);

                PlayerPrefs.SetString(LastSessionIdKey, _currentSession.Id);
                if (!string.IsNullOrEmpty(_currentSession.Code))
                    PlayerPrefs.SetString(LastJoinCodeKey, _currentSession.Code);
                PlayerPrefs.Save();

                _currentSession.PlayerJoined += _ => OnSessionPlayersChanged?.Invoke();
                _currentSession.PlayerLeaving += _ => OnSessionPlayersChanged?.Invoke();

                OnClientConnectResult?.Invoke("Reconnected! Waiting for the host...", true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkLobbyController] Reconnect error: {e.Message}");

                if (e.Message.Contains("not a member"))
                {
                    PlayerPrefs.DeleteKey(LastSessionIdKey);
                    PlayerPrefs.Save();

                    string joinCode = GetSavedJoinCode();
                    _sessionOperationInProgress = false;

                    if (!string.IsNullOrEmpty(joinCode))
                    {
                        await JoinSession(joinCode);
                        return;
                    }
                }

                OnClientConnectResult?.Invoke($"Error: {e.Message}", false);
                ClearSavedSession();
            }
            finally
            {
                _sessionOperationInProgress = false;
            }
        }

        // --- COMMON ---------------------------------------------------------

        private async System.Threading.Tasks.Task LeaveCurrentSession()
        {
            if (_currentSession != null)
            {
                try { await _currentSession.LeaveAsync(); }
                catch (Exception e) { Debug.LogWarning($"[NetworkLobbyController] Session leave error: {e.Message}"); }
                _currentSession = null;
            }
            IsHostSessionActive = false;
        }

        private async Task ShutdownNetworkManagerIfNeeded()
        {
            if (!IsListening) return;
            NetworkManager.Singleton.Shutdown();
            while (NetworkManager.Singleton.ShutdownInProgress)
                await Task.Yield();
        }

        private string GetOrCreateIdentitySecret()
        {
            string secret = PlayerPrefs.GetString(IdentitySecretKey, "");
            if (string.IsNullOrEmpty(secret))
            {
                secret = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(IdentitySecretKey, secret);
                PlayerPrefs.Save();
            }
            return secret;
        }
    }
}