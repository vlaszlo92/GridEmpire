using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GridEmpire.Core
{
    [CreateAssetMenu(fileName = "StatIconLibrary", menuName = "GridEmpire/Stat Icon Library")]
    public class StatIconLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public StatType type;
            public Sprite icon;
        }

        [SerializeField] private List<Entry> entries = new();
        private Dictionary<StatType, Sprite> _lookup;

        public Sprite GetIcon(StatType type)
        {
            _lookup ??= entries.ToDictionary(e => e.type, e => e.icon);
            return _lookup.TryGetValue(type, out var sprite) ? sprite : null;
        }
    }
}