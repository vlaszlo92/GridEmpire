using GridEmpire.Core;
using GridEmpire.Shared;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GridEmpire.Networking
{
    public class GlobalNetworkSettings : NetworkBehaviour
    {
        public static GlobalNetworkSettings Instance { get; private set; }

        public const int MaxPlayersLimit = 6;

        [Header("Unit Stats")]
        [SerializeField] private List<UnitData> trackedUnitData;

        public static event System.Action<List<(int unitIndex, string fieldName)>> OnUnitStatsFieldsChanged;
        public static event System.Action OnUnitStatsSynced;
        public NetworkVariable<FixedString4096Bytes> UnitStatsJson =
    new NetworkVariable<FixedString4096Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


        public NetworkVariable<int> NetworkMapRadius = new NetworkVariable<int>(9);
        public NetworkVariable<int> TotalPlayers = new NetworkVariable<int>(MaxPlayersLimit);
        public NetworkVariable<int> TotalAIBots = new NetworkVariable<int>(0);
        public NetworkVariable<float> TurnSpeed = new NetworkVariable<float>(1.0f);
        public NetworkList<PlayerClientMapping> PlayerMappings;
        public NetworkVariable<int> ConnectedPlayerCount = new NetworkVariable<int>(0);
        public NetworkVariable<bool> FogOfWarEnabled = new NetworkVariable<bool>(true);


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
            PlayerMappings.Clear();

            NetworkMapRadius.Value = settings.mapRadius;
            TotalPlayers.Value = Mathf.Min(settings.totalPlayers, MaxPlayersLimit);
            TotalAIBots.Value = settings.aiBots;
            TurnSpeed.Value = settings.turnSpeedMultiplier;
            FogOfWarEnabled.Value = settings.fogOfWarEnabled;

            Debug.Log($"[GlobalNetworkSettings] Beállítások frissítve a Szerveren: MapRadius={NetworkMapRadius.Value}, TotalPlayers={TotalPlayers.Value}, AIBots={TotalAIBots.Value}");

            RequestReRegisterClientRpc();
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

            UnitStatsJson.OnValueChanged += OnUnitStatsJsonChanged;
            if (!IsServer && !string.IsNullOrEmpty(UnitStatsJson.Value.ToString()))
                ApplyUnitStats(null, UnitStatsJson.Value.ToString());
        }

        public override void OnNetworkDespawn()
        {
            UnitStatsJson.OnValueChanged -= OnUnitStatsJsonChanged;
        }

        public void SyncUnitStatsToClients(IEnumerable<UnitData> unitDataList)
        {
            if (!IsServer) return;
            string json = JsonUtility.ToJson(UnitStatsSnapshotUtil.Collect(unitDataList));
            UnitStatsJson.Value = json;
        }


        private void OnUnitStatsJsonChanged(FixedString4096Bytes previous, FixedString4096Bytes current)
        {
            if (IsServer) return;
            ApplyUnitStats(previous.ToString(), current.ToString());
        }

        [ClientRpc]
        private void RequestReRegisterClientRpc()
        {
            ConnectionManager.Instance?.RegisterLocalPlayer();
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

        [ClientRpc]
        public void TriggerDebugDumpClientRpc()
        {
            NetworkDebugDump.DumpClientState();
        }

        private void ApplyUnitStats(string previousJson, string currentJson)
        {
            var collection = JsonUtility.FromJson<UnitStatsCollection>(currentJson);
            UnitStatsSnapshotUtil.Apply(trackedUnitData, collection);
            OnUnitStatsSynced?.Invoke();

            if (!string.IsNullOrEmpty(previousJson))
            {
                var previousCollection = JsonUtility.FromJson<UnitStatsCollection>(previousJson);
                var changed = UnitStatsSnapshotUtil.Diff(previousCollection, collection);
                if (changed.Count > 0)
                    OnUnitStatsFieldsChanged?.Invoke(changed);
            }
        }
    }
}