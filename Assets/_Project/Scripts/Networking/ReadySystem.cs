// ReadySystem.cs - GridEmpire.Networking namespace, Networking mappa
using GridEmpire.Networking;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace GridEmpire.Networking
{
    public class ReadySystem : NetworkBehaviour
    {
        public static ReadySystem Instance { get; private set; }

        public static event System.Action OnGameStart;
        public static event System.Action<int, int> OnReadyCountChanged;

        private readonly HashSet<ulong> _readyClients = new HashSet<ulong>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ClientReadyServerRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (_readyClients.Contains(clientId)) return;

            _readyClients.Add(clientId);
            Debug.Log($"[ReadySystem] Client ready: {clientId}, total: {_readyClients.Count}");

            int expectedHumans = GlobalNetworkSettings.Instance.TotalPlayers.Value
                               - GlobalNetworkSettings.Instance.TotalAIBots.Value;

            UpdateLoadingScreenClientRpc(_readyClients.Count, expectedHumans);

            if (_readyClients.Count >= expectedHumans)
            {
                Debug.Log("[ReadySystem] All clients ready, game starting in 3 seconds.");
                StartCoroutine(DelayedGameStart());
            }
        }

        private IEnumerator DelayedGameStart()
        {
            yield return new WaitForSeconds(0f);
            StartGameClientRpc();
        }

        [ClientRpc]
        private void UpdateLoadingScreenClientRpc(int readyCount, int totalCount)
        {
            OnReadyCountChanged?.Invoke(readyCount, totalCount);
        }

        [ClientRpc]
        private void StartGameClientRpc()
        {
            Debug.Log("[ReadySystem] Game Start signal received!");
            OnGameStart?.Invoke();
        }
    }
}