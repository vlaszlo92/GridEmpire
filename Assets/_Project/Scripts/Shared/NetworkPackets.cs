using GridEmpire.Shared;
using System;
using System.Collections.Generic;

namespace GridEmpire.Shared
{
    /*  
        Data Package,    Content
        Header,         TurnNumber (e.g., 42)
        Unit Actions,   "List: [UnitId, ActionType, TargetCellID, TargetUnitId, RemainingHP, IsDead]"
        New Units,      "List: [NewUnitId, PlayerId, UnitTypeId, CellID]"
        Economy,        "List: [PlayerId, CurrentGold]"
        Map Updates,    "List: [CellID, NewOwnerId]"
    */

    // This is the main package that the server broadcasts
    [Serializable]
    public class TurnSnapshot
    {
        public int TurnIndex;
        public List<UnitActionResult> UnitActions = new List<UnitActionResult>();
        public List<NewUnitData> SpawnedUnits = new List<NewUnitData>();
        public List<PlayerSyncData> PlayerStats = new List<PlayerSyncData>();
        public List<CellSyncData> MapUpdates = new List<CellSyncData>();
    }

    [Serializable]
    public class UnitActionResult
    {
        public int UnitId;
        public ActionType Type;
        public int TargetCellId;
        public int TargetUnitId;
        public float NewHP;
        public bool IsDead;
        public float NewStamina;
    }

    [Serializable]
    public class NewUnitData
    {
        public int UnitId;
        public int OwnerId;
        public string UnitTypeKey;
        public int SpawnCellId;
    }

    [Serializable]
    public class PlayerSyncData
    {
        public int PlayerId;
        public float CurrentGold;
    }

    [Serializable]
    public class CellSyncData
    {
        public int CellId;
        public int OwnerId; 
        public int InfluencingPlayerId;
        public float InfluenceValue;
    }
}