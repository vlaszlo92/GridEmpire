using UnityEngine;
using System.IO;

namespace GridEmpire.Shared
{
    public static class GameSettingsStorage
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "game_settings.json");

        public static GameSettings Load()
        {
            if (File.Exists(SavePath))
            {
                string json = File.ReadAllText(SavePath);
                return JsonUtility.FromJson<GameSettings>(json);
            }

            return new GameSettings();
        }

        public static void Save(GameSettings settings)
        {
            string json = JsonUtility.ToJson(settings, true);
            File.WriteAllText(SavePath, json);
        }
    }
}