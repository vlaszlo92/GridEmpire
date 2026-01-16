using GridEmpire.Core;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace GridEmpire.Gameplay
{
    public class TurnResolver : MonoBehaviour, ITurnResolver
    {
        public List<UnitAction> _actionQueue = new List<UnitAction>();
        private UnitSpawner _spawner;

        private void Awake()
        {
            _spawner = Object.FindFirstObjectByType<UnitSpawner>();
            var tm = Object.FindFirstObjectByType<TurnManager>();
            if (tm != null) tm.RegisterResolver(this);
        }

        public void EnqueueAction(UnitAction action) => _actionQueue.Add(action);

        public void ResolveAll()
        {
            UnitController[] allUnits = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);

            // 1. GYÁRTÁSI FÁZIS (Itt dõl el, ki születik meg)
            ResolveSpawning();

            // 2. HARCI FÁZIS
            ResolveCombat(allUnits);

            // 3. HÓDÍTÁS ÉS MOZGÁS FÁZIS
            ResolveMovementAndCapture();

            // 4. TAKARÍTÁS
            FinalizeTurn(allUnits);
        }

        private void ResolveSpawning()
        {
            if (_spawner == null) return;

            // Idõ telik a gyártósorokban
            _spawner.AdvanceQueues();

            foreach (var player in GameController.Instance.Players)
            {
                var queue = _spawner.GetQueueForPlayer(player.Id);
                if (queue.Count > 0 && queue[0].remainingTicks <= 0)
                {
                    CellData baseCell = player.BaseCell;
                    if (baseCell != null && baseCell.IsOccupied)
                    {
                        Debug.Log($"[TurnResolver] {player.Name} bázisa foglalt, a(z) {queue[0].data.unitName} várakozik.");
                        continue; // Ugorjuk a gyártást ennél a játékosnál ebben a körben
                    }
                    _spawner.FinalizeSpawn(player.Id, queue[0]);
                    queue.RemoveAt(0);
                }
            }
        }

        private void ResolveCombat(UnitController[] allUnits)
        {
            foreach (var u in allUnits) { u.isInCombat = false; u._combatTarget = null; }

            foreach (var action in _actionQueue.Where(a => a.Type == ActionType.Attack))
            {
                if (action.Performer == null || action.Performer._isDead) continue;

                var enemy = action.TargetUnit;
                if (enemy != null && !enemy._isDead)
                {
                    action.Performer.isInCombat = true;
                    action.Performer._combatTarget = enemy;
                    action.Performer.FaceTarget(enemy.transform.position);
                    enemy.RegisterPendingDamage(action.Performer._data.baseDamage);
                }
            }

            foreach (var u in allUnits) if (u != null) u.ApplyPendingDamage();
        }

        private void ResolveMovementAndCapture()
        {
            // Olyan mezõk, amikre ebben a körben már valaki rálépett
            HashSet<CellData> claimedCells = new HashSet<CellData>();

            foreach (var action in _actionQueue)
            {
                if (action.Performer == null || action.Performer._isDead || action.Performer.isInCombat) continue;

                if (action.Type == ActionType.Capture && action.TargetCell != null)
                {
                    action.Performer.ExecuteFinalCapture(action.TargetCell);
                }
                else if (action.Type == ActionType.Move && action.TargetCell != null)
                {
                    // Validáció: üres-e, és nem foglalta-e le más ebben a tickben
                    if (!action.TargetCell.IsOccupied && !claimedCells.Contains(action.TargetCell))
                    {
                        claimedCells.Add(action.TargetCell);
                        action.Performer.ExecuteFinalMove(action.TargetCell);
                    }
                }
            }
        }

        private void FinalizeTurn(UnitController[] allUnits)
        {
            _actionQueue.Clear();
            foreach (var u in allUnits) if (u != null && u._isDead) u.ExecuteDeath();

            var localPlayer = GameController.Instance.GetLocalPlayer();
            if (localPlayer != null)
                Object.FindFirstObjectByType<GridManager>()?.UpdateFogOfWar(localPlayer.Id);
        }
    }
}