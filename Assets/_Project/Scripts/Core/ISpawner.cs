using System.Collections.Generic;
using Unity.Netcode;

namespace GridEmpire.Core
{
    public interface ISpawner
    {
        int OwnerId { get; }
        void SetNetworkOwnerId(int playerId);
        void Initialize(int playerId);
        void SendSpawnRequest(int unitSlot, int targetCellId);
        IReadOnlyList<QueuedUnit> GetQueue();
        void FinalizeSpawn(QueuedUnit item);
        void AdvanceQueue();
        void SetGridManager(GridManager gridManager);
    }
}
