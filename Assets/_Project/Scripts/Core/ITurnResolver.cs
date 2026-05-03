using GridEmpire.Networking;
using GridEmpire.Shared;
using System.Collections.Generic;

namespace GridEmpire.Core
{
    public interface ITurnResolver
    {
        // Elõkészíti a listákat az új körhöz (pl. összegyûjti az egységeket)
        void PrepareForNextTurn();

        // Ebben a metódusban végezzük a számítást idõszeletelve
        // maxTimeMs: mennyi idõnk van ebben a frame-ben számolni
        void TickProcessing(float maxTimeMs);

        // Kész-e a számítás a következõ körre?
        bool IsCalculationComplete();

        // Kényszerített befejezés (ha lejár az 1 mp, de nem végeztünk)
        void ForceComplete();

        // Az eredmények tényleges alkalmazása a játékvilágra (animációk indítása)
        void ApplyResults();

        // Debug/UI célokra: hol tartunk a feldolgozásban (0-1)
        float GetProgress();

        void RegisterUnit(IUnit unit); 
        void UnregisterUnit(IUnit unit); 
        void RegisterSpawner(ISpawner spawner); 
        void UnregisterSpawner(ISpawner spawner);
        void EnqueueAction(UnitAction action);
        TurnSnapshot BuildSnapshot(int turnIndex);
        void MarkCellChanged(int cellId);
        IReadOnlyCollection<int> GetChangedCells();
        void ClearChangedCells();
    }
}