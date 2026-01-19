using GridEmpire.Core;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

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

        private CellData _currentTargetCell;
        private GridManager _gridManager;
        private ITurnResolver _resolver;
        private List<CellData> _initialPath;
        private float _currentHP;
        private float _pendingDamage;
        private UnitAction _nextAction;
        private CellData _previousCell;
        [SerializeField] private float _currentStamina;

        public int OwnerId => _ownerId;
        public UnitData Data => _data;
        public CellData CurrentCell => _currentCell;
        public bool IsDead => _isDead;
        public void DestroyUnit() => ExecuteDeath();

        private void Update()
        {
            if (_previousCell != null)
            {
                Debug.DrawLine(transform.position, _gridManager.GetWorldPosition(_previousCell.Q, _previousCell.R), Color.red);
            }
        }

        public void Initialize(UnitData data, List<CellData> path, GridManager gm, int ownerId)
        {
            _data = data;
            _gridManager = gm;
            _ownerId = ownerId;
            _currentHP = data.maxHp;
            _currentStamina = data.maxStamina;
            _resolver = FindFirstObjectByType<TurnResolver>();
            _initialPath = path != null ? new List<CellData>(path) : new List<CellData>();

            var player = GameController.Instance?.GetPlayerById(ownerId);
            player?.ActiveUnits.Add(this);

            if (player != null && player.BaseCell != null)
                _currentCell = player.BaseCell;
            else
                _currentCell = _gridManager.GetCellAtPosition(transform.position);

            if (_currentCell != null) _currentCell.RegisterOccupier(this);

            UpdateInitialFacing();
        }

        private void UpdateInitialFacing()
        {
            CellData target = null;
            if (_initialPath.Count > 0) target = _initialPath[0];
            else target = FindExpansionCell();

            if (target != null)
            {
                Vector3 targetPos = _gridManager.GetWorldPosition(target.Q, target.R);
                FaceTarget(targetPos);
            }
        }

        public void PlanAction()
        {
            if (_isDead) return;
            _nextAction = new UnitAction { Performer = this, PlayerId = _ownerId, Type = ActionType.Idle };
            _currentStamina = Mathf.Min(_currentStamina + _data.staminaPerTurn, _data.maxStamina);

            UnitController nearbyEnemy = ScanForEnemies();
            if (nearbyEnemy != null)
            {
                _nextAction.Type = ActionType.Attack;
                _nextAction.TargetUnit = nearbyEnemy;
                _combatTarget = nearbyEnemy;
                isInCombat = true;
                _previousCell = _currentCell;
                ApplyFacingAndEnqueue();
                return;
            }

            isInCombat = false;

            if (_currentTargetCell != null)
            {
                if (_currentTargetCell.OwnerId != _ownerId)
                {
                    _nextAction.Type = ActionType.Capture;
                    _nextAction.TargetCell = _currentTargetCell;
                    _previousCell = _currentCell;
                    ApplyFacingAndEnqueue();
                    return;
                }
                else if (_currentCell != _currentTargetCell)
                {
                    if (!_currentTargetCell.IsOccupied && _currentStamina >= 1.0f)
                    {
                        _nextAction.Type = ActionType.Move;
                        _nextAction.TargetCell = _currentTargetCell;
                        ApplyFacingAndEnqueue();
                        return;
                    }
                    else { _currentTargetCell = null; }
                }
            }

            CellData next = GetValidNeighbor();
            if (next != null)
            {
                _currentTargetCell = next;
                _nextAction.Type = (next.OwnerId == _ownerId) ? ActionType.Move : ActionType.Capture;
                _nextAction.TargetCell = next;
                if (_nextAction.Type == ActionType.Capture) _previousCell = _currentCell;
            }
            else
            {
                _nextAction.Type = ActionType.Idle;
                _previousCell = _currentCell;
            }
            ApplyFacingAndEnqueue();
        }

        private CellData GetValidNeighbor()
        {
            if (_initialPath != null && _initialPath.Count > 0)
            {
                CellData p = _initialPath[0];
                if (_gridManager.GetDistance(_currentCell, p) == 1 && !p.IsOccupied) return p;
            }
            return FindExpansionCell();
        }

        private void ApplyFacingAndEnqueue()
        {
            if (_nextAction.TargetCell != null || _nextAction.TargetUnit != null)
            {
                if (_nextAction.Type == ActionType.Move && _currentStamina < 1.0f)
                    _nextAction.Type = ActionType.Idle;
                if (_resolver != null) _resolver.EnqueueAction(_nextAction);
                else Debug.LogError("[UnitController] Resolver not found!");
            }
        }

        private CellData FindExpansionCell()
        {
            var player = GameController.Instance?.GetPlayerById(_ownerId);
            if (player == null || player.BaseCell == null) return null;

            var neighbors = _gridManager.GetNeighbors(_currentCell);
            int currentDist = _gridManager.GetDistance(_currentCell, player.BaseCell);

            var preferredCells = neighbors.Where(n =>
                _gridManager.GetDistance(n, player.BaseCell) > currentDist && !n.IsOccupied && n != _previousCell
            ).ToList();

            if (preferredCells.Count > 0) return preferredCells[Random.Range(0, preferredCells.Count)];

            var fallbackCells = neighbors.Where(n =>
                !n.IsOccupied && !n.IsBase && n != _previousCell
            ).ToList();

            if (fallbackCells.Count > 0) return fallbackCells[Random.Range(0, fallbackCells.Count)];

            return null;
        }

        public void CalculateCombatLogic()
        {
            if (_isDead || _nextAction == null || _nextAction.Type != ActionType.Attack) return;

            if (_nextAction.TargetUnit is UnitController target && !target._isDead)
            {
                FaceTarget(target.transform.position);

                // Alapsebzés kiszámítása
                float totalDamage = _data.baseDamage;

                // Bónusz sebzés ellenõrzése (Típus elõny)
                if (_data.strongAgainst == target.Data.type)
                {
                    totalDamage += _data.bonusDamage;
                    Debug.Log($"{_data.unitName} bónusz sebzést oszt ki neki: {target.Data.unitName}!");
                }

                target.RegisterPendingDamage(totalDamage);
            }
        }

        public void ApplyPendingDamage()
        {
            if (_isDead) return;
            _currentHP -= _pendingDamage;
            _pendingDamage = 0;
            if (_currentHP <= 0) _isDead = true;
        }

        public void ExecuteFinalMove(CellData next)
        {
            if (next == null || next == _currentCell) return;
            _currentStamina -= 1.0f;
            _previousCell = _currentCell;
            if (_currentCell != null) _currentCell.UnregisterOccupier(this);
            Vector3 targetPos = _gridManager.GetWorldPosition(next.Q, next.R);
            FaceTarget(targetPos);
            _currentCell = next;
            _currentCell.RegisterOccupier(this);
            if (_currentTargetCell == next) _currentTargetCell = null;
            if (_initialPath.Count > 0 && _initialPath[0] == next) _initialPath.RemoveAt(0);
            StopAllCoroutines();
            StartCoroutine(Animate(next));
        }

        public void ExecuteFinalCapture(CellData target)
        {
            if (target == null) return;
            Vector3 targetPos = _gridManager.GetWorldPosition(target.Q, target.R);
            FaceTarget(targetPos);

            // LOGIKA: Megnézzük, semleges-e a mezõ
            bool isNeutral = target.OwnerId == -1;

            // Érték kiválasztása a UnitData-ból
            float speed = isNeutral ? _data.exploreSpeed : _data.conquerSpeed;

            target.UpdateCapture(_ownerId, speed);

            if (target.OwnerId == _ownerId)
                _gridManager.FinalizeCapture(target, _ownerId);
        }

        private IEnumerator Animate(CellData c)
        {
            Vector3 startPos = transform.position;
            Vector3 targetPos = _gridManager.GetWorldPosition(c.Q, c.R);
            float tickTime = TurnManager.Instance != null ? TurnManager.Instance.TickDuration : 1.0f;
            float elapsed = 0f;

            while (elapsed < tickTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / tickTime);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }
            transform.position = targetPos;
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
                if (n.IsOccupied && n.GetFirstOccupier() is UnitController uc && uc._ownerId != _ownerId && !uc._isDead)
                    return uc;
            }
            return null;
        }

        public void RegisterPendingDamage(float amount) => _pendingDamage += amount;

        public void ExecuteDeath()
        {
            if (_currentCell != null) _currentCell.UnregisterOccupier(this);

            GameController.Instance.RemoveUnit(this);
            Destroy(gameObject);
        }
        public float GetCurrentHP() => _currentHP; 
        public float GetCurrentStamina() => _currentStamina;
    }
}