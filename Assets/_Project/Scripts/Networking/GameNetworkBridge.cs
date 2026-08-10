using GridEmpire.Core;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GridEmpire.Networking
{
    public class GameNetworkBridge : MonoBehaviour
    {
        private bool _mappingSubscribed = false;

        private void Start()
        {
            StartCoroutine(WireUpBridge());
        }

        private IEnumerator WireUpBridge()
        {
            yield return new WaitUntil(() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);

            ReadySystem.OnGameStart += HandleGameStart;
            GameController.OnLocalInitializationComplete += HandleLocalInitializationComplete;

            StartCoroutine(SubscribeToMappingsWhenReady());

            if (NetworkManager.Singleton.IsServer)
            {
                StartCoroutine(SendConfigWhenReady());
            }
            else
            {
                StartCoroutine(GenerateClientGridWhenReady());
            }
        }

        private void OnDestroy()
        {
            ReadySystem.OnGameStart -= HandleGameStart;
            GameController.OnLocalInitializationComplete -= HandleLocalInitializationComplete;

            if (_mappingSubscribed && GlobalNetworkSettings.Instance != null)
                GlobalNetworkSettings.Instance.PlayerMappings.OnListChanged -= HandleMappingsChanged;
        }

        private IEnumerator SendConfigWhenReady()
        {
            yield return new WaitUntil(() =>
                GameController.Instance != null &&
                GlobalNetworkSettings.Instance != null &&
                GlobalNetworkSettings.Instance.NetworkMapRadius.Value > 0);

            var settings = GlobalNetworkSettings.Instance;
            int totalPlayers = settings.TotalPlayers.Value;
            int aiBots = settings.TotalAIBots.Value;
            int humanCount = totalPlayers - aiBots;

            yield return new WaitUntil(() => settings.PlayerMappings.Count >= humanCount);

            settings.AssignMissingRandomColors();

            var names = new string[totalPlayers];
            var colors = new Color[totalPlayers];

            for (int playerId = 0; playerId < humanCount; playerId++)
            {
                ulong clientId = ulong.MaxValue;
                foreach (var mapping in settings.PlayerMappings)
                {
                    if (mapping.PlayerId == playerId) { clientId = mapping.ClientId; break; }
                }
                names[playerId] = settings.GetPlayerName(playerId);
                colors[playerId] = settings.GetPlayerColor(playerId);
            }

            var config = new GameSessionConfig
            {
                MapRadius = settings.NetworkMapRadius.Value,
                TotalPlayers = totalPlayers,
                TotalAIBots = aiBots,
                TurnSpeedMultiplier = settings.TurnSpeed.Value,
                FogOfWarEnabled = settings.FogOfWarEnabled.Value,
                GoldPerTurnPerCell = settings.GoldPerTurnPerCell.Value,
                PlayerNames = names,
                PlayerColors = colors
            };

            GameController.Instance.SetSessionConfig(config);
            Debug.Log("[GameNetworkBridge] Session config atadva a GameController-nek.");
        }

        private IEnumerator SubscribeToMappingsWhenReady()
        {
            yield return new WaitUntil(() =>
                GameController.Instance != null &&
                GlobalNetworkSettings.Instance != null);

            GlobalNetworkSettings.Instance.PlayerMappings.OnListChanged += HandleMappingsChanged;
            _mappingSubscribed = true;

            // Reconnect eseten megvarjuk, amig a halozati lista adatai tenylegesen megerkeznek
            while (GameController.Instance.LocalPlayerId == -1)
            {
                TryResolveLocalPlayerId();
                if (GameController.Instance.LocalPlayerId != -1) break;
                yield return null;
            }
        }

        private void HandleMappingsChanged(NetworkListEvent<PlayerClientMapping> changeEvent)
        {
            TryResolveLocalPlayerId();
        }

        private void TryResolveLocalPlayerId()
        {
            if (NetworkManager.Singleton == null || GlobalNetworkSettings.Instance == null) return;

            ulong myClientId = NetworkManager.Singleton.LocalClientId;
            int playerId = GlobalNetworkSettings.Instance.GetPlayerIdForClient(myClientId);

            if (playerId != -1)
            {
                Debug.Log($"[GameNetworkBridge] Mapping megerkezett: clientId={myClientId} → playerId={playerId}");
                GameController.Instance.TrySetLocalPlayerId(playerId);
            }
        }

        private IEnumerator GenerateClientGridWhenReady()
        {
            yield return new WaitUntil(() =>
                GridManager.Instance != null &&
                GlobalNetworkSettings.Instance != null &&
                GlobalNetworkSettings.Instance.NetworkMapRadius.Value > 0);

            if (!GridManager.Instance.IsReady)
            {
                var settings = GlobalNetworkSettings.Instance;
                GridManager.Instance.FogOfWarEnabled = settings.FogOfWarEnabled.Value;
                GridManager.Instance.GenerateGrid(settings.NetworkMapRadius.Value);
            }
        }

        private void HandleGameStart()
        {
            TurnManager.Instance?.StartGame();
        }

        private void HandleLocalInitializationComplete()
        {
            ReadySystem.Instance?.ClientReadyServerRpc(NetworkManager.Singleton.LocalClientId);
        }
    }
}