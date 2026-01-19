using GridEmpire.Core;
using System.Collections.Generic;
using UnityEngine;

namespace GridEmpire.Gameplay
{
    public class UnitSpawner : MonoBehaviour
    {
        public static System.Action<int, int, CellData> OnRequestUnitSpawn;

        [Header("Unit Definitions")]
        [SerializeField] private UnitData axeman;
        [SerializeField] private UnitData spearman;
        [SerializeField] private UnitData cavalry;
        [SerializeField] private UnitData scout;
        [SerializeField] private GridManager gridManager;

        private int _ownerId = -1; // Alapértelmezett érvénytelen érték
        private PlayerProfile _ownerProfile;
        private List<QueuedUnit> _myQueue = new List<QueuedUnit>();
        public int OwnerId => _ownerId;

        private void Start()
        {
            // CSAK AKKOR inicializálja magát local playernek, ha még senki nem állította be
            // Ez biztosítja, hogy a Hierarchy-ba kézzel letett spawner mûködjön, 
            // de az AI-é ne íródjon felül.
            if (_ownerId == -1)
            {
                var local = GameController.Instance.GetLocalPlayer();
                if (local != null) Initialize(local.Id);
            }

            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
        }

        public void Initialize(int userId)
        {
            _ownerId = userId;
            _ownerProfile = GameController.Instance.GetPlayerById(userId);
        }

        private void OnEnable() => OnRequestUnitSpawn += HandleSpawnRequest;
        private void OnDisable() => OnRequestUnitSpawn -= HandleSpawnRequest;

        private void HandleSpawnRequest(int playerId, int unitSlot, CellData targetCell)
        {
            if (playerId != _ownerId || _myQueue.Count >= 6) return;

            UnitData data = unitSlot switch { 0 => axeman, 1 => spearman, 2 => cavalry, 3 => scout, _ => null };
            if (data != null) RequestUnit(data, targetCell);
        }

        public bool RequestUnit(UnitData data, CellData targetCell)
        {
            if (_ownerProfile == null || _ownerProfile.Gold < data.cost || !_ownerProfile.IsAlive)
                return false;

            _ownerProfile.Gold -= data.cost;
            if (targetCell == null) targetCell = _ownerProfile.BaseCell;

            _myQueue.Add(new QueuedUnit(data, data.recruitmentTime, targetCell));
            return true;
        }

        public void AdvanceQueue()
        {
            if (_myQueue.Count > 0)
            {
                _myQueue[0].remainingTicks--;

                if (_myQueue[0].remainingTicks <= 0)
                {
                    // Ellenõrizzük, hogy a bázis szabad-e
                    if (_ownerProfile != null && _ownerProfile.BaseCell != null && !_ownerProfile.BaseCell.IsOccupied)
                    {
                        FinalizeSpawn(_myQueue[0]);
                        _myQueue.RemoveAt(0);
                    }
                }
            }
        }

        public void FinalizeSpawn(QueuedUnit item)
        {
            CellData spawnCell = _ownerProfile.BaseCell;
            if (spawnCell == null) return;

            GameObject go = Instantiate(item.data.unitPrefab, gridManager.GetWorldPosition(spawnCell.Q, spawnCell.R), Quaternion.identity);
            UnitController controller = go.AddComponent<UnitController>();

            var path = gridManager.FindPath(spawnCell, item.targetCell);
            controller.Initialize(item.data, path, gridManager, _ownerId);
        }

        public List<QueuedUnit> GetQueue() => _myQueue;

        public void RemoveUnitFromQueue(int index)
        {
            if (index >= 0 && index < _myQueue.Count)
            {
                if (_ownerProfile != null) _ownerProfile.Gold += _myQueue[index].data.cost;
                _myQueue.RemoveAt(index);
            }
        }
    }
}

[System.Serializable]
public class QueuedUnit
{
    public UnitData data;
    public CellData targetCell;
    public int remainingTicks;

    public QueuedUnit(UnitData data, int ticks, CellData targetCell)
    {
        this.data = data;
        this.remainingTicks = ticks;
        this.targetCell = targetCell;
    }
}
