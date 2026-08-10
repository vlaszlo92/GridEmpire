using System;
using UnityEngine;

namespace GridEmpire.Commands
{
    // Minden parancs ose
    [Serializable]
    public abstract class GameCommand
    {
        public int PlayerId; // Ki kuldte?
        public abstract void Execute(GridEmpire.Core.GameController context);
    }

    [Serializable]
    public class MoveUnitCommand : GameCommand
    {
        public int UnitId;      // Melyik egyseg?
        public int TargetQ;     // Hova? (Q koordinata)
        public int TargetR;     // Hova? (R koordinata)

        public override void Execute(GridEmpire.Core.GameController context)
        {
            // 1. Megkeressuk az egyseget ID alapjan
            var unit = context.GetUnitById(UnitId);

            // 2. Biztonsagi ellenorzes (Server Authority)
            if (unit == null || unit.OwnerId != PlayerId)
            {
                return;
            }

            // 3. Vegrehajtas
            // Itt hivjuk meg a logikat, ami eddig az InputManagerben volt
            unit.RequestMove(new Vector2Int(TargetQ, TargetR));
        }
    }

    [Serializable]
    public class SpawnUnitCommand : GameCommand
    {
        public int UnitTypeId; // Melyik egysegtipus? (Index az UnitData listaban)
        public int TargetQ;
        public int TargetR;

        public override void Execute(GridEmpire.Core.GameController context)
        {
            // Validacio: Van eleg penze? Szabad a hely?
            // Ha igen -> Spawn
        }
    }
}