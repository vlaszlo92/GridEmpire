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

        private PlayerProfile GetProfile()
        {
            if (_ownerProfile == null && _ownerId != -1)
                _ownerProfile = GameController.Instance?.GetPlayerById(_ownerId);
            return _ownerProfile;
        }

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
            base.OnNetworkSpawn();

            NetworkOwnerId.OnValueChanged += OnNetworkOwnerIdChanged;

            if (axeman != null) GameController.Instance?.RegisterUnitData(axeman);
            if (spearman != null) GameController.Instance?.RegisterUnitData(spearman);
            if (cavalry != null) GameController.Instance?.RegisterUnitData(cavalry);
            if (scout != null) GameController.Instance?.RegisterUnitData(scout);

            if (IsServer && _pendingOwnerId != -1)
            {
                NetworkOwnerId.Value = _pendingOwnerId;
            }

            if (NetworkOwnerId.Value != -1)
            {
                Initialize(NetworkOwnerId.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            NetworkOwnerId.OnValueChanged -= OnNetworkOwnerIdChanged;
        }

        private void OnNetworkOwnerIdChanged(int previousValue, int newValue)
        {
            if (newValue != -1)
            {
                Initialize(newValue);
            }
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

            Debug.Log($"[UnitSpawner] Initialize: owner={_ownerId}, profile={GetProfile()?.Name ?? "NULL"}, grid={gridManager != null}");
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
            var profile = GetProfile();
            if (targetCell == null) targetCell = profile?.BaseCell;

            if (IsServer)
            {
                UnitData data = SlotToData(unitSlot);
                if (data != null) RequestUnit(data, targetCell);
            }
            else
            {
                SendSpawnRequest(unitSlot, targetCell?.Id ?? -1);
            }
        }

        public bool RequestUnit(UnitData data, CellData targetCell)
        {
            var profile = GetProfile();
            if (profile == null || !profile.IsAlive)
            {
                Debug.LogWarning($"[UnitSpawner] RequestUnit meghiúsult: profile null vagy nem él! owner={_ownerId}");
                return false;
            }
            if (_myQueue.Count >= MaxQueueSize) return false;
            if (!profile.SpendGold(data.cost))
            {
                Debug.LogWarning($"[UnitSpawner] RequestUnit meghiúsult: nincs elég arany! owner={_ownerId}, cost={data.cost}, gold={profile.Gold}");
                return false;
            }

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
                var profile = GetProfile();
                if (profile?.BaseCell != null && !profile.BaseCell.IsOccupied)
                    FinalizeSpawn(itemToSpawn);
                else
                {
                    itemToSpawn.RemainingTicks = 1;
                    itemToSpawn.IsWaitingToSpawn = true;
                    _myQueue.Insert(0, itemToSpawn);
                }
            }
            SyncQueueClientRpc(SerializeQueue());
        }

        public void FinalizeSpawn(QueuedUnit item)
        {
            if (!IsServer) return;
            if (gridManager == null) { Debug.LogError($"[UnitSpawner] FinalizeSpawn: gridManager NULL! owner={_ownerId}"); return; }

            var profile = GetProfile();
            CellData spawnCell = profile?.BaseCell;
            if (spawnCell == null) return;

            Vector3 spawnPos = gridManager.GetWorldPosition(spawnCell.Q, spawnCell.R);
            Vector3 dir = gridManager.GetMapCenterWorldPosition() - spawnPos;
            dir.y = 0;
            Quaternion initialRot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized) : Quaternion.identity;

            int newId = GameController.Instance.GetNextAvailableId();
            GameObject go = Instantiate(item.Data.unitPrefab, spawnPos, initialRot);
            UnitController controller = go.GetComponent<UnitController>();
            if (!go.TryGetComponent<NetworkObject>(out var netObj)) { Debug.LogError("[UnitSpawner] NetworkObject hiányzik!"); return; }            

            controller.NetworkUnitId.Value = newId;
            controller.NetworkOwnerId.Value = _ownerId;
            controller.NetworkUnitTypeIndex.Value = item.Data.index;
            netObj.Spawn();

            var path = item.TargetCell != null ? gridManager.FindPath(spawnCell, item.TargetCell) : null;
            controller.Initialize(newId, item.Data, path, gridManager, _ownerId);
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
            var profile = GetProfile();
            if (gridManager == null) gridManager = GridManager.Instance;
            UnitData data = SlotToData(unitSlot);
            CellData target = (targetCellId == -1) ? profile?.BaseCell : gridManager?.GetCellById(targetCellId);
            if (data != null) RequestUnit(data, target);
        }

        private int[] SerializeQueue()
        {
            var result = new int[_myQueue.Count * 3];
            for (int i = 0; i < _myQueue.Count; i++)
            {
                result[i * 3] = _myQueue[i].Data.index;
                result[i * 3 + 1] = _myQueue[i].RemainingTicks;
                result[i * 3 + 2] = _myQueue[i].IsWaitingToSpawn ? 1 : 0;
            }
            return result;
        }

        [ClientRpc]
        private void SyncQueueClientRpc(int[] serialized)
        {
            if (IsServer) return;
            _myQueue.Clear();
            for (int i = 0; i < serialized.Length; i += 3)
            {
                var data = GameController.Instance.GetUnitDataByIndex(serialized[i]);
                if (data == null) continue;
                _myQueue.Add(new QueuedUnit(data, data.recruitmentTime, null)
                {
                    RemainingTicks = serialized[i + 1],
                    IsWaitingToSpawn = serialized[i + 2] == 1
                });
            }
        }

        public IReadOnlyList<QueuedUnit> GetQueue() => _myQueue.AsReadOnly();

        public void RemoveUnitFromQueue(int index)
        {
            if (index <= 0 || index >= _myQueue.Count) return;

            if (!IsServer)
            {
                _myQueue.RemoveAt(index);
                RemoveFromQueueServerRpc(index);
            }
            else
            {
                ExecuteRemoveFromQueue(index);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RemoveFromQueueServerRpc(int index) => ExecuteRemoveFromQueue(index);

        private void ExecuteRemoveFromQueue(int index)
        {
            if (index <= 0 || index >= _myQueue.Count) return;
            GetProfile()?.AddGold(_myQueue[index].Data.cost);
            _myQueue.RemoveAt(index);
            SyncQueueClientRpc(SerializeQueue());
        }

        public void ClearQueue()
        {
            if (_myQueue.Count <= 1) return;

            if (!IsServer)
            {
                for (int i = _myQueue.Count - 1; i > 0; i--)
                {
                    _myQueue.RemoveAt(i);
                }
                ClearQueueServerRpc();
            }
            else
            {
                ExecuteClearQueue();
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ClearQueueServerRpc() => ExecuteClearQueue();

        private void ExecuteClearQueue()
        {
            if (_myQueue.Count <= 1) return;

            for (int i = _myQueue.Count - 1; i > 0; i--)
            {
                GetProfile()?.AddGold(_myQueue[i].Data.cost);
                _myQueue.RemoveAt(i);
            }

            SyncQueueClientRpc(SerializeQueue());
        }

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