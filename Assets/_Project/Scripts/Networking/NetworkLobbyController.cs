using System;
using System.Collections;
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
    /// A lobby halozati retege: UGS session kezeles, NetworkManager Host/Client inditas,
    /// connection approval, disconnect figyeles, GlobalNetworkSettings szinkronizalas.
    /// A MainMenuController kizarolag ezen keresztul nyul halozati funkciokhoz.
    /// </summary>
    public class NetworkLobbyController : MonoBehaviour
    {
        public static NetworkLobbyController Instance { get; private set; }

        public event Action OnServicesInitialized;
        public event Action<string> OnHostSessionReady;      // join code
        public event Action<string> OnHostSessionFailed;     // hibauzenet
        public event Action OnSessionPlayersChanged;         // host: jatekos csatlakozott/kilepett
        public event Action<string, bool> OnClientConnectResult; // (uzenet, siker)
        public event Action OnHostConnectionLost;             // kliens leszakadt a hosttol

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
        private bool _gameStarting;
        private bool _disconnectCallbackSubscribed;

        private bool _sessionOperationInProgress;
        public bool IsSessionOperationInProgress => _sessionOperationInProgress;

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
                Debug.Log("[NetworkLobbyController] UGS inicializalva es bejelentkezve.");
                OnServicesInitialized?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkLobbyController] UGS inicializalas sikertelen: {e.Message}");
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

            Debug.Log("[NetworkLobbyController] Kapcsolat megszakadt a hosttal.");
            OnHostConnectionLost?.Invoke();
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

                var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
                _currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                IsHostSessionActive = true;

                _currentSession.PlayerJoined += _ => OnSessionPlayersChanged?.Invoke();
                _currentSession.PlayerLeaving += _ => OnSessionPlayersChanged?.Invoke();

                OnHostSessionReady?.Invoke(_currentSession.Code);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkLobbyController] Session hiba: {e.Message}");
                OnHostSessionFailed?.Invoke(e.Message);
            }
            finally
            {
                _sessionOperationInProgress = false;
            }
        }

        public void SyncSettingsToClients(int totalPlayers, int aiBots, int mapRadius, float turnSpeed)
        {
            if (IsHost && GlobalNetworkSettings.Instance != null)
                GlobalNetworkSettings.Instance.UpdateSettings(totalPlayers, aiBots, mapRadius, turnSpeed);
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
            _gameStarting = true;

            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.ConnectionApprovalCallback = ApproveConnection;

            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.StartHost();

            if (GlobalNetworkSettings.Instance != null)
                GlobalNetworkSettings.Instance.InitializeFromSettings(settings);
            else
                Debug.LogError("[NetworkLobbyController] GlobalNetworkSettings nem talalhato a jelenetben!");

            NetworkDebugDump.DumpServerState(settings, sceneName, expectedHumans);
            GlobalNetworkSettings.Instance?.TriggerDebugDumpClientRpc();

            StartCoroutine(LoadGameSceneRoutine(sceneName, expectedHumans));
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = true;
            response.CreatePlayerObject = false;
        }

        private IEnumerator LoadGameSceneRoutine(string sceneName, int expectedHumans)
        {
            while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
                yield return null;

            while (NetworkManager.Singleton.ConnectedClientsIds.Count < expectedHumans)
                yield return new WaitForSeconds(0.5f);

            Debug.Log("[NetworkLobbyController] Minden kliens csatlakozott, scene load indul.");
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
        }

        // --- CLIENT ---------------------------------------------------------

        public async Task JoinSession(string joinCode)
        {
            if (!ServicesReady || _sessionOperationInProgress) return;
            _sessionOperationInProgress = true;
            try
            {
                try
                {
                    _currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
                }
                catch (Exception joinEx) when (joinEx.Message.Contains("already a member"))
                {
                    Debug.LogWarning($"[NetworkLobbyController] Mar tagja egy session-nek. joinCode={joinCode}");
                    throw;
                }

                await ShutdownNetworkManagerIfNeeded();
                NetworkManager.Singleton.StartClient();

                OnClientConnectResult?.Invoke("Csatlakozva! Varakozas a hostra...", true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkLobbyController] Session hiba: {e.Message}");
                OnClientConnectResult?.Invoke($"Hiba: {e.Message}", false);
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
        }


        // --- COMMON ---------------------------------------------------------

        private async System.Threading.Tasks.Task LeaveCurrentSession()
        {
            if (_currentSession != null)
            {
                try { await _currentSession.LeaveAsync(); }
                catch (Exception e) { Debug.LogWarning($"[NetworkLobbyController] Session elhagyas: {e.Message}"); }
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
    }
}