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
        public float goldPerTurnPerCell = 0.1f;
    }
}