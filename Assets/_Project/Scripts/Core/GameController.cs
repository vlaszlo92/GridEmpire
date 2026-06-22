using GridEmpire.AI;
using GridEmpire.Networking;
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

        [Header("Manager Prefabs")]
        [SerializeField] private GameObject gridManagerPrefab;
        [SerializeField] private GameObject turnManagerPrefab;

        private Dictionary<int, UnitData> _unitDataRegistry = new Dictionary<int, UnitData>();
        private bool _serverInitStarted = false;

        [Header("Game Settings")]
        [Range(2, 6)][SerializeField] private int playerCount = 2;
        [SerializeField] private int aiCount = 0;
        [SerializeField] private Color[] playerColors;
        [SerializeField] private GameObject aiPrefab;
        [SerializeField] private GameObject playerSpawnerPrefab;

        private NetworkList<PlayerData> _networkPlayers;
        [SerializeField] private List<PlayerProfile> _players = new List<PlayerProfile>();
        public IReadOnlyList<PlayerProfile> Players => _players;

        private Dictionary<int, IUnit> _unitRegistry = new Dictionary<int, IUnit>();
        private Dictionary<int, ISpawner> _spawnerRegistry = new Dictionary<int, ISpawner>();
        private int _nextUnitId = 1000;
        private int _nextHumanPlayerId = 0;

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
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                StartCoroutine(ServerInitChain());
            }
            else
            {
                _networkPlayers.OnListChanged += HandleNetworkListChanged;
                GlobalNetworkSettings.Instance.PlayerMappings.OnListChanged += HandleMappingsChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            else
            {
                if (GlobalNetworkSettings.Instance != null)
                    GlobalNetworkSettings.Instance.PlayerMappings.OnListChanged -= HandleMappingsChanged;
            }
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

        // ─── SZERVER INIT LÁNC ───────────────────────────────────────────────────────

        private IEnumerator ServerInitChain()
        {
            // 1. Várunk amíg GlobalNetworkSettings kész
            yield return new WaitUntil(() =>
                GlobalNetworkSettings.Instance != null &&
                GlobalNetworkSettings.Instance.NetworkMapRadius.Value > 0);

            // 2. Settings és játékosok
            LoadSettings();
            InitializePlayers();

            // 3. GridManager spawn és várunk amíg kész
            GameObject gmObj = Instantiate(gridManagerPrefab);
            var gridManager = gmObj.GetComponent<GridManager>();
            gridManager.GenerateGrid(GlobalNetworkSettings.Instance.NetworkMapRadius.Value);
            gmObj.GetComponent<NetworkObject>().Spawn();

            yield return new WaitUntil(() => GridManager.Instance != null && GridManager.Instance.IsReady);
            Debug.Log("[GameController] GridManager kész.");

            // 4. BaseCell beállítás minden játékosnak
            AssignBaseCells(GridManager.Instance);
            Debug.Log("[GameController] BaseCells beállítva.");

            // 5. FogOfWar a host playernek
            IsDebugMode = !GlobalNetworkSettings.Instance.FogOfWarEnabled.Value;
            var hostPlayer = GetPlayerById(0);
            if (hostPlayer != null)
                GridManager.Instance.UpdateFogOfWar(hostPlayer.Id);

            // 6. Spawnerek létrehozása
            SetupSpawners();
            Debug.Log("[GameController] Spawnerek kész.");

            // 7. TurnManager spawn
            GameObject tmObj = Instantiate(turnManagerPrefab);
            tmObj.GetComponent<NetworkObject>().Spawn();
            Debug.Log("[GameController] TurnManager kész.");

            // 8. Host ready jel
            Debug.Log("[GameController] Host ready jel küldése.");
            ReadySystem.Instance?.ClientReadyServerRpc(NetworkManager.Singleton.LocalClientId);
        }

        private void InitializePlayers()
        {
            _players.Clear();
            _networkPlayers.Clear();

            int pCount = GlobalNetworkSettings.Instance.TotalPlayers.Value;
            int aCount = GlobalNetworkSettings.Instance.TotalAIBots.Value;

            for (int i = 0; i < pCount; i++)
            {
                bool isAi = i >= (pCount - aCount);
                bool isLocal = (i == 0 && !isAi);
                string pName = isAi ? $"AI {i}" : $"Player {i}";
                Color pColor = i < playerColors.Length ? playerColors[i] : Color.white;

                _networkPlayers.Add(new PlayerData { Id = i, Color = pColor, IsAi = isAi });
                _players.Add(new PlayerProfile(i, pName, pColor, isAi, isLocal, null));
            }

            Debug.Log($"[GameController] InitializePlayers: {_players.Count} játékos.");
        }

        private void AssignBaseCells(GridManager gridManager)
        {
            int count = _players.Count;
            int radius = GlobalNetworkSettings.Instance.NetworkMapRadius.Value;

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
            SyncBaseCellsClientRpc(_players.Select(p => p.Id).ToArray(), _players.Select(p => p.BaseCell?.Id ?? -1).ToArray());
        }

        [ClientRpc]
        private void SyncBaseCellsClientRpc(int[] playerIds, int[] cellIds)
        {
            for (int i = 0; i < playerIds.Length; i++)
            {
                var player = GetPlayerById(playerIds[i]);
                var cell = GridManager.Instance?.GetCellById(cellIds[i]);
                if (player != null && cell != null)
                {
                    cell.OwnerId = player.Id;
                    cell.IsBase = true;
                    cell.SetInfluence(player.Id, 1.0f);
                    player.BaseCell = cell;
                    GridManager.Instance?.RefreshCell(cell);
                    Debug.Log($"[Client] BaseCell szinkronizálva: player={player.Id}, cell={cell.Id}");
                }
            }

            var localPlayer = GetLocalPlayer();
            if (localPlayer != null)
                GridManager.Instance?.UpdateFogOfWar(localPlayer.Id);
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
                        Debug.LogError($"{aiPrefab.name} prefabon nincs NetworkObject!");
                    var aiScript = aiObj.GetComponent<SimpleAI>();
                    if (aiScript != null) aiScript.Initialize(i);
                    else aiObj.SendMessage("Initialize", i, SendMessageOptions.DontRequireReceiver);
                }
                else
                {
                    GlobalNetworkSettings.Instance.AddMapping((ulong)i, i);
                    GameObject spawnerObj = Instantiate(playerSpawnerPrefab);
                    spawnerObj.name = $"PlayerSpawner_{i}";
                    var spawner = spawnerObj.GetComponent<ISpawner>();
                    spawner?.SetNetworkOwnerId(i);
                    if (spawnerObj.TryGetComponent<NetworkObject>(out var netObj))
                        netObj.Spawn();
                    else
                        Debug.LogError("PlayerSpawner prefabon nincs NetworkObject!");
                    spawner?.Initialize(i);
                }
            }
        }

        // ─── KLIENS INIT LÁNC ────────────────────────────────────────────────────────

        private void HandleMappingsChanged(NetworkListEvent<PlayerClientMapping> changeEvent)
        {
            ulong myId = NetworkManager.Singleton.LocalClientId;
            int playerId = GlobalNetworkSettings.Instance.GetPlayerIdForClient(myId);

            if (playerId != -1)
            {
                Debug.Log($"[Client] Mapping megérkezett: clientId={myId} → playerId={playerId}");
                SyncLocalPlayersFromNetwork();
                StartCoroutine(ClientInitChain());
            }
        }

        private IEnumerator ClientInitChain()
        {
            // 1. Várunk amíg GridManager spawn és grid kész
            yield return new WaitUntil(() =>
                GridManager.Instance != null && GridManager.Instance.IsReady);
            Debug.Log("[Client] GridManager kész.");

            // 2. Várunk amíg a local player BaseCell-je megérkezik
            yield return new WaitUntil(() => GetLocalPlayer()?.BaseCell != null);
            Debug.Log($"[Client] BaseCell kész: {GetLocalPlayer().BaseCell.Id}");

            // 3. Kamera és UI aktiválás
            OnLocalPlayerReady?.Invoke();
            Debug.Log("[Client] OnLocalPlayerReady elküldve.");

            // 4. Várunk egy framet hogy minden komponens reagáljon
            yield return null;

            // 5. Ready jel
            Debug.Log("[Client] Ready jel küldése.");
            ReadySystem.Instance?.ClientReadyServerRpc(NetworkManager.Singleton.LocalClientId);
        }

        // ─── KLIENS SZINKRON ─────────────────────────────────────────────────────────

        private void OnClientConnected(ulong clientId)
        {
            StartCoroutine(RegisterClientWhenReady(clientId));
        }

        private IEnumerator RegisterClientWhenReady(ulong clientId)
        {
            yield return new WaitUntil(() =>
                GlobalNetworkSettings.Instance != null &&
                GlobalNetworkSettings.Instance.TotalPlayers.Value > 0);

            int humanCount = GlobalNetworkSettings.Instance.TotalPlayers.Value
                           - GlobalNetworkSettings.Instance.TotalAIBots.Value;

            if (_nextHumanPlayerId < humanCount)
            {
                GlobalNetworkSettings.Instance.AddMapping(clientId, _nextHumanPlayerId);
                _nextHumanPlayerId++;
            }
        }

        private void HandleNetworkListChanged(NetworkListEvent<PlayerData> changeEvent)
        {
            SyncLocalPlayersFromNetwork();
        }

        private void SyncLocalPlayersFromNetwork()
        {
            int myPlayerId = GlobalNetworkSettings.Instance.GetPlayerIdForClient(
                NetworkManager.Singleton.LocalClientId);

            var oldPlayers = _players.ToDictionary(p => p.Id);
            _players.Clear();

            foreach (var data in _networkPlayers)
            {
                bool isLocal = !data.IsAi && data.Id == myPlayerId;
                string displayName = data.IsAi ? $"AI {data.Id}" : $"Player {data.Id}";
                var newProfile = new PlayerProfile(data.Id, displayName, data.Color, data.IsAi, isLocal, null);

                if (oldPlayers.TryGetValue(data.Id, out var old) && old.BaseCell != null)
                    newProfile.BaseCell = old.BaseCell;

                _players.Add(newProfile);
            }

            Debug.Log($"[GameController] SyncLocalPlayers: {_players.Count} játékos, localId={myPlayerId}");
        }

        // ─── GAZDASÁG ────────────────────────────────────────────────────────────────
        private void ProcessEconomy()
        {
            if (!IsServer) return;
            foreach (var player in _players.Where(p => p.IsAlive))
                player.AddGold(player.GoldIncome);

            // Szinkronizál minden kliensre
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

        private void LoadSettings()
        {
            GameSettings settings = GameSettings.Load();
            playerCount = settings.totalPlayers;
            aiCount = settings.aiBots;
        }

        // ─── REGISTRY ────────────────────────────────────────────────────────────────

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
            Debug.LogError($"[GameController] UnitData nem található: {index}");
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
            }
        }
    }
}