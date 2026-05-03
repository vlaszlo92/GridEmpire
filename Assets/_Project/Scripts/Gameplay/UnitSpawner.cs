using GridEmpire.Core;
using GridEmpire.Shared;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace GridEmpire.Gameplay
{
    public class UnitSpawner : NetworkBehaviour, ISpawner
    {
        public static System.Action<int, int, CellData> OnRequestUnitSpawn;
        public const int MaxQueueSize = 6;

        [Header("Unit Definitions")]
        [SerializeField] private UnitData axeman;
        [SerializeField] private UnitData spearman;
        [SerializeField] private UnitData cavalry;
        [SerializeField] private UnitData scout;
        [SerializeField] private GridManager gridManager;

        public NetworkVariable<int> NetworkOwnerId = new NetworkVariable<int>(-1);
        private int _pendingOwnerId = -1;

        private int _ownerId = -1;
        private PlayerProfile _ownerProfile;
        private readonly List<QueuedUnit> _myQueue = new List<QueuedUnit>();
        private TurnResolver _resolver;

        public int OwnerId => _ownerId;

        private void Start()
        {
            _resolver = FindFirstObjectByType<TurnResolver>();
            if (_resolver != null) _resolver.RegisterSpawner(this);
        }

        private void OnEnable()
        {
            OnRequestUnitSpawn += HandleSpawnRequest;
            GameController.OnLocalPlayerReady += HandleLocalPlayerReady;
        }

        private void OnDisable()
        {
            OnRequestUnitSpawn -= HandleSpawnRequest;
            GameController.OnLocalPlayerReady -= HandleLocalPlayerReady;
        }

        public override void OnNetworkSpawn()
        {
            if (axeman != null) GameController.Instance.RegisterUnitData(axeman);
            if (spearman != null) GameController.Instance.RegisterUnitData(spearman);
            if (cavalry != null) GameController.Instance.RegisterUnitData(cavalry);
            if (scout != null) GameController.Instance.RegisterUnitData(scout);

            if (IsServer && _pendingOwnerId != -1)
                NetworkOwnerId.Value = _pendingOwnerId;
        }

        public void SetNetworkOwnerId(int playerId) => _pendingOwnerId = playerId;

        public void Initialize(int userId)
        {
            _ownerId = userId;
            _ownerProfile = GameController.Instance?.GetPlayerById(userId);

            if (_resolver == null)
                _resolver = FindFirstObjectByType<TurnResolver>();
            _resolver?.RegisterSpawner(this);
            GameController.Instance?.RegisterSpawner(this);

            if (gridManager == null)
                gridManager = GridManager.Instance;

            if (axeman != null) GameController.Instance?.RegisterUnitData(axeman);
            if (spearman != null) GameController.Instance?.RegisterUnitData(spearman);
            if (cavalry != null) GameController.Instance?.RegisterUnitData(cavalry);
            if (scout != null) GameController.Instance?.RegisterUnitData(scout);

            Debug.Log($"[UnitSpawner] Initialize: owner={_ownerId}, profile={_ownerProfile?.Id.ToString() ?? "NULL"}, grid={gridManager != null}");
        }

        private void HandleLocalPlayerReady()
        {
            if (IsServer) return;
            var local = GameController.Instance?.GetLocalPlayer();
            if (local == null) return;
            StartCoroutine(WaitAndInitializeClient(local.Id));
        }

        private IEnumerator WaitAndInitializeClient(int localPlayerId)
        {
            float t = 0f;
            while (NetworkOwnerId.Value == -1 && t < 5f)
            {
                t += Time.deltaTime;
                yield return null;
            }

            if (NetworkOwnerId.Value != localPlayerId)
                yield break;

            Initialize(localPlayerId);
        }

        public void SetGridManager(GridManager gm)
        {
            gridManager = gm;
        }

        public override void OnDestroy()
        {
            _resolver?.UnregisterSpawner(this);
            base.OnDestroy();
        }

        private void HandleSpawnRequest(int playerId, int unitSlot, CellData targetCell)
        {
            if (_ownerId == -1 || playerId != _ownerId) return;
            if (targetCell == null) targetCell = _ownerProfile?.BaseCell;
            if (IsServer) { UnitData data = SlotToData(unitSlot); if (data != null) RequestUnit(data, targetCell); }
            else SendSpawnRequest(unitSlot, targetCell?.Id ?? -1);
        }

        public bool RequestUnit(UnitData data, CellData targetCell)
        {
            if (_ownerProfile == null) _ownerProfile = GameController.Instance?.GetPlayerById(_ownerId);
            if (_ownerProfile == null || !_ownerProfile.IsAlive) return false;
            if (_myQueue.Count >= MaxQueueSize) return false;
            if (!_ownerProfile.SpendGold(data.cost)) return false;
            _myQueue.Add(new QueuedUnit(data, data.recruitmentTime, targetCell));
            SyncQueueClientRpc(SerializeQueue());
            return true;
        }

        public void AdvanceQueue()
        {
            if (_myQueue.Count == 0) return;
            _myQueue[0].RemainingTicks--;
            if (_myQueue[0].RemainingTicks <= 0)
            {
                QueuedUnit itemToSpawn = _myQueue[0];
                _myQueue.RemoveAt(0);
                if (_ownerProfile?.BaseCell != null && !_ownerProfile.BaseCell.IsOccupied)
                    FinalizeSpawn(itemToSpawn);
                else { itemToSpawn.RemainingTicks = 1; _myQueue.Insert(0, itemToSpawn); }
            }
            SyncQueueClientRpc(SerializeQueue());
        }

        public void FinalizeSpawn(QueuedUnit item)
        {
            if (!IsServer) return;
            if (gridManager == null) { Debug.LogError($"[UnitSpawner] FinalizeSpawn: gridManager NULL! owner={_ownerId}"); return; }
            CellData spawnCell = _ownerProfile?.BaseCell;
            if (spawnCell == null) return;
            Vector3 spawnPos = gridManager.GetWorldPosition(spawnCell.Q, spawnCell.R);
            int newId = GameController.Instance.GetNextAvailableId();
            GameObject go = Instantiate(item.Data.unitPrefab, spawnPos, Quaternion.identity);
            UnitController controller = go.GetComponent<UnitController>();
            if (!go.TryGetComponent<NetworkObject>(out var netObj)) { Debug.LogError("[UnitSpawner] NetworkObject hiányzik!"); return; }
            netObj.Spawn();
            controller.NetworkUnitId.Value = newId;
            controller.NetworkOwnerId.Value = _ownerId;
            controller.NetworkUnitTypeIndex.Value = item.Data.index;
            var path = item.TargetCell != null ? gridManager.FindPath(spawnCell, item.TargetCell) : null;
            controller.Initialize(newId, item.Data, path, gridManager, _ownerId);
        }

        public void RemoveUnitFromQueue(int index)
        {
            if (IsServer) ExecuteRemoveFromQueue(index);
            else RemoveFromQueueServerRpc(index);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RemoveFromQueueServerRpc(int index) => ExecuteRemoveFromQueue(index);

        private void ExecuteRemoveFromQueue(int index)
        {
            if (index <= 0 || index >= _myQueue.Count) return;
            _ownerProfile?.AddGold(_myQueue[index].Data.cost);
            _myQueue.RemoveAt(index);
            SyncQueueClientRpc(SerializeQueue());
        }

        public void SendSpawnRequest(int unitSlot, int targetCellId)
        {
            if (IsServer) ExecuteSpawnLogic(unitSlot, targetCellId);
            else SpawnRequestServerRpc(unitSlot, targetCellId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SpawnRequestServerRpc(int unitSlot, int targetCellId) => ExecuteSpawnLogic(unitSlot, targetCellId);

        private void ExecuteSpawnLogic(int unitSlot, int targetCellId)
        {
            if (_ownerProfile == null) _ownerProfile = GameController.Instance?.GetPlayerById(_ownerId);
            if (gridManager == null) gridManager = GridManager.Instance;
            UnitData data = SlotToData(unitSlot);
            CellData target = (targetCellId == -1) ? _ownerProfile?.BaseCell : gridManager?.GetCellById(targetCellId);
            if (data != null) RequestUnit(data, target);
        }

        private int[] SerializeQueue()
        {
            var result = new int[_myQueue.Count * 2];
            for (int i = 0; i < _myQueue.Count; i++) { result[i * 2] = _myQueue[i].Data.index; result[i * 2 + 1] = _myQueue[i].RemainingTicks; }
            return result;
        }

        [ClientRpc]
        private void SyncQueueClientRpc(int[] serialized)
        {
            if (IsServer) return;
            _myQueue.Clear();
            for (int i = 0; i < serialized.Length; i += 2)
            {
                var data = GameController.Instance.GetUnitDataByIndex(serialized[i]);
                if (data == null) continue;
                _myQueue.Add(new QueuedUnit(data, data.recruitmentTime, null) { RemainingTicks = serialized[i + 1] });
            }
        }

        public IReadOnlyList<QueuedUnit> GetQueue() => _myQueue.AsReadOnly();

        private UnitData SlotToData(int slot) => slot switch
        {
            0 => axeman,
            1 => spearman,
            2 => cavalry,
            3 => scout,
            _ => null
        };
    }
}