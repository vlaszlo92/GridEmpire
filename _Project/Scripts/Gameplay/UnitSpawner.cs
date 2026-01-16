using GridEmpire.Core;
using System.Collections.Generic;
using UnityEngine;

namespace GridEmpire.Gameplay
{
    public class UnitSpawner : MonoBehaviour
    {
        public static UnitSpawner Instance { get; private set; }
        public static System.Action<int, int, CellData> OnRequestUnitSpawn;

        [Header("Unit Definitions")]
        [SerializeField] private UnitData axeman;
        [SerializeField] private UnitData spearman;
        [SerializeField] private UnitData cavalry;
        [SerializeField] private GridManager gridManager;

        private Dictionary<int, List<QueuedUnit>> _playerQueues = new Dictionary<int, List<QueuedUnit>>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void OnEnable() => OnRequestUnitSpawn += HandleSpawnRequest;
        private void OnDisable() => OnRequestUnitSpawn -= HandleSpawnRequest;

        private void HandleSpawnRequest(int playerId, int unitSlot, CellData targetCell)
        {
            UnitData data = unitSlot switch { 0 => axeman, 1 => spearman, 2 => cavalry, _ => null };
            if (data != null) RequestUnit(playerId, data, targetCell);
        }

        // --- VALIDÁCIÓS ÉS ELÕKÉSZÍTÕ LOGIKA ---
        public bool RequestUnit(int playerId, UnitData data, CellData targetCell)
        {
            var playerProfile = GameController.Instance.GetPlayerById(playerId);
            if (playerProfile == null || playerProfile.Gold < data.cost || !playerProfile.IsAlive) return false;

            playerProfile.Gold -= data.cost;

            if (!_playerQueues.ContainsKey(playerId)) _playerQueues[playerId] = new List<QueuedUnit>();
            
            if (targetCell == null) targetCell = playerProfile.BaseCell;
            _playerQueues[playerId].Add(new QueuedUnit(data, data.recruitmentTime, targetCell));
            return true;
        }

        // --- TURNRESOLVER HÍVJA ---
        public void AdvanceQueues()
        {
            foreach (var queue in _playerQueues.Values)
            {
                if (queue.Count > 0) queue[0].remainingTicks--;
            }
        }

        // --- TURNRESOLVER HÍVJA ---
        public void FinalizeSpawn(int playerId, QueuedUnit item)
        {
            var playerProfile = GameController.Instance.GetPlayerById(playerId);
            CellData spawnCell = playerProfile.BaseCell;

            GameObject go = Instantiate(item.data.unitPrefab, gridManager.GetWorldPosition(spawnCell.Q, spawnCell.R), Quaternion.identity);
            UnitController controller = go.AddComponent<UnitController>();

            var path = gridManager.FindPath(spawnCell, item.targetCell);
            controller.Initialize(item.data, path, gridManager, playerId);
            
            // Kezdõ irány (AI vagy bázis elhelyezkedése alapján)
            int dir = spawnCell.Q > 0 ? 4 : 1;
            controller.SetStartingDirection(dir);
        }

        public List<QueuedUnit> GetQueueForPlayer(int playerId) => 
            _playerQueues.TryGetValue(playerId, out var q) ? q : new List<QueuedUnit>();
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
}