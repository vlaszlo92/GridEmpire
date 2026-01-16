using GridEmpire.Core;
using GridEmpire.Gameplay;

public enum ActionType { Move, Attack, Capture, Spawn }

public class UnitAction
{
    public ActionType Type;
    public UnitController Performer; // Spawn esetén ez null lehet, vagy a bázis
    public CellData TargetCell;
    public UnitController TargetUnit;
    public UnitData SpawnData;       // Új: mit akarunk spawnolni
    public int PlayerId;            // Új: ki akarja a spawnt
}