using System.Numerics;
using UnityEngine;

namespace GridEmpire.Core
{
    public interface IUnit
    {
        int Id { get; }
        int OwnerId { get; }
        UnitData Data { get; }
        CellData CurrentCell { get; }
        bool IsDead { get; }
        void RequestMove(Vector2Int targetPos);
        void RequestMove(CellData target);
        void DestroyUnit();
        float GetCurrentHP();
        void SyncFromSnapshot(float newHp, bool isDead); 
    }
}