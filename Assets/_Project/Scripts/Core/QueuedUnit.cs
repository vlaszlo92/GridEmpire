using GridEmpire.Core;

namespace GridEmpire.Core
{
    [System.Serializable]
    public class QueuedUnit
    {
        public UnitData Data { get; }
        public CellData TargetCell { get; }
        public int RemainingTicks { get; set; }

        public QueuedUnit(UnitData data, int ticks, CellData targetCell)
        {
            Data = data;
            RemainingTicks = ticks;
            TargetCell = targetCell;
        }
    }
}
