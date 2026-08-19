namespace GridEmpire.Networking
{
    public static class NetworkAuthority
    {
        /// <summary>Ensures that the sender clientId actually belongs to the specified playerId
        /// based on the server-side GlobalNetworkSettings mapping.</summary>
        public static bool IsOwner(ulong senderClientId, int expectedPlayerId)
        {
            var settings = GlobalNetworkSettings.Instance;
            if (settings == null) return false;
            return settings.GetPlayerIdForClient(senderClientId) == expectedPlayerId;
        }
    }
}