using System.Collections.Generic;
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
        float GetCurrentStamina();
        void SyncFromSnapshot(float newHp, float newStamina, bool isDead);
        void SetVisible(bool visible);
        void SetAudioVisible(bool visible);
        void SyncToAuthoritativeState();
        EffectiveUnitStats Stats { get; }
        void RefreshNetworkVisibility(GridManager gridManager, IReadOnlyList<PlayerProfile> players);
    }
}