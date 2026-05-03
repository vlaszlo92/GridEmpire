using GridEmpire.Core;
using GridEmpire.Networking;
using GridEmpire.Shared;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace GridEmpire.Gameplay
{
    public class TurnResolver : MonoBehaviour, ITurnResolver
    {
        private enum ResolveState { Collecting, Spawning, Combat, CaptureConflict, Movement, Ready }
        private ResolveState _currentState = ResolveState.Ready;

        private List<UnitAction> _actionQueue = new List<UnitAction>();

        private readonly HashSet<UnitController> _registeredUnits = new HashSet<UnitController>();
        private List<UnitController> _processingUnits = new List<UnitController>();

        private readonly HashSet<ISpawner> _registeredSpawners = new HashSet<ISpawner>();
        private List<ISpawner> _processingSpawners = new List<ISpawner>();

        private readonly HashSet<int> _changedCellIds = new HashSet<int>();
        public void MarkCellChanged(int cellId) => _changedCellIds.Add(cellId);
        public IReadOnlyCollection<int> GetChangedCells() => _changedCellIds;
        public void ClearChangedCells() => _changedCellIds.Clear();

        private int _currentUnitIndex = 0;
        private Stopwatch _stopwatch = new Stopwatch();

        private void Awake()
        {
            TurnManager.Instance?.RegisterResolver(this);
        }

        // --- Regisztrációs API ---

        public void RegisterUnit(IUnit unit)
        {
            if (unit == null) return;
            if (unit is UnitController uc) _registeredUnits.Add(uc);
        }

        public void UnregisterUnit(IUnit unit)
        {
            if (unit == null) return;
            if (unit is UnitController uc) _registeredUnits.Remove(uc);
        }

        public void RegisterSpawner(ISpawner spawner)
        {
            UnityEngine.Debug.Log($"[TurnResolver] Registering spawner: {spawner}");
            if (spawner == null) return;
            _registeredSpawners.Add(spawner);
        }

        public void UnregisterSpawner(ISpawner spawner)
        {
            if (spawner == null) return;
            _registeredSpawners.Remove(spawner);
        }

        // --- ITurnResolver implementáció ---

        public void PrepareForNextTurn()
        {
            _actionQueue.Clear();

            _processingUnits = _registeredUnits.Where(u => u != null).ToList();
            _processingSpawners = _registeredSpawners.Where(s => s != null).ToList();

            _currentUnitIndex = 0;
            _currentState = ResolveState.Spawning;
        }

        public void TickProcessing(float maxTimeMs)
        {
            if (!_stopwatch.IsRunning) _stopwatch.Restart();

            while (_stopwatch.ElapsedMilliseconds < maxTimeMs)
            {
                if (_currentState == ResolveState.Spawning)
                {
                    for (int i = 0; i < _processingSpawners.Count; i++)
                    {
                        var spawner = _processingSpawners[i];
                        if (spawner == null) continue;
                        spawner.AdvanceQueue();
                    }
                    _currentState = ResolveState.Combat;
                }
                else if (_currentState == ResolveState.Combat)
                {
                    ProcessCombatStep();
                }
                else if (_currentState == ResolveState.CaptureConflict)
                {
                    ProcessCaptureConflictStep();
                }
                else if (_currentState == ResolveState.Movement)
                {
                    ProcessMovementStep();
                }
                else if (_currentState == ResolveState.Ready)
                {
                    break;
                }

                if ((_currentState == ResolveState.Combat ||
                     _currentState == ResolveState.CaptureConflict ||
                     _currentState == ResolveState.Movement) &&
                    (_processingUnits == null || _processingUnits.Count == 0))
                {
                    _currentState = ResolveState.Ready;
                    break;
                }
            }

            _stopwatch.Stop();
        }

        public void ApplyResults()
        {
            // 1. Sebzések érvényesítése
            foreach (var u in _processingUnits)
            {
                if (u != null) u.ApplyPendingDamage();
            }

            // 2. Mozgások és foglalások
            ResolveMovementAndCapture();

            // 3. Halottak kezelése
            foreach (var u in _processingUnits)
            {
                if (u != null && u._isDead) u.ExecuteDeath();
            }

            // 4. Fog of War frissítése
            var localPlayer = GameController.Instance.GetLocalPlayer();
            if (localPlayer != null)
            {
                Object.FindFirstObjectByType<GridManager>()?.UpdateFogOfWar(localPlayer.Id);
            }
        }

        // --- Feldolgozó lépések ---

        private void ProcessCombatStep()
        {
            if (_currentUnitIndex < _processingUnits.Count)
            {
                var unit = _processingUnits[_currentUnitIndex];
                if (unit != null && !unit._isDead) unit.CalculateCombatLogic();
                _currentUnitIndex++;
            }
            else
            {
                // Combat kész – CaptureConflict fázis következik
                _currentUnitIndex = 0;
                _currentState = ResolveState.CaptureConflict;
            }
        }

        private void ProcessCaptureConflictStep()
        {
            if (_currentUnitIndex < _processingUnits.Count)
            {
                var unit = _processingUnits[_currentUnitIndex];
                if (unit != null && !unit._isDead) unit.CalculateCaptureConflict();
                _currentUnitIndex++;
            }
            else
            {
                // CaptureConflict kész – Movement fázis következik
                _currentUnitIndex = 0;
                _currentState = ResolveState.Movement;
            }
        }

        private void ProcessMovementStep()
        {
            if (_currentUnitIndex < _processingUnits.Count)
            {
                var unit = _processingUnits[_currentUnitIndex];
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
            GridManager gridManager = Object.FindFirstObjectByType<GridManager>();

            foreach (var action in _actionQueue)
            {
                UnitController controller = _processingUnits.Find(u => u.Id == action.PerformerUnitId);
                if (controller == null || controller.IsDead || controller.isInCombat) continue;

                if (action.Type == ActionType.Capture && action.TargetCellId != -1)
                {
                    CellData targetCell = gridManager.GetCellById(action.TargetCellId);
                    if (targetCell != null)
                        controller.ExecuteFinalCapture(targetCell);
                }
                else if (action.Type == ActionType.Move && action.TargetCellId != -1)
                {
                    CellData targetCell = gridManager.GetCellById(action.TargetCellId);
                    if (targetCell != null && !targetCell.IsOccupied && !claimedCells.Contains(targetCell))
                    {
                        claimedCells.Add(targetCell);
                        controller.ExecuteFinalMove(targetCell);
                    }
                }
            }
        }

        public void EnqueueAction(UnitAction action) => _actionQueue.Add(action);
        public bool IsCalculationComplete() => _currentState == ResolveState.Ready;

        public void ForceComplete()
        {
            while (!IsCalculationComplete())
                TickProcessing(999f);
        }

        public float GetProgress()
        {
            if (_processingUnits == null || _processingUnits.Count == 0) return 1f;
            float phaseIndex = (int)_currentState;
            return phaseIndex / 5f + (_currentUnitIndex / (float)_processingUnits.Count / 5f);
        }

        public TurnSnapshot BuildSnapshot(int turnIndex)
        {
            var snapshot = new TurnSnapshot { TurnIndex = turnIndex };

            foreach (var action in _actionQueue)
            {
                var unit = _processingUnits.Find(u => u.Id == action.PerformerUnitId);
                if (unit == null) continue;

                snapshot.UnitActions.Add(new UnitActionResult
                {
                    UnitId = action.PerformerUnitId,
                    Type = action.Type,
                    TargetCellId = action.TargetCellId,
                    TargetUnitId = action.TargetUnitId,
                    NewHP = unit.GetCurrentHP(),
                    IsDead = unit._isDead
                });
            }

            _changedCellIds.Clear();

            foreach (var player in GameController.Instance.Players)
            {
                snapshot.PlayerStats.Add(new PlayerSyncData
                {
                    PlayerId = player.Id,
                    CurrentGold = player.Gold
                });
            }

            return snapshot;
        }
    }
}