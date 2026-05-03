namespace GridEmpire.Shared
{
    public enum ActionType {Idle, Move, Attack, Capture, Spawn }

    [System.Serializable]
    public class UnitAction
    {
        public ActionType Type;
        public int PerformerUnitId;
        public int TargetCellId;
        public int TargetUnitId;
        public int PlayerId;
    }
}