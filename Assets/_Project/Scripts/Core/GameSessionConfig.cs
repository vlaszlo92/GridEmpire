namespace GridEmpire.Core
{
    /// <summary>
    /// Sima, netcode-mentes adatatado. A Networking reteg tolti fel
    /// (GlobalNetworkSettings alapjan), a Core csak ezt ismeri.
    /// </summary>
    public class GameSessionConfig
    {
        public int MapRadius;
        public int TotalPlayers;
        public int TotalAIBots;
        public float TurnSpeedMultiplier;
        public bool FogOfWarEnabled;
    }
}