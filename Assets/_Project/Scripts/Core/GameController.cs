using GridEmpire.AI;
using GridEmpire.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace GridEmpire.Core
{
    public class GameController : NetworkBehaviour
    {
        public static GameController Instance { get; private set; }
        public static event System.Action OnLocalPlayerReady;

        /// <summary>Fires when the local player is ready.
        /// The Networking layer will call this once the local clientId → playerId mapping is resolved.</summary>
        public static event System.Action OnLocalInitializationComplete;

        /// <summary>Sets the Networking layer (GameNetworkBridge) to avoid circular assembly references between Core and Networking.
        /// Used on the server side: resolves clientId -> playerId mapping.</summary>
        public static System.Func<ulong, int> ResolvePlayerIdForClient;

        [Header("Manager Prefabs")]
        [SerializeField] private GameObject gridManagerPrefab;
        [SerializeField] private GameObject turnManagerPrefab;

        private Dictionary<int, UnitData> _unitDataRegistry = new Dictionary<int, UnitData>();

        [Header("Game Settings")]
        [SerializeField] private Color[] playerColors;
        [SerializeField] private GameObject aiPrefab;
        [SerializeField] private GameObject playerSpawnerPrefab;

        private GameSessionConfig _config;
        private int _localPlayerId = -1;
        public int LocalPlayerId => _localPlayerId;
        private bool _clientInitStarted = false;

        private NetworkList<PlayerData> _networkPlayers;
        [SerializeField] private List<PlayerProfile> _players = new List<PlayerProfile>();
        public IReadOnlyList<PlayerProfile> Players => _players;

        private Dictionary<int, IUnit> _unitRegistry = new Dictionary<int, IUnit>();
        private Dictionary<int, ISpawner> _spawnerRegistry = new Dictionary<int, ISpawner>();
        private int _nextUnitId = 1000;

        public bool HasUnitData(int index) => _unitDataRegistry.ContainsKey(index);
        public static System.Action<IUnit> OnUnitSelected;
        public static System.Action OnUnitRemoved;
        public static bool IsDebugMode;

        private IUnit _selectedUnit;
        public IUnit SelectedUnit
        {
            get => _selectedUnit;
            set { _selectedUnit = value; OnUnitSelected?.Invoke(_selectedUnit); }
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
            _networkPlayers = new NetworkList<PlayerData>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Debug.Log($"[HOST Side] Local clientId: {NetworkManager.Singleton.LocalClientId}");
                StartCoroutine(ServerInitChain());
            }
            else
            {
                Debug.Log($"[CLIENT Side] Local clientId: {NetworkManager.Singleton.LocalClientId}");
                _networkPlayers.OnListChanged += HandleNetworkListChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
                _networkPlayers.OnListChanged -= HandleNetworkListChanged;
        }

        private void OnEnable()
        {
            TurnManager.OnTurnCompleted += ProcessEconomy;
            CellData.OnCellOwnerChanged += HandleCellOwnershipChange;
        }

        private void OnDisable()
        {
            TurnManager.OnTurnCompleted -= ProcessEconomy;
            CellData.OnCellOwnerChanged -= HandleCellOwnershipChange;
        }

        // --- SESSION CONFIG / LOCAL PLAYER ID (Networking layer data) -------

        public void SetSessionConfig(GameSessionConfig config) => _config = config;

        public void TrySetLocalPlayerId(int playerId)
        {
            if (playerId < 0 || _clientInitStarted) return;
            _localPlayerId = playerId;
            _clientInitStarted = true;
            Debug.Log($"[Client] Local playerId set: {playerId}");
            SyncLocalPlayersFromNetwork();
            StartCoroutine(ClientInitChain());
        }

        // --- SZERVER INIT LaNC -------------------------------------------------------

        private IEnumerator ServerInitChain()
        {
            // 1. Wait until we receive the session config from the Networking layer
            yield return new WaitUntil(() => _config != null);

            // 2. Initialize players
            InitializePlayers();

            // 3. GridManager spawn and wait until ready
            GameObject gmObj = Instantiate(gridManagerPrefab);
            var gridManager = gmObj.GetComponent<GridManager>();
            gridManager.FogOfWarEnabled = _config.FogOfWarEnabled;
            gridManager.GenerateGrid(_config.MapRadius);
            gmObj.GetComponent<NetworkObject>().Spawn();

            yield return new WaitUntil(() => GridManager.Instance != null && GridManager.Instance.IsReady);
            Debug.Log("[GameController] GridManager ready.");

            // 4. Assign base cells to each player
            AssignBaseCells(GridManager.Instance);
            Debug.Log("[GameController] BaseCells ready.");

            // 5. FogOfWar for the host player
            IsDebugMode = !_config.FogOfWarEnabled;
            var hostPlayer = GetPlayerById(0);
            if (hostPlayer != null)
                GridManager.Instance.UpdateFogOfWar(hostPlayer.Id);

            // 6. Setup spawners
            SetupSpawners();
            Debug.Log("[GameController] Spawners ready.");

            // 7. TurnManager spawn
            GameObject tmObj = Instantiate(turnManagerPrefab);
            tmObj.GetComponent<NetworkObject>().Spawn();
            Debug.Log("[GameController] TurnManager ready.");

            // 8. Server initialization complete – the Networking layer will handle the rest
            Debug.Log("[GameController] Server initialization complete.");
            OnLocalInitializationComplete?.Invoke();
        }

        private void InitializePlayers()
        {
            _players.Clear();
            _networkPlayers.Clear();

            int pCount = _config.TotalPlayers;
            int aCount = _config.TotalAIBots;
            int humanCount = pCount - aCount;

            for (int i = 0; i < pCount; i++)
            {
                bool isAi = i >= humanCount;
                bool isLocal = (i == 0 && !isAi);

                string pName = (!isAi && _config.PlayerNames != null && i < _config.PlayerNames.Length && !string.IsNullOrEmpty(_config.PlayerNames[i]))
                    ? _config.PlayerNames[i]
                    : (isAi ? $"AI {i}" : $"Player {i}");
                Color pColor = _config.PlayerColors[i];

                _networkPlayers.Add(new PlayerData { Id = i, Color = pColor, IsAi = isAi });
                _players.Add(new PlayerProfile(i, pName, pColor, isAi, isLocal, null, _config.GoldPerTurnPerCell));
            }

            Debug.Log($"[GameController] InitializePlayers: {_players.Count} players.");
        }

        private void AssignBaseCells(GridManager gridManager)
        {
            int count = _players.Count;
            int radius = _config.MapRadius;

            Vector2Int[] corners = new Vector2Int[]
            {
                new Vector2Int(0, -radius),
                new Vector2Int(radius, -radius),
                new Vector2Int(radius, 0),
                new Vector2Int(0, radius),
                new Vector2Int(-radius, radius),
                new Vector2Int(-radius, 0)
            };

            int[] indices;
            switch (count)
            {
                case 2: indices = new int[] { 0, 3 }; break;
                case 3: indices = new int[] { 0, 2, 4 }; break;
                case 4: indices = new int[] { 0, 1, 3, 4 }; break;
                case 6: indices = new int[] { 0, 1, 2, 3, 4, 5 }; break;
                default: indices = Enumerable.Range(0, count).ToArray(); break;
            }

            for (int i = 0; i < count; i++)
            {
                var player = _players[i];
                var corner = corners[indices[i]];
                var cell = gridManager.GetCell(corner.x, corner.y);
                if (cell == null)
                {
                    Debug.LogError($"[GameController] AssignBaseCells: cell NULL player={player.Id}");
                    continue;
                }
                cell.OwnerId = player.Id;
                cell.IsBase = true;
                cell.SetInfluence(player.Id, 1.0f);
                player.BaseCell = cell;
                gridManager.RefreshCell(cell);
                Debug.Log($"[GameController] BaseCell: player={player.Id}, cell={cell.Id}");
            }
        }
        // --- GRID STATE SYNC (first connection + reconnect) ---------------

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestGridStateServerRpc(RpcParams rpcParams = default)
        {
            StartCoroutine(SendGridStateWhenReady(rpcParams.Receive.SenderClientId));
        }

        private IEnumerator SendGridStateWhenReady(ulong clientId)
        {
            yield return new WaitUntil(() =>
                _players.Count > 0 &&
                _players.Where(p => !p.IsAI).All(p => p.BaseCell != null));

            int playerId = ResolvePlayerIdForClient?.Invoke(clientId) ?? -1;

            IEnumerable<CellData> cells = GridManager.Instance.GetAllCells();
            if (!IsDebugMode && GridManager.Instance.FogOfWarEnabled && playerId != -1)
            {
                var known = GridManager.Instance.GetVisibleCells(playerId);
                cells = cells.Where(c => c.OwnerId == playerId || known.Contains(c));
            }

            var cellList = cells.ToList();
            int[] ids = cellList.Select(c => c.Id).ToArray();
            int[] owners = cellList.Select(c => c.OwnerId).ToArray();
            bool[] bases = cellList.Select(c => c.IsBase).ToArray();

            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
            SyncGridStateClientRpc(ids, owners, bases, clientRpcParams);
        }

        [ClientRpc]
        private void SyncGridStateClientRpc(int[] cellIds, int[] ownerIds, bool[] isBase, ClientRpcParams clientRpcParams = default)
        {
            StartCoroutine(ApplyGridStateWhenReady(cellIds, ownerIds, isBase));
        }

        private IEnumerator ApplyGridStateWhenReady(int[] cellIds, int[] ownerIds, bool[] isBase)
        {
            yield return new WaitUntil(() => GridManager.Instance != null && GridManager.Instance.IsReady);

            for (int i = 0; i < cellIds.Length; i++)
            {
                var cell = GridManager.Instance.GetCellById(cellIds[i]);
                if (cell == null) continue;

                cell.OwnerId = ownerIds[i];
                cell.IsBase = isBase[i];
                if (ownerIds[i] != -1) cell.SetInfluence(ownerIds[i], 1f);
                GridManager.Instance.RefreshCell(cell);
            }

            foreach (var player in _players)
                player.BaseCell = GridManager.Instance.GetAllCells().FirstOrDefault(c => c.IsBase && c.OwnerId == player.Id);

            var localPlayer = GetLocalPlayer();
            if (localPlayer != null)
                GridManager.Instance.UpdateFogOfWar(localPlayer.Id);

            Debug.Log("[Client] Grid state synchronized.");
        }


        private void SetupSpawners()
        {
            foreach (var profile in _players)
            {
                int i = profile.Id;

                if (profile.IsAI)
                {
                    GameObject aiObj = Instantiate(aiPrefab);
                    aiObj.name = profile.Name;
                    var aiSpawner = aiObj.GetComponent<ISpawner>();
                    aiSpawner?.SetNetworkOwnerId(i);
                    if (aiObj.TryGetComponent<NetworkObject>(out var netObj))
                        netObj.Spawn();
                    else
                        Debug.LogError($"{aiPrefab.name} has no NetworkObject on prefab!");
                    var aiScript = aiObj.GetComponent<SimpleAI>();
                    if (aiScript != null) aiScript.Initialize(i);
                    else aiObj.SendMessage("Initialize", i, SendMessageOptions.DontRequireReceiver);
                }
                else
                {
                    GameObject spawnerObj = Instantiate(playerSpawnerPrefab);
                    spawnerObj.name = $"PlayerSpawner_{i}";
                    var spawner = spawnerObj.GetComponent<ISpawner>();
                    spawner?.SetNetworkOwnerId(i);
                    if (spawnerObj.TryGetComponent<NetworkObject>(out var netObj))
                        netObj.Spawn();
                    else
                        Debug.LogError("PlayerSpawner has no NetworkObject on prefab!");
                    spawner?.Initialize(i);
                }
            }
        }

        // --- CLIENT INIT CHAIN --------------------------------------------------------

        private IEnumerator ClientInitChain()
        {
            yield return new WaitUntil(() =>
                GridManager.Instance != null && GridManager.Instance.IsReady);
            Debug.Log("[Client] GridManager ready.");

            RequestGridStateServerRpc();

            yield return new WaitUntil(() => GetLocalPlayer()?.BaseCell != null);
            Debug.Log($"[Client] BaseCell ready: {GetLocalPlayer().BaseCell.Id}");

            ResyncAllUnitsLocal();

            OnLocalPlayerReady?.Invoke();
            Debug.Log("[Client] OnLocalPlayerReady sent.");

            yield return null;

            Debug.Log("[Client] Client initialization complete.");
            OnLocalInitializationComplete?.Invoke();
        }

        // --- CLIENT SYNC ---------------------------------------------------------

        public void ResyncAllUnitsLocal()
        {
            foreach (var unit in _unitRegistry.Values.ToList())
            {
                if (unit is IUnit unitController)
                {
                    unitController.SyncToAuthoritativeState();
                }
            }
            Debug.Log("[GameController] All units have been resynchronized with the local state.");
        }

        private void HandleNetworkListChanged(NetworkListEvent<PlayerData> changeEvent)
        {
            SyncLocalPlayersFromNetwork();
        }

        private void SyncLocalPlayersFromNetwork()
        {
            var existingById = _players.ToDictionary(p => p.Id);
            var newList = new List<PlayerProfile>();

            foreach (var data in _networkPlayers)
            {
                if (existingById.TryGetValue(data.Id, out var existing))
                {
                    existing.SetLocal(!data.IsAi && data.Id == _localPlayerId);
                    newList.Add(existing);
                }
                else
                {
                    bool isLocal = !data.IsAi && data.Id == _localPlayerId;
                    string displayName = data.IsAi ? $"AI {data.Id}" : $"Player {data.Id}";
                    newList.Add(new PlayerProfile(data.Id, displayName, data.Color, data.IsAi, isLocal, null));
                }
            }

            _players.Clear();
            _players.AddRange(newList);

            Debug.Log($"[GameController] SyncLocalPlayers: {_players.Count} players, localId={_localPlayerId}");
        }


        // --- ECONOMY ----------------------------------------------------------------
        private void ProcessEconomy()
        {
            if (!IsServer) return;
            foreach (var player in _players.Where(p => p.IsAlive))
                player.AddGold(player.GoldIncome);

            var ids = _players.Select(p => p.Id).ToArray();
            var golds = _players.Select(p => p.Gold).ToArray();
            var incomes = _players.Select(p => p.GoldIncome).ToArray();
            SyncEconomyClientRpc(ids, golds, incomes);
        }

        [ClientRpc]
        private void SyncEconomyClientRpc(int[] playerIds, float[] golds, float[] incomes)
        {
            if (IsServer) return;
            for (int i = 0; i < playerIds.Length; i++)
            {
                var player = GetPlayerById(playerIds[i]);
                if (player != null)
                {
                    player.SyncGold(golds[i]);
                    player.SyncIncome(incomes[i]);
                }
            }
        }

        private void HandleCellOwnershipChange(int fromPlayer, int toPlayer)
        {
            GetPlayerById(fromPlayer)?.ChangeOwnedCells(-1);
            GetPlayerById(toPlayer)?.ChangeOwnedCells(+1);
        }

        // --- REGISTRY ----------------------------------------------------------------

        public int GetNextAvailableId() => _nextUnitId++;
        public void RegisterUnit(IUnit unit)
        {
            _unitRegistry[unit.Id] = unit;
            GetPlayerById(unit.OwnerId)?.AddUnit(unit);
        }

        public void UnregisterUnit(int id)
        {
            if (!_unitRegistry.Remove(id, out var unit)) return;
            GetPlayerById(unit.OwnerId)?.RemoveUnit(unit);
        }
        public IUnit GetUnitById(int id) => _unitRegistry.GetValueOrDefault(id);
        public void RegisterSpawner(ISpawner spawner) => _spawnerRegistry[spawner.OwnerId] = spawner;
        public ISpawner GetSpawnerByPlayerId(int id) => _spawnerRegistry.GetValueOrDefault(id);
        public IReadOnlyCollection<IUnit> GetAllUnits() => _unitRegistry.Values;
        public IReadOnlyCollection<IUnit> GetUnitsForPlayer(int playerId) => GetPlayerById(playerId)?.ActiveUnits ?? (IReadOnlyCollection<IUnit>)Array.Empty<IUnit>();

        public void RegisterUnitData(UnitData data)
        {
            if (!_unitDataRegistry.ContainsKey(data.index))
                _unitDataRegistry[data.index] = data;
        }

        public UnitData GetUnitDataByIndex(int index)
        {
            if (_unitDataRegistry.TryGetValue(index, out var data)) return data;
            Debug.LogError($"[GameController] UnitData not found: {index}");
            return null;
        }

        public void RemoveUnit(IUnit unit)
        {
            if (unit == null) return;
            UnregisterUnit(unit.Id);
            if (_selectedUnit?.Id == unit.Id) SelectedUnit = null;
            OnUnitRemoved?.Invoke();
        }

        public void RefreshPlayerIncome(PlayerProfile player) => player?.RecalculateIncome();
        public PlayerProfile GetLocalPlayer() => _players.FirstOrDefault(p => p.IsLocalPlayer);
        public PlayerProfile GetPlayerById(int id) => _players.FirstOrDefault(p => p.Id == id);
        public IReadOnlyList<PlayerProfile> GetPlayers() => Players;
        public void UpdateUnitVisibility(HashSet<CellData> visibleCells, int forPlayerId)
        {
            foreach (var unit in _unitRegistry.Values)
            {
                bool visible = visibleCells == null
                               || unit.OwnerId == forPlayerId
                               || (unit.CurrentCell != null && visibleCells.Contains(unit.CurrentCell));

                unit.SetVisible(visible);
                unit.SetAudioVisible(visible);
            }
        }
    }
}