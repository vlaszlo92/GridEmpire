using System;
using UnityEngine;

namespace GridEmpire.Commands
{
    // Minden parancs õse
    [Serializable]
    public abstract class GameCommand
    {
        public int PlayerId; // Ki küldte?
        public abstract void Execute(GridEmpire.Core.GameController context);
    }

    [Serializable]
    public class MoveUnitCommand : GameCommand
    {
        public int UnitId;      // Melyik egység?
        public int TargetQ;     // Hova? (Q koordináta)
        public int TargetR;     // Hova? (R koordináta)

        public override void Execute(GridEmpire.Core.GameController context)
        {
            // 1. Megkeressük az egységet ID alapján
            var unit = context.GetUnitById(UnitId);

            // 2. Biztonsági ellenõrzés (Server Authority)
            if (unit == null || unit.OwnerId != PlayerId)
            {
                return;
            }

            // 3. Végrehajtás
            // Itt hívjuk meg a logikát, ami eddig az InputManagerben volt
            unit.RequestMove(new Vector2Int(TargetQ, TargetR));
        }
    }

    [Serializable]
    public class SpawnUnitCommand : GameCommand
    {
        public int UnitTypeId; // Melyik egységtípus? (Index az UnitData listában)
        public int TargetQ;
        public int TargetR;

        public override void Execute(GridEmpire.Core.GameController context)
        {
            // Validáció: Van elég pénze? Szabad a hely?
            // Ha igen -> Spawn
        }
    }
}