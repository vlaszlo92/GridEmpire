using System;
using System.Collections;
using UnityEngine;

namespace GridEmpire.Core
{
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance { get; private set; }

        [SerializeField] private float tickDuration = 1.0f;
        public float TickDuration => tickDuration; 

        private int _turnCount = 0;
        public int TurnCount => _turnCount;

        private ITurnResolver _resolver;
        private float _timer;
        private bool _isPaused;

        public static event Action OnTick;

        public static System.Action<float> OnTickDurationChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void RegisterResolver(ITurnResolver resolver) => _resolver = resolver;

        private void Update()
        {
            if (_isPaused) return;

            _timer += Time.deltaTime;
            if (_timer >= tickDuration)
            {
                _timer = 0;
                StartCoroutine(ExecuteTurnCycle());
            }
        }

        private IEnumerator ExecuteTurnCycle()
        {
            _turnCount++;
            OnTick?.Invoke();

            yield return new WaitForEndOfFrame();

            if (_resolver != null)
            {
                _resolver.ResolveAll();
            }
        }

        public void SetPaused(bool paused) => _isPaused = paused;
        public void SetTickDuration(float newDuration)
        {
            tickDuration = newDuration;
            OnTickDurationChanged?.Invoke(tickDuration);
        }
    }
}