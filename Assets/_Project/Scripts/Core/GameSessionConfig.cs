namespace GridEmpire.Core
{
    /// <summary>
    /// Sima, netcode-mentes adatátadó. A Networking réteg tölti fel
    /// (GlobalNetworkSettings alapján), a Core csak ezt ismeri.
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