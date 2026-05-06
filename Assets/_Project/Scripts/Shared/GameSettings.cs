using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace GridEmpire.Shared
{

    [System.Serializable]
    public class GameSettings
    {
        public int totalPlayers = 2;
        public int aiBots = 1;
        public float turnSpeedMultiplier = 1.0f;
        public int mapRadius = 15;
        public bool fogOfWarEnabled = true;

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

        public void Save()
        {
            string json = JsonUtility.ToJson(this, true);
            File.WriteAllText(SavePath, json);
        }
    }
}