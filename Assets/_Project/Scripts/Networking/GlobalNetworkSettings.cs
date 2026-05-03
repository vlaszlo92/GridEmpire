using GridEmpire.Shared;
using Unity.Netcode;
using UnityEngine;

namespace GridEmpire.Networking
{
    public class GlobalNetworkSettings : NetworkBehaviour
    {
        public static GlobalNetworkSettings Instance { get; private set; }

        public NetworkVariable<int> NetworkMapRadius = new NetworkVariable<int>(15);
        public NetworkVariable<int> TotalPlayers = new NetworkVariable<int>(2);
        public NetworkVariable<int> TotalAIBots = new NetworkVariable<int>(1);
        public NetworkVariable<float> TurnSpeed = new NetworkVariable<float>(1.0f);
        public NetworkList<PlayerClientMapping> PlayerMappings;
        public NetworkVariable<int> ConnectedPlayerCount = new NetworkVariable<int>(0);

        public void UpdateSettings(int totalPlayers, int aiBots, int mapRadius, float turnSpeed)
        {
            if (!IsServer) return;
            TotalPlayers.Value = totalPlayers;
            TotalAIBots.Value = aiBots;
            NetworkMapRadius.Value = mapRadius;
            TurnSpeed.Value = turnSpeed;
        }
        public void InitializeFromSettings(GameSettings settings)
        {
            if (!IsServer) return;
            NetworkMapRadius.Value = settings.mapRadius;
            TotalPlayers.Value = settings.totalPlayers;
            TotalAIBots.Value = settings.aiBots;
            TurnSpeed.Value = settings.turnSpeedMultiplier;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            PlayerMappings = new NetworkList<PlayerClientMapping>();
        }

        public void AddMapping(ulong clientId, int playerId)
        {
            if (!IsServer) return;
            PlayerMappings.Add(new PlayerClientMapping { ClientId = clientId, PlayerId = playerId });
        }

        public int GetPlayerIdForClient(ulong clientId)
        {
            foreach (var m in PlayerMappings)
                if (m.ClientId == clientId) return m.PlayerId;

            Debug.LogWarning($"[GNS] Nincs mapping clientId {clientId}-hoz! Összes mapping:");
            foreach (var m in PlayerMappings)
                Debug.Log($"  ClientId={m.ClientId} → PlayerId={m.PlayerId}");
            return -1;
        }
    }
}