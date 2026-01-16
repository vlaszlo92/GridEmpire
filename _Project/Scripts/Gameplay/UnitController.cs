using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using GridEmpire.Core;

namespace GridEmpire.Gameplay
{
    public class UnitController : MonoBehaviour, IUnit
    {
        [Header("Unit State")]
        public UnitData _data;
        public int _ownerId; 
        public bool _isDead = false;
        public bool isInCombat = false;
        public CellData _currentCell;
        public UnitController _combatTarget;

        private GridManager _gridManager;
        private TurnResolver _resolver;
        private List<CellData> _initialPath;
        private float _currentHP;
        private float _pendingDamage;
        private int _currentDirection = 0;

        // --- IUnit Interfész implementáció ---
        public int OwnerId => _ownerId;
        public UnitData Data => _data;
        public CellData CurrentCell => _currentCell;
        public bool IsDead => _isDead;
        public void DestroyUnit() => ExecuteDeath();
        // --------------------------------------

        public void Initialize(UnitData data, List<CellData> path, GridManager gm, int ownerId)
        {
            _data = data;
            _gridManager = gm;
            _ownerId = ownerId;
            _currentHP = data.maxHp;
            _resolver = Object.FindFirstObjectByType<TurnResolver>();
            _initialPath = path != null ? new List<CellData>(path) : new List<CellData>();

            // Regisztráció a játékos profiljába
            var player = GameController.Instance?.GetPlayerById(ownerId);
            player?.ActiveUnits.Add(this);

            if (_initialPath.Count > 0) _currentCell = _initialPath[0];
            else _currentCell = _gridManager.GetCellAtPosition(transform.position);

            if (_currentCell != null) _currentCell.RegisterOccupier(this);

            TurnManager.OnTick += HandleTick;
        }

        public void SetStartingDirection(int dir)
        {
            _currentDirection = dir;
            // Azonnali vizuális fordítás a kezdõ irányba
            Vector3 targetPos = _gridManager.GetWorldPosition(_currentCell.Q, _currentCell.R) + GetDirectionVector(dir);
            FaceTarget(targetPos);
        }

        private Vector3 GetDirectionVector(int dir)
        {
            // Segédmetódus a kezdõ irányba nézéshez
            float angle = dir * 60f * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
        }

        private void HandleTick()
        {
            if (_isDead || _resolver == null) return;

            UnitAction decision = new UnitAction { Performer = this };
            UnitController nearbyEnemy = ScanForEnemies();

            if (nearbyEnemy != null)
            {
                decision.Type = ActionType.Attack;
                decision.TargetUnit = nearbyEnemy;
                _combatTarget = nearbyEnemy;
            }
            else if (_initialPath.Count > 1)
            {
                CellData next = _initialPath[1];
                decision.Type = (next.OwnerId == _ownerId) ? ActionType.Move : ActionType.Capture;
                decision.TargetCell = next;
            }
            else
            {
                // Automata terjeszkedés (ha elfogyott az út)
                CellData nextInDir = _gridManager.GetNeighborInDirection(_currentCell, _currentDirection);

                // Ha falba ütközik vagy foglalt mezõre érne, fordul
                if (nextInDir == null || (nextInDir.OwnerId == _ownerId && nextInDir.IsOccupied))
                {
                    _currentDirection = (_currentDirection + 1) % 6;
                    nextInDir = _gridManager.GetNeighborInDirection(_currentCell, _currentDirection);
                }

                if (nextInDir != null)
                {
                    decision.Type = (nextInDir.OwnerId == _ownerId) ? ActionType.Move : ActionType.Capture;
                    decision.TargetCell = nextInDir;
                }
            }
            _resolver.EnqueueAction(decision);
        }

        public void ExecuteFinalMove(CellData next)
        {
            if (_currentCell != null) _currentCell.UnregisterOccupier(this);
            _currentCell = next;
            _currentCell.RegisterOccupier(this);
            if (_initialPath.Count > 0) _initialPath.RemoveAt(0);

            StopAllCoroutines();
            StartCoroutine(Animate(next));
        }

        private IEnumerator Animate(CellData c)
        {
            Vector3 startPos = transform.position;
            Vector3 targetPos = _gridManager.GetWorldPosition(c.Q, c.R);
            Quaternion startRot = transform.rotation;

            Vector3 direction = (targetPos - startPos).normalized;
            Quaternion targetRot = direction != Vector3.zero ? Quaternion.LookRotation(direction) : startRot;

            float tickTime = TurnManager.Instance != null ? TurnManager.Instance.TickDuration : 1.0f;

            // Ha azt akarod, hogy ne legyen szünet a mezõk közt, hagyd 1.0f-en.
            // Ha 0.9f, akkor a maradék 10%-ban állni fog.
            float travelDuration = tickTime * 1.0f;
            float elapsed = 0f;

            while (elapsed < travelDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);

                // --- LINEÁRIS MOZGÁS ---
                // Nincs smoothT, közvetlenül a t-t használjuk a Lerp-nél
                transform.position = Vector3.Lerp(startPos, targetPos, t);

                // A forgatás maradjon gyors az elején, hogy irányba álljon
                float rotationT = Mathf.Clamp01(t * 5f);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, rotationT);

                yield return null;
            }

            transform.position = targetPos;
        }

        public void ExecuteFinalCapture(CellData target)
        {
            // A foglalás mértéke jöhetne akár az UnitData-ból is
            target.UpdateCapture(_ownerId, 0.2f);
            if (target.OwnerId == _ownerId) _gridManager.FinalizeCapture(target, _ownerId);
        }

        public void FaceTarget(Vector3 targetPos)
        {
            Vector3 dir = (targetPos - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);
        }

        public UnitController ScanForEnemies()
        {
            var neighbors = _gridManager.GetNeighbors(_currentCell);
            foreach (var n in neighbors)
            {
                if (n.IsOccupied)
                {
                    // Itt is használhatnánk IUnit-ot, de mivel ez a Gameplay réteg, 
                    // a UnitController ismerheti önmagát.
                    UnitController uc = n.GetFirstOccupier() as UnitController;
                    if (uc != null && uc._ownerId != _ownerId && !uc._isDead) return uc;
                }
            }
            return null;
        }

        public void RegisterPendingDamage(float amount) => _pendingDamage += amount;

        public void ApplyPendingDamage()
        {
            if (_isDead) return;
            _currentHP -= _pendingDamage;
            _pendingDamage = 0;
            if (_currentHP <= 0) _isDead = true;
        }

        public void ExecuteDeath()
        {
            TurnManager.OnTick -= HandleTick;

            // Eltávolítás a mezõrõl
            if (_currentCell != null) _currentCell.UnregisterOccupier(this);

            // Eltávolítás a játékos listájából
            var player = GameController.Instance?.GetPlayerById(_ownerId);
            player?.ActiveUnits.Remove(this);

            Destroy(gameObject);
        }
    }
}