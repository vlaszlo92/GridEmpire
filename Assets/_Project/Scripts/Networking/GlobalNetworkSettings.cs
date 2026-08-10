using GridEmpire.Core;
using GridEmpire.Shared;
using System;
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
        public static event System.Action OnPlayerLobbyInfosChanged;
        public static event System.Action<int> OnColorRejected;

        public NetworkVariable<FixedString4096Bytes> UnitStatsJson = new NetworkVariable<FixedString4096Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> NetworkMapRadius = new NetworkVariable<int>(9);
        public NetworkVariable<int> TotalPlayers = new NetworkVariable<int>(MaxPlayersLimit);
        public NetworkVariable<int> TotalAIBots = new NetworkVariable<int>(0);
        public NetworkVariable<float> TurnSpeed = new NetworkVariable<float>(1.0f);
        public NetworkVariable<int> ConnectedPlayerCount = new NetworkVariable<int>(0);
        public NetworkVariable<bool> FogOfWarEnabled = new NetworkVariable<bool>(true);
        public NetworkVariable<float> GoldPerTurnPerCell = new NetworkVariable<float>(0.1f);
        public NetworkList<PlayerLobbyInfo> PlayerLobbyInfos;
        public NetworkList<PlayerClientMapping> PlayerMappings;

        public void UpdateSettings(int totalPlayers, int aiBots, int mapRadius, float turnSpeed, float goldPerTurnPerCell)
        {
            if (!IsServer) return;
            TotalPlayers.Value = Mathf.Min(totalPlayers, MaxPlayersLimit);
            TotalAIBots.Value = aiBots;
            NetworkMapRadius.Value = mapRadius;
            TurnSpeed.Value = turnSpeed;
            GoldPerTurnPerCell.Value = goldPerTurnPerCell;
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
            GoldPerTurnPerCell.Value = settings.goldPerTurnPerCell;

            Debug.Log($"[GlobalNetworkSettings] Beallitasok frissitve a Szerveren: MapRadius={NetworkMapRadius.Value}, TotalPlayers={TotalPlayers.Value}, AIBots={TotalAIBots.Value}");

            RequestReRegisterClientRpc();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            PlayerMappings = new NetworkList<PlayerClientMapping>();
            PlayerLobbyInfos = new NetworkList<PlayerLobbyInfo>();

            TotalPlayers.Value = MaxPlayersLimit;
            TotalAIBots.Value = 0;
        }

        public override void OnNetworkSpawn()
        {
            UnitStatsJson.OnValueChanged += OnUnitStatsJsonChanged;
            if (!IsServer && !string.IsNullOrEmpty(UnitStatsJson.Value.ToString()))
                ApplyUnitStats(null, UnitStatsJson.Value.ToString());

            PlayerLobbyInfos.OnListChanged += _ => OnPlayerLobbyInfosChanged?.Invoke();

            if (IsServer)
            {
                PlayerMappings.OnListChanged += HandlePlayerMappingsChangedForLobbyInfo;
                // Ha idaig mar volt mapping (pl. a host sajat maga), azokra is pot kell.
                foreach (var mapping in PlayerMappings)
                    RegisterLobbyInfoIfMissing(mapping.PlayerId);
            }
        }


        private void RegisterLobbyInfoIfMissing(int playerId)
        {
            if (FindLobbyInfoIndex(playerId) != -1) return;
            PlayerLobbyInfos.Add(new PlayerLobbyInfo { PlayerId = playerId, Name = default, ColorIndex = -1 });
            Debug.Log($"[GlobalNetworkSettings] Lobby info letrehozva playerId={playerId}");
        }

        public override void OnNetworkDespawn()
        {
            UnitStatsJson.OnValueChanged -= OnUnitStatsJsonChanged;
            if (IsServer && PlayerMappings != null)
                PlayerMappings.OnListChanged -= HandlePlayerMappingsChangedForLobbyInfo;
        }

        private void HandlePlayerMappingsChangedForLobbyInfo(NetworkListEvent<PlayerClientMapping> changeEvent)
        {
            if (changeEvent.Type != NetworkListEvent<PlayerClientMapping>.EventType.Add &&
                changeEvent.Type != NetworkListEvent<PlayerClientMapping>.EventType.Value) return;

            RegisterLobbyInfoIfMissing(changeEvent.Value.PlayerId);
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

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestNameServerRpc(string name, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            int playerId = GetPlayerIdForClient(clientId);
            Debug.Log($"[GlobalNetworkSettings] RequestNameServerRpc erkezett. clientId={clientId}, playerId={playerId}, name='{name}'");
            if (playerId == -1) return; // meg nincs feloldva a mapping

            string clean = string.IsNullOrWhiteSpace(name) ? "" : name.Trim();
            if (clean.Length > 28) clean = clean.Substring(0, 28);
            var fixedName = new FixedString32Bytes(clean);

            int idx = FindLobbyInfoIndex(playerId);
            if (idx == -1)
                PlayerLobbyInfos.Add(new PlayerLobbyInfo { PlayerId = playerId, Name = fixedName, ColorIndex = -1 });
            else
            {
                var info = PlayerLobbyInfos[idx];
                info.Name = fixedName;
                PlayerLobbyInfos[idx] = info;
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestColorServerRpc(int colorIndex, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            int playerId = GetPlayerIdForClient(clientId);
            Debug.Log($"[GlobalNetworkSettings] RequestColorServerRpc erkezett. clientId={clientId}, playerId={playerId}, colorIndex={colorIndex}");
            if (playerId == -1) return;

            if (colorIndex < 0 || colorIndex >= PredefinedColors.Colors.Length) return;

            if (IsColorTaken(colorIndex, playerId))
            {
                Debug.Log($"[GlobalNetworkSettings] Szin elutasitva. playerId={playerId}, colorIndex={colorIndex}");
                ColorRejectedClientRpc(colorIndex, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                });
                return;
            }

            int idx = FindLobbyInfoIndex(playerId);
            if (idx == -1)
                PlayerLobbyInfos.Add(new PlayerLobbyInfo { PlayerId = playerId, Name = default, ColorIndex = colorIndex });
            else
            {
                var info = PlayerLobbyInfos[idx];
                info.ColorIndex = colorIndex;
                PlayerLobbyInfos[idx] = info;
            }
        }

        [ClientRpc]
        private void ColorRejectedClientRpc(int colorIndex, ClientRpcParams clientRpcParams = default)
        {
            OnColorRejected?.Invoke(colorIndex);
        }

        private int FindLobbyInfoIndex(int playerId)
        {
            for (int i = 0; i < PlayerLobbyInfos.Count; i++)
                if (PlayerLobbyInfos[i].PlayerId == playerId) return i;
            return -1;
        }

        public bool IsColorTaken(int colorIndex, int exceptPlayerId)
        {
            foreach (var info in PlayerLobbyInfos)
                if (info.PlayerId != exceptPlayerId && info.ColorIndex == colorIndex) return true;
            return false;
        }

        /// <summary>Szerver hívja Start Game előtt: akinek nincs színe, random szabad (vagy ha nincs szabad, random) színt kap.</summary>
        public void AssignMissingRandomColors()
        {
            if (!IsServer) return;

            var used = new HashSet<int>();
            foreach (var info in PlayerLobbyInfos)
                if (info.ColorIndex != -1) used.Add(info.ColorIndex);

            for (int i = 0; i < PlayerLobbyInfos.Count; i++)
            {
                if (PlayerLobbyInfos[i].ColorIndex != -1) continue;

                var free = new List<int>();
                for (int c = 0; c < PredefinedColors.Colors.Length; c++)
                    if (!used.Contains(c)) free.Add(c);

                int chosen = free.Count > 0
                    ? free[UnityEngine.Random.Range(0, free.Count)]
                    : UnityEngine.Random.Range(0, PredefinedColors.Colors.Length);

                var info = PlayerLobbyInfos[i];
                info.ColorIndex = chosen;
                PlayerLobbyInfos[i] = info;
                used.Add(chosen);
            }
        }

        public string GetPlayerName(int playerId)
        {
            int idx = FindLobbyInfoIndex(playerId);
            if (idx != -1 && !PlayerLobbyInfos[idx].Name.IsEmpty) return PlayerLobbyInfos[idx].Name.ToString();
            return $"Player {playerId}";
        }

        public Color GetPlayerColor(int playerId)
        {
            int idx = FindLobbyInfoIndex(playerId);
            if (idx != -1 && PlayerLobbyInfos[idx].ColorIndex >= 0 && PlayerLobbyInfos[idx].ColorIndex < PredefinedColors.Colors.Length)
                return PredefinedColors.Colors[PlayerLobbyInfos[idx].ColorIndex];
            return Color.white; //TODO: first available color or default color
        }
    }
}