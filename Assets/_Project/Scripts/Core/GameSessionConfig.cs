using UnityEngine;

namespace GridEmpire.Core
{
    public class GameSessionConfig
    {
        public int MapRadius;
        public int TotalPlayers;
        public int TotalAIBots;
        public float TurnSpeedMultiplier;
        public bool FogOfWarEnabled;
        public float GoldPerTurnPerCell;
        public string[] PlayerNames;
        public Color[] PlayerColors;
    }
}