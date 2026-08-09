using System.IO;
using UnityEngine;

namespace GridEmpire.Core
{
    public static class UnitStatsStorage
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "unit_stats.json");

        public static UnitStatsCollection Load()
        {
            if (!File.Exists(SavePath)) return null;
            return JsonUtility.FromJson<UnitStatsCollection>(File.ReadAllText(SavePath));
        }

        public static void Save(UnitStatsCollection collection)
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(collection, true));
        }
    }
}