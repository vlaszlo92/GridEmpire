using UnityEngine;
using GridEmpire.Core;
using Unity.Netcode;
using Debug = UnityEngine.Debug;

namespace GridEmpire.AI
{
    [RequireComponent(typeof(NetworkObject))]
    // [RequireComponent(typeof(ISpawner))] // Az ISpawner nem komponens, így ezt vedd le
    public class SimpleAI : NetworkBehaviour
    {
        private int _aiPlayerId;
        private ISpawner _mySpawner; // UnitSpawner helyett az interfészt használjuk!
        private bool _isInitialized = false;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                this.enabled = false;
                return;
            }
            //Debug.Log("SimpleAI OnNetworkSpawn: Szerver oldalon inicializálás...");

            // Az ISpawner interfészt keressük meg a rajta lévõ UnitSpawner-en keresztül
            _mySpawner = GetComponent<ISpawner>();

            if (_aiPlayerId != 0 && _mySpawner != null)
            {
                _mySpawner.Initialize(_aiPlayerId);
            }
        }

        public void Initialize(int playerId)
        {
            _aiPlayerId = playerId;
            _mySpawner = GetComponent<ISpawner>();

            if (_mySpawner != null)
            {
                _mySpawner.Initialize(playerId);
            }

            _isInitialized = true;
        }

        private void OnEnable() => TurnManager.OnTurnCompleted += ExecuteTurn;
        private void OnDisable() => TurnManager.OnTurnCompleted -= ExecuteTurn;

        private void ExecuteTurn()
        {
            if (!IsServer || !_isInitialized || _mySpawner == null) return;

            if (!IsSpawned) return;

            DecideAndAct();
        }

        private void DecideAndAct()
        {
            if (_mySpawner == null) return;
            var profile = GameController.Instance?.GetPlayerById(_aiPlayerId);
            if (profile == null || profile.Gold < 30) return; // 30 = legolcsóbb egység ára
            _mySpawner.SendSpawnRequest(0, -1); // mindig axemant kér
        }

        private void TryRequestUnit(int slot)
        {
            if (_mySpawner == null) return;

            // Az ISpawner interfészen keresztül hívjuk a kérést. 
            // Mivel a SimpleAI csak a szerveren fut, ez a UnitSpawner.SendSpawnRequest-en 
            // belül az IsServer ágra fog futni -> azonnali RequestUnit().
            _mySpawner.SendSpawnRequest(slot, -1);

            //Debug.Log($"AI {_aiPlayerId} elküldte a toborzási kérést a(z) {slot}. slotra.");
        }
    }
}