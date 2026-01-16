using GridEmpire.Core;
using GridEmpire.Gameplay;
using GridEmpire.Input;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GridEmpire.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("Global References")]
        [SerializeField] private UnitSpawner spawner;

        [Header("Resources & Info")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI turnText;

        [Header("Spawn Buttons")]
        [SerializeField] private Button axemanBtn;
        [SerializeField] private Button spearmanBtn;
        [SerializeField] private Button cavalryBtn;

        [Header("Queue UI References")]
        [SerializeField] private Transform queueContainer;
        [SerializeField] private GameObject queueIconPrefab;

        private class QueueIconRefs
        {
            public GameObject root;
            public Image fillImage;
            public TextMeshProUGUI tickText;
        }

        private List<QueueIconRefs> _iconRefs = new List<QueueIconRefs>();
        private float _currentTickRate = 1f;
        private float _visualFillAmount = 0f;
        private float _tickTimer = 0f;

        private void Awake()
        {
            if (TurnManager.Instance != null)
                _currentTickRate = TurnManager.Instance.TickDuration;
        }

        private void Start()
        {
            // A gombok most már a UnitSpawner új metódusát hívják (RequestUnit)
            // Itt feltételezzük, hogy az UI mindig a LocalPlayer (ID 0) adatait kezeli
            axemanBtn.onClick.AddListener(() => RequestSpawn(0)); // 0 = Axeman slot vagy típus
            spearmanBtn.onClick.AddListener(() => RequestSpawn(1)); // 1 = Spearman
            cavalryBtn.onClick.AddListener(() => RequestSpawn(2)); // 2 = Cavalry
        }

        private void OnEnable()
        {
            TurnManager.OnTickDurationChanged += HandleTickRateChange;
            TurnManager.OnTick += ResetVisualTimer;
        }

        private void OnDisable()
        {
            TurnManager.OnTickDurationChanged -= HandleTickRateChange;
            TurnManager.OnTick -= ResetVisualTimer;
        }

        private void HandleTickRateChange(float rate) => _currentTickRate = rate;

        private void ResetVisualTimer()
        {
            // Minden kör elején nullázzuk a belsõ UI idõzítõt
            _tickTimer = 0f;
        }

        private void Update()
        {
            _tickTimer += Time.deltaTime;
            _tickTimer = Mathf.Min(_tickTimer, _currentTickRate);

            // 1. Adatok lekérése a GameControllerbõl (GridManager helyett)
            var localPlayer = GameController.Instance?.GetLocalPlayer();
            if (localPlayer != null)
            {
                goldText.text = $"Gold: {(int)localPlayer.Gold}";
            }

            if (TurnManager.Instance != null)
                turnText.text = $"Turn: {TurnManager.Instance.TurnCount}";

            // 2. Ikonok szinkronizálása a megfelelõ játékos sorával
            if (localPlayer != null)
            {
                SyncQueueIcons(localPlayer.Id);
                HandleSmoothFill(localPlayer.Id);
            }
        }

        private void RequestSpawn(int unitSlot)
        {            
            // Megkérdezzük a GameControllert, ki az aktuális emberi játékos
            var localPlayer = GameController.Instance.GetLocalPlayer();

            if (localPlayer != null)
            {
                // Most már két paramétert küldünk: (playerId, unitSlot)
                UnitSpawner.OnRequestUnitSpawn?.Invoke(localPlayer.Id, unitSlot, localPlayer.SelectedCell);
            }
            else
            {
                Debug.LogError("UIManager: Nem található LocalPlayer!");
            }
        }
        private void SyncQueueIcons(int playerId)
        {
            // A spawner-tõl lekérjük az adott játékos sorát
            var queue = spawner.GetQueueForPlayer(playerId);
            int displayCount = Mathf.Min(queue.Count, 7);

            // Törlés, ha rövidült a sor
            while (_iconRefs.Count > displayCount)
            {
                Destroy(_iconRefs[_iconRefs.Count - 1].root);
                _iconRefs.RemoveAt(_iconRefs.Count - 1);
                if (_iconRefs.Count == 0) _visualFillAmount = 0f;
            }

            // Létrehozás, ha nõtt a sor
            while (_iconRefs.Count < displayCount)
            {
                GameObject newIcon = Instantiate(queueIconPrefab, queueContainer);
                _iconRefs.Add(new QueueIconRefs
                {
                    root = newIcon,
                    fillImage = newIcon.transform.GetChild(0).GetComponent<Image>(),
                    tickText = newIcon.GetComponentInChildren<TextMeshProUGUI>()
                });
            }

            // Adatok frissítése
            for (int i = 0; i < _iconRefs.Count; i++)
            {
                _iconRefs[i].fillImage.color = GetUnitColor(queue[i].data.unitName);
                _iconRefs[i].tickText.text = queue[i].remainingTicks.ToString();
            }
        }

        private void HandleSmoothFill(int playerId)
        {
            var queue = spawner.GetQueueForPlayer(playerId);
            if (queue.Count == 0 || _iconRefs.Count == 0)
            {
                _visualFillAmount = 0f;
                return;
            }

            float totalTicks = (float)queue[0].data.recruitmentTime;
            float remainingTicks = (float)queue[0].remainingTicks;

            // Kiszámoljuk a már lezárt körök arányát
            // Ha 2 kör a max és 2 van hátra, akkor 0 kör kész.
            // Ha 2 kör a max és 1 van hátra, akkor 1 kör kész.
            float completedTicks = totalTicks - remainingTicks;
            float baseFill = completedTicks / totalTicks;

            // Kiszámoljuk az aktuális körön belüli haladást (0.0 és 1.0 között)
            float currentTickProgress = _tickTimer / _currentTickRate;

            // A teljes kitöltöttség: az eddigi körök + az aktuális körbõl arányosan ennyi
            // (1f / totalTicks) az egy körhöz tartozó szelet a teljes csíkon
            _visualFillAmount = baseFill + (currentTickProgress * (1f / totalTicks));

            _visualFillAmount = Mathf.Clamp01(_visualFillAmount);
            _iconRefs[0].fillImage.fillAmount = _visualFillAmount;

            for (int i = 1; i < _iconRefs.Count; i++)
                _iconRefs[i].fillImage.fillAmount = 0f;
        }

        private Color GetUnitColor(string unitName)
        {
            if (string.IsNullOrEmpty(unitName)) return Color.white;
            if (unitName.Contains("Axeman")) return new Color(0.8f, 0.2f, 0.2f); // Pirosas
            if (unitName.Contains("Spearman")) return new Color(0.8f, 0.8f, 0.2f); // Sárgás
            if (unitName.Contains("Cavalry")) return new Color(0.2f, 0.2f, 0.8f); // Kékes
            return Color.white;
        }
    }
}