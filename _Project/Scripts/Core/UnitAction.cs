namespace GridEmpire.Core
{
    public enum ActionType {Idle, Move, Attack, Capture, Spawn }

    [System.Serializable]
    public class UnitAction
    {
        public ActionType Type;
        // A konkrét UnitController helyett az interfészt használjuk!
        public IUnit Performer; 
        public CellData TargetCell;
        public IUnit TargetUnit;
        public UnitData SpawnData;       
        public int PlayerId;            
    }
}