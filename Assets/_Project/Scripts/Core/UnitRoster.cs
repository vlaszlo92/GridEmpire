using System.Collections.Generic;
using UnityEngine;

namespace GridEmpire.Core
{
    [CreateAssetMenu(fileName = "NewUnitRoster", menuName = "GridEmpire/Unit Roster")]
    public class UnitRoster : ScriptableObject
    {
        [SerializeField] private List<UnitData> units = new();

        public IReadOnlyList<UnitData> Units => units;
        
        public UnitData GetBySlot(int slot) =>
            (slot >= 0 && slot < units.Count) ? units[slot] : null;

        public int GetSlotIndex(UnitData data) => units.IndexOf(data);
    }
}