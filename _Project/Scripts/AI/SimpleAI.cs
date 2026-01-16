using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using GridEmpire.Core;
using GridEmpire.Gameplay;

namespace GridEmpire.AI
{
    public class SimpleAI : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private int aiPlayerId = 1;

        [Header("Expansion Settings")]
        [SerializeField] private float capturePowerPerTick = 0.15f;

        [Header("Recruitment Settings")]
        [SerializeField] private int preferredUnitSlot = 0; // 0: Axeman, 1: Spearman, 2: Cavalry
        [SerializeField] private int minGoldToKeep = 0;    // Tartalék, ne költse el az összes pénzét azonnal
        [SerializeField] private PlayerProfile aiPlayerProfile;

        // Referenciák elérése a Singletonokon keresztül
        private GridManager Grid => FindFirstObjectByType<GridManager>();

        private void Awake()
        {
            aiPlayerProfile = GameController.Instance.GetPlayerById(aiPlayerId);
        }

        private void OnEnable() => TurnManager.OnTick += ExecuteTurn;
        private void OnDisable() => TurnManager.OnTick -= ExecuteTurn;

        private void ExecuteTurn()
        {
            // 1. TERJESZKEDÉS (Mezõk elfoglalása a bázis körül)
            HandleExpansion();

            // 2. EGYSÉG GYÁRTÁS (Toborzás, ha van elég arany)
            HandleSpawning();
        }

        private void HandleExpansion()
        {
            if (Grid == null) return;

            // Megkeressük az összes olyan mezõt, ami az AI szomszédságában van, de nem az övé
            HashSet<CellData> potentialTargets = new HashSet<CellData>();

            // Az AI összes birtokolt mezõjét lekérjük
            var myCells = Grid.GetAllCells().Where(c => c.OwnerId == aiPlayerId);

            foreach (var cell in myCells)
            {
                foreach (var neighbor in Grid.GetNeighbors(cell))
                {
                    if (neighbor.OwnerId != aiPlayerId)
                        potentialTargets.Add(neighbor);
                }
            }

            if (potentialTargets.Count > 0)
            {
                // Egyszerû prioritás: a bázishoz legközelebbit válassza (vagy az elsõt a listából)
                aiPlayerProfile.SelectedCell = potentialTargets.First();
                aiPlayerProfile.SelectedCell.UpdateCapture(aiPlayerId, capturePowerPerTick);

                // Ha az UpdateCapture átbillentette a tulajdonost, jelezzük a GridManagernek
                if (aiPlayerProfile.SelectedCell.OwnerId == aiPlayerId)
                {
                    Grid.FinalizeCapture(aiPlayerProfile.SelectedCell, aiPlayerId);
                    Debug.Log($"AI (ID: {aiPlayerId}) elfoglalta: {aiPlayerProfile.SelectedCell.Q}, {aiPlayerProfile.SelectedCell.R}");
                }
            }
        }

        private void HandleSpawning()
        {
            // Ellenõrizzük a gyártósort a Spawneren keresztül
            var myQueue = UnitSpawner.Instance.GetQueueForPlayer(aiPlayerId);

            // Ha van elég pénze (hagyva egy kis tartalékot) és nincs tele a sora (max 2 egység várhat)
            if (aiPlayerProfile.Gold >= (minGoldToKeep + 1) && myQueue.Count < 2)
            {
                // A javított, kétparaméteres eseményt hívjuk meg!
                // (Ki kéri, Melyik slotot)
                UnitSpawner.OnRequestUnitSpawn?.Invoke(aiPlayerId, preferredUnitSlot, aiPlayerProfile.SelectedCell);

                Debug.Log($"AI (ID: {aiPlayerId}) toborzási kérést küldött. Slot: {preferredUnitSlot}");
            }
        }
    }
}