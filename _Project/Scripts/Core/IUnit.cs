namespace GridEmpire.Core
{
    public interface IUnit
    {
        int OwnerId { get; }
        UnitData Data { get; }
        CellData CurrentCell { get; }
        bool IsDead { get; }
        void DestroyUnit(); // Ha pl. a játékos kiesik, ezen keresztül törölhetjük az egységeit
    }
}