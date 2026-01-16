using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace GridEmpire.Core
{
    public class GameController : MonoBehaviour
    {
        public static GameController Instance { get; private set; }

        [Header("Game Settings")]
        [Range(2, 6)]
        [SerializeField] private int playerCount = 2;
        [SerializeField] private int aiCount = 1;
        [SerializeField] private Color[] playerColors;

        [SerializeField] public List<PlayerProfile> _players = new List<PlayerProfile>();
        public IReadOnlyList<PlayerProfile> Players => _players;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            InitializePlayers();
        }

        private void OnEnable() => TurnManager.OnTick += ProcessEconomy;
        private void OnDisable() => TurnManager.OnTick -= ProcessEconomy;

        private void InitializePlayers()
        {
            // Példa: 1 ember (Local), a többi AI
            for (int i = 0; i < playerCount; i++)
            {
                bool isAi = i >= (playerCount - aiCount);
                bool isLocal = (i == 0); // Teszteléshez a 0. játékos az ember

                Color pColor = i < playerColors.Length ? playerColors[i] : Color.white;

                _players.Add(new PlayerProfile(i, $"Player {i}", pColor, isAi, isLocal, null));
            }
            Debug.Log($"{_players.Count} játékos inicializálva.");
        }

        private void ProcessEconomy()
        {
            // Áthelyezett Economy logika: Mindenki kap aranyat a területei után
            var gridManager = Object.FindFirstObjectByType<GridManager>();
            if (gridManager == null) return;

            var allCells = gridManager.GetAllCells();

            foreach (var player in _players.Where(p => p.IsAlive))
            {
                int ownedCells = allCells.Count(c => c.OwnerId == player.Id);
                float income = 1 + (ownedCells * 0.1f);
                player.Gold += income;
            }
        }

        public PlayerProfile GetLocalPlayer() => _players.FirstOrDefault(p => p.IsLocalPlayer);
        public PlayerProfile GetPlayerById(int id) => _players.FirstOrDefault(p => p.Id == id);
    }
}