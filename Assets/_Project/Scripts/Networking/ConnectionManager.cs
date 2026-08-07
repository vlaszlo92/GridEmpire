using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

namespace GridEmpire.Networking
{
    public class ConnectionManager : NetworkBehaviour
    {
        public static ConnectionManager Instance { get; private set; }

        private int _assignedHumanCount = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;

            RegisterLocalPlayer();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitAuthIdServerRpc(string authId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            int playerId = ResolvePlayerId(authId, clientId);
            Debug.Log($"[ConnectionManager] clientId={clientId} → authId={authId} → playerId={playerId}");
        }

        public void RegisterLocalPlayer()
        {
            if (AuthenticationService.Instance.IsSignedIn)
            {
                SubmitAuthIdServerRpc(AuthenticationService.Instance.PlayerId);
            }
            else
            {
                Debug.LogError("[ConnectionManager] A kliens nincs bejelentkezve UGS-be!");
            }
        }

        private int ResolvePlayerId(string authId, ulong clientId)
        {
            var mappings = GlobalNetworkSettings.Instance.PlayerMappings;
            var authIdFixed = new FixedString64Bytes(authId);

            // 1. Kifagyott/Újracsatlakozó játékos azonosítása (Reconnection):
            for (int i = 0; i < mappings.Count; i++)
            {
                if (mappings[i].AuthId.Equals(authIdFixed))
                {
                    var updated = mappings[i];
                    updated.ClientId = clientId; // Frissítjük az új csatlakozási ClientId-t
                    mappings[i] = updated;
                    Debug.Log($"[ConnectionManager] Játékos visszacsatlakozott! AuthId={authId}, PlayerId={updated.PlayerId}, Új ClientId={clientId}");
                    return updated.PlayerId;
                }
            }

            // 2. Új játékos regisztrálása
            int humanCount = GlobalNetworkSettings.Instance.TotalPlayers.Value
                           - GlobalNetworkSettings.Instance.TotalAIBots.Value;

            if (_assignedHumanCount >= humanCount)
            {
                Debug.LogError($"[ConnectionManager] Nincs szabad PlayerId slot! authId={authId}, clientId={clientId}");
                return -1;
            }

            int playerId = _assignedHumanCount;
            _assignedHumanCount++;
            mappings.Add(new PlayerClientMapping { ClientId = clientId, PlayerId = playerId, AuthId = authIdFixed });
            return playerId;
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            Debug.Log($"[ConnectionManager] clientId={clientId} lecsatlakozott. Mapping megőrizve Reconnection-höz.");
        }
    }
}