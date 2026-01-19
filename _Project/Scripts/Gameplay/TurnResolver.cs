using GridEmpire.Core;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace GridEmpire.Gameplay
{
    public class TurnResolver : MonoBehaviour, ITurnResolver
    {
        private enum ResolveState { Collecting, Spawning, Combat, Movement, Ready }
        private ResolveState _currentState = ResolveState.Ready;

        private List<UnitAction> _actionQueue = new List<UnitAction>();
        private List<UnitController> _allUnits = new List<UnitController>();

        // Ez tárolja az összes aktív spawert (Local + AI)
        private List<UnitSpawner> _allSpawners = new List<UnitSpawner>();

        private int _currentUnitIndex = 0;

        private void Awake()
        {
            TurnManager.Instance?.RegisterResolver(this);
        }

        public void PrepareForNextTurn()
        {
            _actionQueue.Clear();
            _allUnits = FindObjectsByType<UnitController>(FindObjectsSortMode.None).ToList();

            // Minden kör elején megkeressük a spawner-eket
            _allSpawners = FindObjectsByType<UnitSpawner>(FindObjectsSortMode.None).ToList();

            _currentUnitIndex = 0;
            _currentState = ResolveState.Spawning;
        }

        public void TickProcessing(float maxTimeMs)
        {
            float startTime = Time.realtimeSinceStartup * 1000f;

            while (Time.realtimeSinceStartup * 1000f - startTime < maxTimeMs)
            {
                if (_currentState == ResolveState.Spawning)
                {
                    // CSAK A TICKEKET CSÖKKENTJÜK! 
                    // Minden spawneren csak EGYSZER futhat le ebben a fázisban.
                    foreach (var spawner in _allSpawners)
                    {
                        if (spawner != null)
                        {
                            var queue = spawner.GetQueue();
                            if (queue.Count > 0)
                            {
                                queue[0].remainingTicks--;
                            }
                        }
                    }
                    _currentState = ResolveState.Combat;
                }
                else if (_currentState == ResolveState.Combat)
                {
                    ProcessCombatStep();
                }
                else if (_currentState == ResolveState.Movement)
                {
                    ProcessMovementStep();
                }
                else if (_currentState == ResolveState.Ready)
                {
                    break;
                }
            }
        }

        public void ApplyResults()
        {
            // 1. EGYSÉG GYÁRTÁS VÉGLEGESÍTÉSE (Itt történik a tényleges Instantiate)
            foreach (var spawner in _allSpawners)
            {
                if (spawner == null) continue;

                var queue = spawner.GetQueue();
                if (queue.Count > 0 && queue[0].remainingTicks <= 0)
                {
                    // Megkeressük a spawner tulajdonosát a foglaltság ellenõrzéshez
                    var owner = GameController.Instance.GetPlayerById(spawner.OwnerId);

                    if (owner != null && owner.BaseCell != null && !owner.BaseCell.IsOccupied)
                    {
                        // Itt hívjuk meg a spawner belsõ metódusát a példányosításhoz
                        spawner.FinalizeSpawn(queue[0]);
                        queue.RemoveAt(0);
                    }
                }
            }

            // 2. SEBZÉSEK ÉRVÉNYESÍTÉSE
            foreach (var u in _allUnits)
            {
                if (u != null) u.ApplyPendingDamage();
            }

            // 3. MOZGÁSOK ÉS FOGLALÁSOK
            ResolveMovementAndCapture();

            // 4. HALOTTAK
            foreach (var u in _allUnits)
            {
                if (u != null && u._isDead) u.ExecuteDeath();
            }

            // 5. FOW
            var localPlayer = GameController.Instance.GetLocalPlayer();
            if (localPlayer != null)
            {
                Object.FindFirstObjectByType<GridManager>()?.UpdateFogOfWar(localPlayer.Id);
            }
        }

        // --- AZ EREDETI METÓDUSAID VÁLTOZATLANUL ---

        private void ProcessCombatStep()
        {
            if (_currentUnitIndex < _allUnits.Count)
            {
                var unit = _allUnits[_currentUnitIndex];
                if (unit != null && !unit._isDead) unit.CalculateCombatLogic();
                _currentUnitIndex++;
            }
            else
            {
                _currentUnitIndex = 0;
                _currentState = ResolveState.Movement;
            }
        }

        private void ProcessMovementStep()
        {
            if (_currentUnitIndex < _allUnits.Count)
            {
                var unit = _allUnits[_currentUnitIndex];
                if (unit != null && !unit._isDead) unit.PlanAction();
                _currentUnitIndex++;
            }
            else
            {
                _currentState = ResolveState.Ready;
            }
        }

        private void ResolveMovementAndCapture()
        {
            HashSet<CellData> claimedCells = new HashSet<CellData>();
            foreach (var action in _actionQueue)
            {
                if (action.Performer == null || action.Performer.IsDead) continue;
                UnitController controller = action.Performer as UnitController;
                if (controller == null || controller.isInCombat) continue;

                if (action.Type == ActionType.Capture && action.TargetCell != null)
                    controller.ExecuteFinalCapture(action.TargetCell);
                else if (action.Type == ActionType.Move && action.TargetCell != null)
                {
                    if (!action.TargetCell.IsOccupied && !claimedCells.Contains(action.TargetCell))
                    {
                        claimedCells.Add(action.TargetCell);
                        controller.ExecuteFinalMove(action.TargetCell);
                    }
                }
            }
        }

        public void EnqueueAction(UnitAction action) => _actionQueue.Add(action);
        public bool IsCalculationComplete() => _currentState == ResolveState.Ready;
        public void ForceComplete() { while (!IsCalculationComplete()) TickProcessing(999f); }
        public float GetProgress()
        {
            if (_allUnits.Count == 0) return 1f;
            return (int)_currentState / 4f + (_currentUnitIndex / (float)_allUnits.Count / 4f);
        }
    }
}