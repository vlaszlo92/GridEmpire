using GridEmpire.Core;
using GridEmpire.Shared;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace GridEmpire.Gameplay
{
    public class UnitController : NetworkBehaviour, IUnit
    {
        public NetworkVariable<int> NetworkUnitId = new NetworkVariable<int>();
        public NetworkVariable<int> NetworkOwnerId = new NetworkVariable<int>();
        public NetworkVariable<int> NetworkUnitTypeIndex = new NetworkVariable<int>();

        [Header("Unit State")]
        [SerializeField] private int _id;
        public int Id => _id;
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

        private PlayerProfile _ownerProfile;
        private MeshRenderer[] _renderers;

        public int OwnerId => _ownerId;
        public UnitData Data => _data;
        public CellData CurrentCell => _currentCell;
        public bool IsDead => _isDead;
        public void DestroyUnit() => ExecuteDeath();

        private void Awake()
        {
            _gridManager = FindFirstObjectByType<GridManager>();
            _resolver = FindFirstObjectByType<TurnResolver>();
        }

        public override void OnNetworkSpawn()
        {
            _id = NetworkUnitId.Value;
            _ownerId = NetworkOwnerId.Value;

            if (!IsServer)
            {
                StartCoroutine(ClientInitDeferred());
            }
        }

        private IEnumerator ClientInitDeferred()
        {
            yield return new WaitUntil(() =>
                GameController.Instance != null &&
                GameController.Instance.GetPlayerById(_ownerId) != null &&
                FindFirstObjectByType<GridManager>() != null
            );

            _data = GameController.Instance.GetUnitDataByIndex(NetworkUnitTypeIndex.Value);
            if (_data == null) yield break;

            _gridManager = FindFirstObjectByType<GridManager>();
            GameController.Instance.RegisterUnit(this);
            _ownerProfile = GameController.Instance.GetPlayerById(_ownerId);
            _ownerProfile?.AddUnit(this);
            SyncPositionToCurrentCell();
            OnCellVisibilityChanged(_currentCell.CurrentVisibility);
        }

        private void Update()
        {
            if (_previousCell != null && _gridManager != null)
            {
                Debug.DrawLine(transform.position, _gridManager.GetWorldPosition(_previousCell.Q, _previousCell.R), Color.red);
            }
        }

        public void Initialize(int uniqueId, UnitData data, List<CellData> path, GridManager gm, int ownerId)
        {
            if (!IsServer) return;

            NetworkUnitId.Value = uniqueId;
            NetworkOwnerId.Value = ownerId;
            NetworkUnitTypeIndex.Value = data.index;

            _id = uniqueId;
            _data = data;
            _gridManager = gm;
            _ownerId = ownerId;
            _currentHP = data.maxHp;
            _currentStamina = data.maxStamina;

            _resolver = FindFirstObjectByType<TurnResolver>();
            _resolver?.RegisterUnit(this);
            GameController.Instance.RegisterUnit(this);

            _ownerProfile = GameController.Instance?.GetPlayerById(ownerId);
            _ownerProfile?.AddUnit(this);

            _initialPath = path != null ? new List<CellData>(path) : new List<CellData>();

            if (_ownerProfile != null && _ownerProfile.BaseCell != null)
                _currentCell = _ownerProfile.BaseCell;
            else
                _currentCell = _gridManager.GetCellAtPosition(transform.position);

            if (_currentCell != null) _currentCell.RegisterOccupier(this);

            UpdateInitialFacing();
            OnCellVisibilityChanged(_currentCell.CurrentVisibility);
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

        private void SyncPositionToCurrentCell()
        {
            if (_gridManager == null)
                _gridManager = FindFirstObjectByType<GridManager>();

            if (_gridManager != null)
            {
                _currentCell = _gridManager.GetCellAtPosition(transform.position);
                if (_currentCell != null)
                {
                    _currentCell.RegisterOccupier(this);
                    transform.position = _gridManager.GetWorldPosition(_currentCell.Q, _currentCell.R);
                }
            }
        }

        // ─── PLAN ACTION ────────────────────────────────────────────────────────────

        public void PlanAction()
        {
            if (!NetworkManager.Singleton.IsServer || _isDead) return;
            //Debug.Log($"[PlanAction] unit={_id}, cell={_currentCell?.Id}, targetCell={_currentTargetCell?.Id}, " +
            //  $"targetOwner={_currentTargetCell?.OwnerId}, myOwner={_ownerId}, " +
            //  $"isOccupied={_currentTargetCell?.IsOccupied}, " +
            //  $"captureProgress={_currentTargetCell?.GetCaptureProgress(_ownerId):F2}, " +
            //  $"prevCell={_previousCell?.Id}");
            _nextAction = new UnitAction { PerformerUnitId = _id, PlayerId = _ownerId, Type = ActionType.Idle, TargetCellId = -1 };
            _currentStamina = Mathf.Min(_currentStamina + _data.staminaPerTurn, _data.maxStamina);

            UnitController nearbyEnemy = ScanForEnemies();
            if (nearbyEnemy != null)
            {
                if (_combatTarget != null && !_combatTarget.IsDead &&
                    _gridManager.GetDistance(_currentCell, _combatTarget.CurrentCell) == 1)
                {
                    nearbyEnemy = _combatTarget;
                }
                else
                {
                    _combatTarget = nearbyEnemy;
                }

                _nextAction.Type = ActionType.Attack;
                _nextAction.TargetUnitId = nearbyEnemy.Id;
                isInCombat = true;
                _previousCell = _currentCell;
                ApplyFacingAndEnqueue();
                return;
            }

            isInCombat = false;

            if (_currentTargetCell != null && _currentTargetCell.IsOccupied && _currentTargetCell.OwnerId != _ownerId)
                _currentTargetCell = null;

            if (_currentTargetCell != null)
            {
                if (_currentTargetCell.OwnerId != _ownerId)
                {
                    // Foglalható: akkor is ha már mások is foglalják (capture conflict)
                    _nextAction.Type = ActionType.Capture;
                    _nextAction.TargetCellId = _currentTargetCell.Id;
                    _previousCell = _currentCell;
                    ApplyFacingAndEnqueue();
                    return;
                }
                else if (_currentCell != _currentTargetCell)
                {
                    bool fullyOwned = _currentTargetCell.GetCaptureProgress(_ownerId) >= 1.0f;
                    if (!_currentTargetCell.IsOccupied && _currentStamina >= 1.0f && fullyOwned)
                    {
                        _nextAction.Type = ActionType.Move;
                        _nextAction.TargetCellId = _currentTargetCell.Id;
                        ApplyFacingAndEnqueue();
                        return;
                    }
                    else 
                    { 
                        _currentTargetCell = null;
                        return;
                    }
                }
            }

            CellData next = GetValidNeighbor();
            if (next != null)
            {
                _currentTargetCell = next;
                _nextAction.Type = (next.OwnerId == _ownerId) ? ActionType.Move : ActionType.Capture;
                _nextAction.TargetCellId = next.Id;
                if (_nextAction.Type == ActionType.Capture) _previousCell = _currentCell;
            }
            else
            {
                _nextAction.Type = ActionType.Idle;
                _nextAction.TargetCellId = -1;
                _previousCell = null;
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
            if (_nextAction.TargetCellId >= 0 || _nextAction.TargetUnitId > 0)
            {
                if (_nextAction.Type == ActionType.Move && _currentStamina < 1.0f)
                    _nextAction.Type = ActionType.Idle;
                if (_resolver != null) _resolver.EnqueueAction(_nextAction);
            }
        }
        private CellData FindExpansionCell()
        {
            var player = GameController.Instance?.GetPlayerById(_ownerId);
            if (player == null || player.BaseCell == null) return null;

            var neighbors = _gridManager.GetNeighbors(_currentCell);
            int currentDist = _gridManager.GetDistance(_currentCell, player.BaseCell);

            CellData preferred = null, fallback = null;
            int preferredCount = 0, fallbackCount = 0;

            foreach (var n in neighbors)
            {
                if (n.IsOccupied || n == _previousCell) continue;
                bool capturable = n.OwnerId != _ownerId || n.GetCaptureProgress(_ownerId) >= 1.0f;
                if (!capturable) continue;

                if (_gridManager.GetDistance(n, player.BaseCell) > currentDist)
                {
                    preferredCount++;
                    // Reservoir sampling: véletlenszerű választás allokáció nélkül
                    if (Random.Range(0, preferredCount) == 0) preferred = n;
                }
                else if (!n.IsBase)
                {
                    fallbackCount++;
                    if (Random.Range(0, fallbackCount) == 0) fallback = n;
                }
            }

            return preferred ?? fallback;
        }

        public void OnCellVisibilityChanged(VisibilityState visibility)
        {
            bool isOwn = _ownerId == GameController.Instance?.GetLocalPlayer()?.Id;
            bool visible = isOwn || visibility == VisibilityState.Visible;
            SetVisible(visible);
        }

        private void SetVisible(bool visible)
        {
            if (_renderers == null)
                _renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var r in _renderers)
                r.enabled = visible;
        }


        // ─── COMBAT ─────────────────────────────────────────────────────────────────

        public void CalculateCombatLogic()
        {
            if (_isDead || _nextAction == null || _nextAction.Type != ActionType.Attack) return;

            var target = GameController.Instance?.GetUnitById(_nextAction.TargetUnitId) as UnitController;
            if (target != null && !target.IsDead)
            {
                FaceTarget(target.transform.position);

                float totalDamage = _data.baseDamage;
                if (_data.strongAgainst == target.Data.type)
                    totalDamage += _data.bonusDamage;

                target.RegisterPendingDamage(totalDamage);

                int targetCellId = target._currentCell?.Id ?? -1;
                int myCellId = _currentCell?.Id ?? -1;

                AttackClientRpc(targetCellId);
                target.BeAttackedClientRpc(myCellId);
            }
        }

        // Capture konfliktus: ha ugyanazt a cellát foglalja ellenséges egység is, sebzik egymást.
        // Csak a magasabb ID-jú egység számol, hogy minden pár pontosan egyszer legyen feldolgozva.
        public void CalculateCaptureConflict()
        {
            if (_isDead || _nextAction == null || _nextAction.Type != ActionType.Capture) return;

            CellData targetCell = _gridManager?.GetCellById(_nextAction.TargetCellId);
            if (targetCell == null) return;

            foreach (int enemyId in targetCell.CapturingUnitIds)
            {
                if (enemyId == _id) continue;

                var enemy = GameController.Instance?.GetUnitById(enemyId) as UnitController;
                if (enemy == null || enemy._isDead || enemy._ownerId == _ownerId) continue;

                if (_id > enemy._id)
                {
                    enemy.RegisterPendingDamage(_data.baseDamage);
                    RegisterPendingDamage(enemy._data.baseDamage);
                    AttackClientRpc(enemy._id);
                }
            }
        }

        [ClientRpc]
        private void AttackClientRpc(int targetCellId)
        {
            if (IsServer) return;
            var cell = _gridManager?.GetCellById(targetCellId);
            if (cell == null) return;
            Vector3 targetPos = _gridManager.GetWorldPosition(cell.Q, cell.R);
            FaceTarget(targetPos);
        }

        [ClientRpc]
        public void BeAttackedClientRpc(int attackerCellId)
        {
            if (IsServer) return;
            if (_gridManager == null) _gridManager = GridManager.Instance;
            var cell = _gridManager?.GetCellById(attackerCellId);
            if (cell == null) return;
            Vector3 attackerPos = _gridManager.GetWorldPosition(cell.Q, cell.R);
            FaceTarget(attackerPos);
        }

        public void ApplyPendingDamage()
        {
            if (_isDead) return;
            _currentHP -= _pendingDamage;
            _pendingDamage = 0;
            if (_currentHP <= 0) _isDead = true;
            DamageClientRpc(_currentHP, _isDead);
        }

        [ClientRpc]
        private void DamageClientRpc(float newHp, bool isDead)
        {
            if (IsServer) return;
            _currentHP = newHp;
            if (isDead && !_isDead)
            {
                _isDead = true;
                // TODO: halál animáció
            }
        }

        // ─── MOVE ────────────────────────────────────────────────────────────────────

        public void ExecuteFinalMove(CellData next)
        {
            if (!IsServer || next == null || next == _currentCell) return;

            _currentStamina -= 1.0f;
            _previousCell = _currentCell;

            if (_currentCell != null) _currentCell.UnregisterOccupier(this);

            Vector3 targetPos = _gridManager.GetWorldPosition(next.Q, next.R);
            FaceTarget(targetPos);

            _currentCell = next;
            _currentCell.RegisterOccupier(this);
            
            if (_previousCell != null && _previousCell.OwnerId == OwnerId)
            {
                _previousCell.SetInfluence(OwnerId, 1f);
                _previousCell.CapturingUnitIds.Clear();
            }

            if (_currentTargetCell == next) _currentTargetCell = null;
            if (_initialPath.Count > 0 && _initialPath[0] == next) _initialPath.RemoveAt(0);

            _resolver?.MarkCellChanged(next.Id);
            if (_previousCell != null) _resolver?.MarkCellChanged(_previousCell.Id);

            StopAllCoroutines();
            StartCoroutine(Animate(next));
            MoveClientRpc(next.Id);
        }

        [ClientRpc]
        private void MoveClientRpc(int targetCellId)
        {
            if (IsServer) return;

            CellData next = _gridManager.GetCellById(targetCellId);
            if (next == null) return;

            if (_currentCell != null) _currentCell.UnregisterOccupier(this);
            _currentCell = next;
            _currentCell.RegisterOccupier(this);

            StopAllCoroutines();
            StartCoroutine(Animate(next));
            // TODO: mozgás animáció
        }

        // ─── CAPTURE ─────────────────────────────────────────────────────────────────

        public void ExecuteFinalCapture(CellData target)
        {
            if (target == null) return;

            Vector3 targetPos = _gridManager.GetWorldPosition(target.Q, target.R);
            FaceTarget(targetPos);

            // Regisztráljuk hogy ezt a cellát foglaljuk (Contains check a duplikáció ellen)
            if (!target.CapturingUnitIds.Contains(_id))
                target.CapturingUnitIds.Add(_id);

            bool isNeutral = target.OwnerId == -1;
            float speed = isNeutral ? _data.exploreSpeed : _data.conquerSpeed;

            target.UpdateCapture(_ownerId, speed);

            bool captured = target.OwnerId == _ownerId;
            if (captured)
            {
                target.SetInfluence(_ownerId, 1f);
                target.CapturingUnitIds.Clear();
                _gridManager.FinalizeCapture(target, _ownerId);
                _resolver?.MarkCellChanged(target.Id);
            }

            CaptureClientRpc(target.Id, target.OwnerId, speed, captured, _ownerId);
        }

        [ClientRpc]
        private void CaptureClientRpc(int cellId, int currentOwnerId, float speed, bool captured, int attackerId)
        {
            if (IsServer) return;

            CellData cell = _gridManager.GetCellById(cellId);
            if (cell == null) return;

            Vector3 targetPos = _gridManager.GetWorldPosition(cell.Q, cell.R);
            FaceTarget(targetPos);

            if (captured)
            {
                cell.SetInfluence(attackerId, 1f);
                cell.CapturingUnitIds.Clear();
            }
            else
            {
                cell.UpdateCapture(attackerId, speed);
            }

            if (captured)
                _gridManager.FinalizeCapture(cell, attackerId);

            cell.OnVisualUpdateRequired?.Invoke();
            // TODO: foglalás animáció
        }

        // ─── DEATH ───────────────────────────────────────────────────────────────────

        public void ExecuteDeath()
        {
            // Ha foglalt egy cellát, töröljük a hódítók listájából
            _currentTargetCell?.CapturingUnitIds.Remove(_id);

            _resolver?.UnregisterUnit(this);
            if (_currentCell != null) _currentCell.UnregisterOccupier(this);
            GameController.Instance?.RemoveUnit(this);

            if (IsServer)
            {
                DeathClientRpc();
                GetComponent<NetworkObject>()?.Despawn(true);
            }
        }

        [ClientRpc]
        private void DeathClientRpc()
        {
            if (IsServer) return;
            _currentTargetCell?.CapturingUnitIds.Remove(_id);
            if (_currentCell != null) _currentCell.UnregisterOccupier(this);
            GameController.Instance?.RemoveUnit(this);
            // TODO: halál animáció
        }

        // ─── HELPERS ─────────────────────────────────────────────────────────────────

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

        public void RequestMove(Vector2Int targetCoords)
        {
            var wpos = new Vector3(targetCoords.x, 0, targetCoords.y);
            CellData targetCell = _gridManager.GetCellAtPosition(wpos);
            RequestMove(targetCell);
        }

        public void RequestMove(CellData target)
        {
            if (_isDead || target == null || target == _currentCell) return;
            if (_initialPath != null) _initialPath.Clear();
            _currentTargetCell = target;
        }

        public float GetCurrentHP() => _currentHP;
        public float GetCurrentStamina() => _currentStamina;

        public void SyncFromSnapshot(float newHp, bool isDead)
        {
            _currentHP = newHp;
            if (isDead && !_isDead) ExecuteDeath();
        }

        public override void OnDestroy()
        {
            // Biztonsági takarítás halálkor vagy jelenetváltáskor
            _currentTargetCell?.CapturingUnitIds.Remove(_id);

            _resolver?.UnregisterUnit(this);
            if (GameController.Instance != null)
                GameController.Instance.UnregisterUnit(_id);
            if (!_isDead)
                GameController.Instance?.RemoveUnit(this);

            base.OnDestroy();
        }
    }
}