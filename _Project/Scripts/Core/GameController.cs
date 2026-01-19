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
        [SerializeField] private int aiCount = 2;
        [SerializeField] private Color[] playerColors;
        [SerializeField] private GameObject aiPrefab;

        [SerializeField] private List<PlayerProfile> _players = new List<PlayerProfile>();
        public IReadOnlyList<PlayerProfile> Players => _players;

        public static System.Action<IUnit> OnUnitSelected;
        public static System.Action OnUnitRemoved;

        private IUnit _selectedUnit;
        public IUnit SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                _selectedUnit = value;
                OnUnitSelected?.Invoke(_selectedUnit);
            }
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            InitializePlayers();
        }

        private void OnEnable()
        {
            TurnManager.OnTurnCompleted += ProcessEconomy;
            CellData.OnCellOwnerChanged += HandleCellOwnershipChange;
        }

        private void OnDisable()
        {
            TurnManager.OnTurnCompleted -= ProcessEconomy;
            CellData.OnCellOwnerChanged -= HandleCellOwnershipChange;
        }

        /// <summary>
        /// Ez a metódus frissíti a cache-elt OwnedCellCount értéket és az ebbõl fakadó jövedelmet.
        /// </summary>
        private void HandleCellOwnershipChange(int fromPlayer, int toPlayer)
        {
            // Levonjuk a régitõl
            var oldOwner = GetPlayerById(fromPlayer);
            if (oldOwner != null)
            {
                oldOwner.OwnedCellCount--;
                RefreshPlayerIncome(oldOwner);
            }

            // Hozzáadjuk az újhoz
            var newOwner = GetPlayerById(toPlayer);
            if (newOwner != null)
            {
                newOwner.OwnedCellCount++;
                RefreshPlayerIncome(newOwner);
            }
        }

        /// <summary>
        /// Újraszámolja a játékos jövedelmét az aktuális adatok alapján.
        /// Ezt hívjuk meg minden olyan eseménynél, ami befolyásolja a gazdaságot.
        /// </summary>
        public void RefreshPlayerIncome(PlayerProfile player)
        {
            if (player == null) return;

            // Fenntartási költség összege (IUnit interfészen keresztül)
            float unitMaintenance = player.ActiveUnits.Sum(u => u.Data.costPerTurn);

            // Képlet: Alap (1) + (Mezõk száma * 0.1) - Egységek költsége
            player.GoldIncome = Mathf.Max(0, 1 + (player.OwnedCellCount * 0.1f) - unitMaintenance);
        }

        /// <summary>
        /// A kör végén lefutó gazdasági frissítés. 
        /// Itt már nincs súlyos számítás, csak a korábban kiszámolt jövedelmet adjuk hozzá.
        /// </summary>
        private void ProcessEconomy()
        {
            foreach (var player in _players.Where(p => p.IsAlive))
            {
                player.Gold += player.GoldIncome;
            }
        }

        private void InitializePlayers()
        {
            _players.Clear();
            for (int i = 0; i < playerCount; i++)
            {
                bool isAi = i >= (playerCount - aiCount);
                bool isLocal = (i == 0 && !isAi);

                Color pColor = i < playerColors.Length ? playerColors[i] : Color.white;
                _players.Add(new PlayerProfile(i, $"Player {i}", pColor, isAi, isLocal, null));

                if (isAi && aiPrefab != null)
                {
                    GameObject aiObj = Instantiate(aiPrefab);
                    aiObj.name = $"AI_Player_{i}";
                    aiObj.SendMessage("Initialize", i, SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        public void RemoveUnit(IUnit unit)
        {
            if (unit == null) return;

            // Kijelölés kezelése
            if (_selectedUnit == unit)
            {
                SelectedUnit = null;
            }

            var player = GetPlayerById(unit.OwnerId);
            if (player != null)
            {
                player.ActiveUnits.Remove(unit);
                RefreshPlayerIncome(player);
            }

            OnUnitRemoved?.Invoke();
        }

        // Segédfüggvények a kényelmes eléréshez
        public PlayerProfile GetLocalPlayer() => _players.FirstOrDefault(p => p.IsLocalPlayer);
        public PlayerProfile GetPlayerById(int id) => _players.FirstOrDefault(p => p.Id == id);
        public List<PlayerProfile> GetPlayers() => _players;
    }
}