using System.Text;
using GridEmpire.Shared;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

namespace GridEmpire.Networking
{
    /// <summary>
    /// Egyszeri, kikapcsolhato diagnosztikai dump: kiirja MINDEN elerheto session/halozati
    /// adatot a Start gomb megnyomasanak pillanataban, szerver es kliens oldalon egyarant.
    /// Kikapcsolas: NetworkDebugDump.Enabled = false;
    /// </summary>
    public static class NetworkDebugDump
    {
        public static bool Enabled = true;

        public static void DumpServerState(GameSettings settings, string sceneName, int expectedHumans)
        {
            if (!Enabled) return;

            var sb = new StringBuilder();
            sb.AppendLine("========== [SESSION_DEBUG_DUMP] SZERVER — Start gomb megnyomva ==========");

            sb.AppendLine("--- StartHostGame parameterek (nyers GameSettings, meg nem halozati) ---");
            sb.AppendLine($"  totalPlayers={settings.totalPlayers}, aiBots={settings.aiBots}, mapRadius={settings.mapRadius}, turnSpeedMultiplier={settings.turnSpeedMultiplier}, fogOfWarEnabled={settings.fogOfWarEnabled}");
            sb.AppendLine($"  sceneName={sceneName}, expectedHumans={expectedHumans}");

            AppendGlobalNetworkSettings(sb);
            AppendNetworkManagerState(sb);
            AppendAuthState(sb);

            sb.AppendLine("========== [SESSION_DEBUG_DUMP] VeGE (szerver) ==========");
            Debug.Log(sb.ToString());
        }

        public static void DumpClientState()
        {
            if (!Enabled) return;

            var sb = new StringBuilder();
            sb.AppendLine("========== [SESSION_DEBUG_DUMP] KLIENS — Start jel megerkezett ==========");

            AppendGlobalNetworkSettings(sb);
            AppendNetworkManagerState(sb);
            AppendAuthState(sb);

            sb.AppendLine("========== [SESSION_DEBUG_DUMP] VeGE (kliens) ==========");
            Debug.Log(sb.ToString());
        }

        private static void AppendGlobalNetworkSettings(StringBuilder sb)
        {
            sb.AppendLine("--- GlobalNetworkSettings (NetworkVariable allapot, ahogy ITT latszik) ---");
            var gns = GlobalNetworkSettings.Instance;
            if (gns == null)
            {
                sb.AppendLine("  GlobalNetworkSettings.Instance == NULL!");
                return;
            }

            sb.AppendLine($"  NetworkMapRadius={gns.NetworkMapRadius.Value}");
            sb.AppendLine($"  TotalPlayers={gns.TotalPlayers.Value}");
            sb.AppendLine($"  TotalAIBots={gns.TotalAIBots.Value}");
            sb.AppendLine($"  TurnSpeed={gns.TurnSpeed.Value}");
            sb.AppendLine($"  ConnectedPlayerCount={gns.ConnectedPlayerCount.Value}");
            sb.AppendLine($"  FogOfWarEnabled={gns.FogOfWarEnabled.Value}");

            var mappings = gns.PlayerMappings;
            sb.AppendLine($"  PlayerMappings (count={(mappings != null ? mappings.Count : -1)}):");
            if (mappings != null)
            {
                foreach (var m in mappings)
                    sb.AppendLine($"    ClientId={m.ClientId}, PlayerId={m.PlayerId}, AuthId={m.AuthId}");
            }
        }

        private static void AppendNetworkManagerState(StringBuilder sb)
        {
            sb.AppendLine("--- NetworkManager allapot (ITT, errol a geprol) ---");
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                sb.AppendLine("  NetworkManager.Singleton == NULL!");
                return;
            }

            sb.AppendLine($"  LocalClientId={nm.LocalClientId}");
            sb.AppendLine($"  IsServer={nm.IsServer}, IsHost={nm.IsHost}, IsClient={nm.IsClient}, IsListening={nm.IsListening}");

            if (nm.IsServer)
                sb.AppendLine($"  ConnectedClientsIds ({nm.ConnectedClientsIds.Count}): {string.Join(", ", nm.ConnectedClientsIds)}");
            else
                sb.AppendLine("  ConnectedClientsIds: (klienseknek nincs teljes ralatasa, csak a szervernek)");
        }

        private static void AppendAuthState(StringBuilder sb)
        {
            sb.AppendLine("--- UGS Authentication (ITT, errol a geprol) ---");
            if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
                sb.AppendLine($"  Sajat AuthId={AuthenticationService.Instance.PlayerId}");
            else
                sb.AppendLine("  Nincs bejelentkezve UGS-be!");
        }
    }
}