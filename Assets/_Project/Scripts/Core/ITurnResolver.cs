using GridEmpire.Shared;
using System.Collections.Generic;

namespace GridEmpire.Core
{
    public interface ITurnResolver
    {
        void PrepareForNextTurn();
        void TickProcessing(float maxTimeMs);
        bool IsCalculationComplete();
        void ForceComplete();
        void ApplyResults();
        float GetProgress();
        void RegisterUnit(IUnit unit);
        void UnregisterUnit(IUnit unit);
        void RegisterSpawner(ISpawner spawner);
        void UnregisterSpawner(ISpawner spawner);
        void EnqueueAction(UnitAction action);
        TurnSnapshot BuildSnapshotForPlayer(int turnIndex, int playerId);
        void MarkCellChanged(int cellId);
        IReadOnlyCollection<int> GetChangedCells();
        void ClearChangedCells();
    }
}