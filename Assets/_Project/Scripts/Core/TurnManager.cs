// TurnManager.cs - GridEmpire.Core namespace, Core mappa
using GridEmpire.Networking;
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
            LoadSettings();
        }

        private void OnEnable()
        {
            ReadySystem.OnGameStart += OnGameStart;
        }

        private void OnDisable()
        {
            ReadySystem.OnGameStart -= OnGameStart;
        }

        private void OnGameStart()
        {
            _gameStarted = true;
            _cachedAiCount = Mathf.Max(1,
                GameController.Instance?.Players.Count(p => p.IsAI) ?? 1);
            UnityEngine.Debug.Log("[TurnManager] Játék elindult.");
        }

        void LoadSettings()
        {
            GameSettings settings = GameSettings.Load();
            tickDuration = settings.turnSpeedMultiplier > 0f
                ? 1.0f / settings.turnSpeedMultiplier
                : tickDuration;
        }

        public void RegisterResolver(ITurnResolver resolver) => _resolver = resolver;

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
                ApplySnapshotClientRpc(json);
            }

            CurrentPhase = TurnPhase.Idle;
            CalculationProgress = 0f;
        }

        [ClientRpc]
        private void ApplySnapshotClientRpc(string json)
        {
            if (IsServer) return;
            var snapshot = JsonUtility.FromJson<TurnSnapshot>(json);
            ApplySnapshot(snapshot);
        }

        private void ApplySnapshot(TurnSnapshot snapshot)
        {
            var gridManager = FindFirstObjectByType<GridManager>();
            if (gridManager == null) return;

            foreach (var unitSync in snapshot.UnitActions)
            {
                var unit = GameController.Instance.GetUnitById(unitSync.UnitId);
                unit?.SyncFromSnapshot(unitSync.NewHP, unitSync.IsDead);
            }

            foreach (var playerSync in snapshot.PlayerStats)
            {
                var player = GameController.Instance.GetPlayerById(playerSync.PlayerId);
                if (player == null) continue;
                player.SetGold(playerSync.CurrentGold);
            }

            var localPlayer = GameController.Instance.GetLocalPlayer();
            if (localPlayer != null) gridManager.UpdateFogOfWar(localPlayer.Id);

            TurnCount = snapshot.TurnIndex;
            OnTurnCompleted?.Invoke();
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