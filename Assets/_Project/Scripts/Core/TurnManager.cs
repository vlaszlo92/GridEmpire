using GridEmpire.Shared;
using System;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace GridEmpire.Core
{
    public enum TurnPhase { Idle, Processing, Finalizing }

    public class TurnManager : NetworkBehaviour
    {
        public static TurnManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float tickDuration = 1.0f;

        [Header("Dynamic Budget Settings")]
        [SerializeField] private float defaultMaxCalculationTimePerFrameMs = 5.0f;
        [Range(0.1f, 1.0f)][SerializeField] private float budgetFraction = 0.6f;
        [SerializeField] private float minCalculationTimePerFrameMs = 0.5f;
        [Range(0.1f, 1.0f)][SerializeField] private float maxCalculationTimeCapFraction = 0.9f;
        [SerializeField] private float warningRateLimitSeconds = 1.0f;

        public float TickDuration => tickDuration;
        public int TurnCount { get; private set; } = 0;
        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Idle;
        public float CalculationProgress { get; private set; }

        private GridManager _gridManager;
        private ITurnResolver _resolver;
        private float _timer;
        private bool _isPaused;
        private bool _gameStarted = false;
        private float _lastWarningTime = -999f;
        private int _cachedAiCount = 1;

        public static event Action OnTurnCompleted;
        public static event Action OnProcessingStarted;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
            _gridManager = FindFirstObjectByType<GridManager>();
            LoadSettings();
        }

        void LoadSettings()
        {
            GameSettings settings = GameSettingsStorage.Load();
            tickDuration = settings.turnSpeedMultiplier > 0f
                ? 1.0f / settings.turnSpeedMultiplier
                : tickDuration;
        }

        public void RegisterResolver(ITurnResolver resolver) => _resolver = resolver;

        /// <summary>A Networking réteg hívja (ReadySystem.OnGameStart-ból), amikor mindenki ready.</summary>
        public void StartGame()
        {
            _gameStarted = true;
            _cachedAiCount = Mathf.Max(1,
                GameController.Instance?.Players.Count(p => p.IsAI) ?? 1);
            Debug.Log("[TurnManager] Játék elindult.");
        }

        private void Update()
        {
            if (!IsServer || !_gameStarted || _isPaused) return;

            _timer += Time.deltaTime;

            switch (CurrentPhase)
            {
                case TurnPhase.Idle:
                    if (_resolver != null && !_resolver.IsCalculationComplete())
                    {
                        CurrentPhase = TurnPhase.Processing;
                        OnProcessingStarted?.Invoke();
                    }
                    break;

                case TurnPhase.Processing:
                    if (_resolver != null)
                    {
                        _resolver.TickProcessing(ComputeDynamicBudgetMs());
                        CalculationProgress = _resolver.GetProgress();
                        if (_resolver.IsCalculationComplete())
                            CurrentPhase = TurnPhase.Idle;
                    }
                    break;
            }

            if (_timer >= tickDuration)
            {
                if (CurrentPhase == TurnPhase.Processing)
                {
                    if (Time.unscaledTime - _lastWarningTime >= warningRateLimitSeconds)
                        _lastWarningTime = Time.unscaledTime;
                    _resolver?.ForceComplete();
                }

                ExecuteTurnVisuals();
                _timer = 0;
            }
        }

        private void ExecuteTurnVisuals()
        {
            TurnCount++;
            _resolver?.ApplyResults();
            _resolver?.PrepareForNextTurn();
            OnTurnCompleted?.Invoke();

            if (IsServer && _resolver != null)
            {
                var snapshot = _resolver.BuildSnapshot(TurnCount);
                string json = JsonUtility.ToJson(snapshot);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                StartCoroutine(DelayedSnapshotSend(json));
#else
        ApplySnapshotClientRpc(json);
#endif
            }

            CurrentPhase = TurnPhase.Idle;
            CalculationProgress = 0f;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private IEnumerator DelayedSnapshotSend(string json)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.05f, 0.25f));
            ApplySnapshotClientRpc(json);
        }
#endif

        [ClientRpc]
        private void ApplySnapshotClientRpc(string json)
        {
            if (IsServer) return;
            var snapshot = JsonUtility.FromJson<TurnSnapshot>(json);
            ApplySnapshot(snapshot);
        }

        private void ApplySnapshot(TurnSnapshot snapshot)
        {
            if (_gridManager == null)
            {
                _gridManager = FindFirstObjectByType<GridManager>();
                return;
            }

            foreach (var unitSync in snapshot.UnitActions)
            {
                var unit = GameController.Instance.GetUnitById(unitSync.UnitId);
                unit?.SyncFromSnapshot(unitSync.NewHP, unitSync.NewStamina, unitSync.IsDead);
            }

            foreach (var playerSync in snapshot.PlayerStats)
            {
                var player = GameController.Instance.GetPlayerById(playerSync.PlayerId);
                if (player == null) continue;
                player.SyncGold(playerSync.CurrentGold);
            }

            TurnCount = snapshot.TurnIndex;
            OnTurnCompleted?.Invoke();

            StartCoroutine(DelayedFogUpdate(_gridManager));
        }

        private IEnumerator DelayedFogUpdate(GridManager gridManager)
        {
            yield return null;
            var localPlayer = GameController.Instance.GetLocalPlayer();
            if (localPlayer != null) gridManager.UpdateFogOfWar(localPlayer.Id);
        }

        public void SetPaused(bool paused) => _isPaused = paused;

        public float ComputeDynamicBudgetMs()
        {
            float tickMs = TickDuration > 0f ? TickDuration * 1000f : 1000f / 60f;
            float rawPerFrame = (tickMs * budgetFraction) / _cachedAiCount;
            float cap = tickMs * maxCalculationTimeCapFraction;
            float computed = Mathf.Clamp(rawPerFrame, minCalculationTimePerFrameMs, cap);
            if (float.IsNaN(computed) || computed <= 0f) computed = defaultMaxCalculationTimePerFrameMs;
            return computed;
        }
    }
}