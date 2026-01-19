using System;
using UnityEngine;

namespace GridEmpire.Core
{
    public enum TurnPhase
    {
        Idle,           // Várakozás, animációk futnak
        Processing,     // Számítások végzése a háttérben (Time-Slicing)
        Finalizing      // Kör lezárása, szinkronizáció
    }

    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float tickDuration = 1.0f;

        // Mennyi idõt engedünk a számításra egy frame-ben (milliszekundum)
        // 16ms = 60 FPS. Ha ebbõl 5ms-t számolunk, marad 11ms a renderelésre.
        [SerializeField] private float maxCalculationTimePerFrameMs = 5.0f;

        public float TickDuration => tickDuration;
        public int TurnCount { get; private set; } = 0;
        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Idle;

        // Progress barhoz hasznos lehet UI-on
        public float CalculationProgress { get; private set; }

        private ITurnResolver _resolver;
        private float _timer;
        private bool _isPaused;

        // Eventek
        public static event Action OnTurnCompleted; // Amikor vizuálisan is vége
        public static event Action OnProcessingStarted;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void RegisterResolver(ITurnResolver resolver) => _resolver = resolver;

        private void Update()
        {
            if (_isPaused) return;

            // 1. IDÕZÍTÕ LOGIKA
            _timer += Time.deltaTime;

            // 2. FÁZIS VEZÉRLÉS
            switch (CurrentPhase)
            {
                case TurnPhase.Idle:
                    // Ha elértük a köridõ mondjuk 10%-át, elkezdhetjük a számítást a következõ körre
                    // Vagy elkezdhetjük azonnal, ahogy az elõzõ animációk lefutottak.
                    // Most egyszerûsítve: ha a timer eléri a végét, LEJÁTSSZUK amit számoltunk,
                    // de a számítást folyamatosan végezzük közben.

                    // EBBEN A MODELLBEN: A számítás a "szünetben" történik
                    if (_resolver != null && !_resolver.IsCalculationComplete())
                    {
                        CurrentPhase = TurnPhase.Processing;
                        OnProcessingStarted?.Invoke();
                    }
                    break;

                case TurnPhase.Processing:
                    if (_resolver != null)
                    {
                        // Itt adjuk át a vezérlést a Resolvernek, de csak X milliszekundumra
                        _resolver.TickProcessing(maxCalculationTimePerFrameMs);
                        CalculationProgress = _resolver.GetProgress();

                        if (_resolver.IsCalculationComplete())
                        {
                            CurrentPhase = TurnPhase.Idle; // Vissza várakozásra, amíg lejár az 1 mp
                        }
                    }
                    break;
            }

            // 3. KÖR VÁLTÁS (Amikor letelik az 1 másodperc)
            if (_timer >= tickDuration)
            {
                if (CurrentPhase == TurnPhase.Processing)
                {
                    // VÉSZHELYZET: Ha lejárt az idõ, de még nem számoltunk ki mindent.
                    // Opció A: Kényszerítjük a befejezést (Lagspike lesz, de tartjuk a ritmust)
                    // Opció B: Várunk (Csúszik a ritmus)
                    // Profi megoldás: Opció A.
                    Debug.LogWarning("Time budget exceeded! Forcing completion.");
                    _resolver.ForceComplete();
                }

                ExecuteTurnVisuals();
                _timer = 0;
            }
        }

        private void ExecuteTurnVisuals()
        {
            TurnCount++;

            // Itt szólunk a Resolvernek, hogy "Alkalmazd az eredményeket!"
            _resolver.ApplyResults();

            // Újraindítjuk a kalkulációt a következõ körre
            _resolver.PrepareForNextTurn();

            OnTurnCompleted?.Invoke();
            CurrentPhase = TurnPhase.Idle;
            CalculationProgress = 0f;
        }

        public void SetPaused(bool paused) => _isPaused = paused;
    }
}