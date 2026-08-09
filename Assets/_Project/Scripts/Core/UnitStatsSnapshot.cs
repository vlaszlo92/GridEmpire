using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GridEmpire.Core
{
    [Serializable]
    public class UnitFieldValue
    {
        public string fieldName;
        public float value;
    }

    [Serializable]
    public class UnitStatsEntry
    {
        public int unitIndex;
        public List<UnitFieldValue> fields;
    }

    [Serializable]
    public class UnitStatsCollection
    {
        public List<UnitStatsEntry> units = new List<UnitStatsEntry>();
    }

    public static class UnitStatsSnapshotUtil
    {
        public static UnitStatsCollection Collect(IEnumerable<UnitData> unitDataList)
        {
            var collection = new UnitStatsCollection();
            foreach (var data in unitDataList)
            {
                if (data == null) continue;
                var entry = new UnitStatsEntry { unitIndex = data.index, fields = new List<UnitFieldValue>() };
                foreach (var field in UnitDataFieldUtil.GetEditableFields(data))
                    entry.fields.Add(new UnitFieldValue { fieldName = field.Name, value = UnitDataFieldUtil.GetValue(data, field) });
                collection.units.Add(entry);
            }
            return collection;
        }

        public static void Apply(IEnumerable<UnitData> unitDataList, UnitStatsCollection collection)
        {
            if (collection?.units == null) return;
            var byIndex = unitDataList.Where(d => d != null).ToDictionary(d => d.index);
            foreach (var entry in collection.units)
            {
                if (!byIndex.TryGetValue(entry.unitIndex, out var data)) continue;
                var fieldsByName = UnitDataFieldUtil.GetEditableFields(data).ToDictionary(f => f.Name);
                foreach (var fv in entry.fields)
                    if (fieldsByName.TryGetValue(fv.fieldName, out var field))
                        UnitDataFieldUtil.SetValue(data, field, fv.value);
            }
        }

        public static List<(int unitIndex, string fieldName)> Diff(UnitStatsCollection oldCollection, UnitStatsCollection newCollection)
        {
            var changed = new List<(int, string)>();
            if (newCollection?.units == null) return changed;

            var oldLookup = new Dictionary<(int, string), float>();
            if (oldCollection?.units != null)
            {
                foreach (var entry in oldCollection.units)
                    foreach (var fv in entry.fields)
                        oldLookup[(entry.unitIndex, fv.fieldName)] = fv.value;
            }

            foreach (var entry in newCollection.units)
            {
                foreach (var fv in entry.fields)
                {
                    var key = (entry.unitIndex, fv.fieldName);
                    if (!oldLookup.TryGetValue(key, out float oldVal) || !Mathf.Approximately(oldVal, fv.value))
                        changed.Add(key);
                }
            }
            return changed;
        }
    }
}