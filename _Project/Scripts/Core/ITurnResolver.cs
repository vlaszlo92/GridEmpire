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
        void EnqueueAction(UnitAction action);
    }
}