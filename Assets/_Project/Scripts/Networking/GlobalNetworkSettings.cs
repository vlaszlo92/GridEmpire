using GridEmpire.Shared;
using Unity.Netcode;
using UnityEngine;

namespace GridEmpire.Networking
{
    public class GlobalNetworkSettings : NetworkBehaviour
    {
        public static GlobalNetworkSettings Instance { get; private set; }

        public NetworkVariable<int> NetworkMapRadius = new NetworkVariable<int>(9);
        public NetworkVariable<int> TotalPlayers = new NetworkVariable<int>(6);
        public NetworkVariable<int> TotalAIBots = new NetworkVariable<int>(0);
        public NetworkVariable<float> TurnSpeed = new NetworkVariable<float>(1.0f);
        public NetworkList<PlayerClientMapping> PlayerMappings;
        public NetworkVariable<int> ConnectedPlayerCount = new NetworkVariable<int>(0);
        public NetworkVariable<bool> FogOfWarEnabled = new NetworkVariable<bool>(true);

        public const int MaxPlayersLimit = 6;

        public void UpdateSettings(int totalPlayers, int aiBots, int mapRadius, float turnSpeed)
        {
            if (!IsServer) return;
            TotalPlayers.Value = Mathf.Min(totalPlayers, MaxPlayersLimit);
            TotalAIBots.Value = aiBots;
            NetworkMapRadius.Value = mapRadius;
            TurnSpeed.Value = turnSpeed;
        }

        public void InitializeFromSettings(GameSettings settings)
        {
            if (!IsServer) return;

            NetworkMapRadius.Value = settings.mapRadius;
            // FIX: Nem MaxPlayersLimit-re állítjuk, hanem a beállításokban szereplő játékosszámra!
            TotalPlayers.Value = Mathf.Min(settings.totalPlayers, MaxPlayersLimit);
            TotalAIBots.Value = settings.aiBots;
            TurnSpeed.Value = settings.turnSpeedMultiplier;
            FogOfWarEnabled.Value = settings.fogOfWarEnabled;

            Debug.Log($"[GlobalNetworkSettings] Beállítások frissítve a Szerveren: MapRadius={NetworkMapRadius.Value}, TotalPlayers={TotalPlayers.Value}, AIBots={TotalAIBots.Value}");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            PlayerMappings = new NetworkList<PlayerClientMapping>();

            TotalPlayers.Value = MaxPlayersLimit;
            TotalAIBots.Value = 0;
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[GlobalNetworkSettings] OnNetworkSpawn. InstanceID={GetInstanceID()}, IsServer={IsServer}, TotalPlayers={TotalPlayers.Value}");
        }

        public int GetPlayerIdForClient(ulong clientId)
        {
            if (PlayerMappings == null) return -1;

            foreach (var mapping in PlayerMappings)
            {
                if (mapping.ClientId == clientId)
                {
                    return mapping.PlayerId;
                }
            }
            return -1;
        }

        public void AddMapping(ulong clientId, int playerId)
        {
            if (!IsServer) return;
            PlayerMappings.Add(new PlayerClientMapping { ClientId = clientId, PlayerId = playerId });
        }
    }
}