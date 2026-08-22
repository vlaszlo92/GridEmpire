using GridEmpire.Core;
using GridEmpire.Input;
using GridEmpire.Networking;
using GridEmpire.Shared;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace GridEmpire.Gameplay
{
    [System.Serializable]
    public struct ColorizableTarget
    {
        public Renderer renderer;
        public int materialIndex;
    }

    public class UnitController : NetworkBehaviour, IUnit
    {
        public NetworkVariable<int> NetworkUnitId = new NetworkVariable<int>();
        public NetworkVariable<int> NetworkOwnerId = new NetworkVariable<int>();
        public NetworkVariable<int> NetworkUnitTypeIndex = new NetworkVariable<int>();
        public NetworkVariable<EffectiveUnitStats> NetworkStats = new NetworkVariable<EffectiveUnitStats>();
        public NetworkVariable<int> NetworkCellId = new NetworkVariable<int>(default, NetworkVariableReadPermission.Everyone);

        public EffectiveUnitStats Stats => NetworkStats.Value;

        [Header("Colorization Setup")]
        [SerializeField] private List<ColorizableTarget> _colorizableTargets;

        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _propBlock;

        [Header("Unit State")]
        [SerializeField] private int _id;
        public int Id => _id;
        public UnitData _data;
        public int _ownerId;
        public bool _isDead = false;
        public bool isInCombat = false;
        public CellData _currentCell;
        public UnitController _combatTarget;
        public UnitAnimator _unitAnimator;

        private CellData _currentTargetCell;
        private GridManager _gridManager;
        private ITurnResolver _resolver;
        private List<CellData> _initialPath;
        private float _currentHP;
        private float _pendingDamage;
        private UnitAction _nextAction;
        private CellData _previousCell;
        private int _facingTargetId = -1;
        [SerializeField] private float _currentStamina;
        private Coroutine _rotateCoroutine;

        private PlayerProfile _ownerProfile;
        private Renderer[] _renderers;

        public int OwnerId => _ownerId;
        public UnitData Data => _data;
        public CellData CurrentCell => _currentCell;
        public void SetInitialCell(CellData cellData) => _currentCell = cellData;
        public bool IsDead => _isDead;

        public void DestroyUnit()
        {
            if (IsServer) ExecuteDeath();
            else RequestDestroyServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestDestroyServerRpc(RpcParams rpcParams = default)
        {
            if (!NetworkAuthority.IsOwner(rpcParams.Receive.SenderClientId, _ownerId)) return;
            ExecuteDeath();
        }

        private void Awake()
        {
            _gridManager = GridManager.Instance;
            _resolver = FindFirstObjectByType<TurnResolver>();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
                foreach (var r in _renderers) r.enabled = false;
            }

            _unitAnimator = GetComponent<UnitAnimator>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            NetworkOwnerId.OnValueChanged += OnOwnerIdChanged;
            if (!IsServer)
            {
                OnNetworkCellChanged(0, NetworkCellId.Value);
            }

            _id = NetworkUnitId.Value;
            _ownerId = NetworkOwnerId.Value;

            Debug.Log(
                $"[FOW] OnNetworkSpawn: unit={_id}, owner={_ownerId}, " +
                $"isServer={IsServer}, localClient={NetworkManager.Singleton.LocalClientId}, " +
                $"networkObjectId={NetworkObject.NetworkObjectId}");

            if (!IsServer)
                StartCoroutine(ClientSyncWhenReady());
        }

        private void OnNetworkCellChanged(int previousId, int newCellId)
        {
            _currentCell = GridManager.Instance.GetCellById(newCellId);
            if (_currentCell != null)
            {
                _currentCell.RegisterOccupier(this);
                transform.position = _gridManager.GetWorldPosition(_currentCell.Q, _currentCell.R);
            }
        }

        private IEnumerator ClientSyncWhenReady()
        {
            yield return new WaitUntil(() => GridManager.Instance != null && GridManager.Instance.IsReady);
            yield return new WaitUntil(() => NetworkUnitId.Value != 0);

            float t = 0f;
            while ((GameController.Instance == null || !GameController.Instance.HasUnitData(NetworkUnitTypeIndex.Value)) && t < 5f)
            {
                t += Time.deltaTime;
                yield return null;
            }

            SyncToAuthoritativeState();

            Debug.Log(
                $"[FOW] ClientSyncWhenReady DONE: unit={_id}, owner={_ownerId}, " +
                $"client={NetworkManager.Singleton.LocalClientId}, " +
                $"hasUnitData={GameController.Instance?.HasUnitData(NetworkUnitTypeIndex.Value)}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            NetworkOwnerId.OnValueChanged -= OnOwnerIdChanged;
        }

        private void OnOwnerIdChanged(int previousValue, int newValue)
        {
            _ownerId = newValue;
            ApplyPlayerColor();
        }

        public void SyncToAuthoritativeState()
        {
            _id = NetworkUnitId.Value;
            _ownerId = NetworkOwnerId.Value;
            _data = GameController.Instance?.GetUnitDataByIndex(NetworkUnitTypeIndex.Value);

            if (_gridManager == null) _gridManager = GridManager.Instance;

            if (_gridManager != null && _gridManager.IsReady)
            {
                _currentCell = _gridManager.GetCellById(NetworkCellId.Value);
                if (_currentCell != null)
                {
                    _currentCell.RegisterOccupier(this);
                    transform.position = _gridManager.GetWorldPosition(_currentCell.Q, _currentCell.R);
                }
            }

            GameController.Instance?.RegisterUnit(this);
            ApplyPlayerColor();

            if (!IsServer)
            {
                var localPlayer = GameController.Instance?.GetLocalPlayer();
                if (localPlayer != null) _gridManager?.UpdateFogOfWar(localPlayer.Id);
            }
        }

        public void Initialize(int uniqueId, UnitData data, List<CellData> path, GridManager gm, int ownerId)
        {
            if (!IsServer) return;

            NetworkUnitId.Value = uniqueId;
            NetworkOwnerId.Value = ownerId;
            NetworkUnitTypeIndex.Value = data.index;

            if (NetworkStats.Value.MaxHp == 0)
            {
                NetworkStats.Value = new EffectiveUnitStats
                {
                    MaxHp = data.maxHp,
                    StaminaPerTurn = data.staminaPerTurn,
                    MaxStamina = data.maxStamina,
                    ConquerSpeed = data.conquerSpeed,
                    ExploreSpeed = data.exploreSpeed,
                    BaseDamage = data.baseDamage,
                    BonusDamage = data.bonusDamage
                };
            }

            _id = uniqueId;
            _data = data;
            _gridManager = gm;
            _ownerId = ownerId;
            _currentHP = NetworkStats.Value.MaxHp;
            _currentStamina = NetworkStats.Value.MaxStamina;

            _resolver = FindFirstObjectByType<TurnResolver>();
            _resolver?.RegisterUnit(this);
            GameController.Instance.RegisterUnit(this);

            _initialPath = path != null ? new List<CellData>(path) : new List<CellData>();

            if (_ownerProfile != null && _ownerProfile.BaseCell != null)
                _currentCell = _ownerProfile.BaseCell;
            else
                _currentCell = _gridManager.GetCellAtPosition(transform.position);

            if (_currentCell != null) _currentCell.RegisterOccupier(this);

            ApplyPlayerColor();
        }

        // --- PLAN ACTION ------------------------------------------------------------

        public void PlanAction()
        {
            if (!NetworkManager.Singleton.IsServer || _isDead) return;
            _nextAction = new UnitAction { PerformerUnitId = _id, PlayerId = _ownerId, Type = ActionType.Idle, TargetCellId = -1 };
            _currentStamina = Mathf.Min(_currentStamina + NetworkStats.Value.StaminaPerTurn, NetworkStats.Value.MaxStamina);
            bool canMove = _currentStamina >= 1.0f;

            UnitController nearbyEnemy = ScanForEnemies();
            if (nearbyEnemy != null)
            {
                if (_combatTarget != null && !_combatTarget.IsDead &&
                    _gridManager.GetDistance(_currentCell, _combatTarget.CurrentCell) == 1)
                    nearbyEnemy = _combatTarget;
                else
                    _combatTarget = nearbyEnemy;

                _nextAction.Type = ActionType.Attack;
                _nextAction.TargetUnitId = nearbyEnemy.Id;
                isInCombat = true;
                _previousCell = _currentCell;
                EnqueueAction();
                return;
            }

            isInCombat = false;

            if (_currentCell.OwnerId == _ownerId && _currentCell.GetCaptureProgress(_ownerId) < 1.0f)
            {
                _nextAction.Type = ActionType.Capture;
                _nextAction.TargetCellId = _currentCell.Id;
                EnqueueAction();
                return;
            }

            if (!canMove)
            {
                _resolver?.EnqueueAction(_nextAction);
                return;
            }

            if (_currentTargetCell != null && _currentTargetCell.IsOccupied && _currentTargetCell.OwnerId != _ownerId)
                _currentTargetCell = null;

            if (_currentTargetCell != null)
            {
                if (_currentTargetCell.OwnerId != _ownerId)
                {
                    _nextAction.Type = ActionType.Capture;
                    _nextAction.TargetCellId = _currentTargetCell.Id;
                    _previousCell = _currentCell;
                    EnqueueAction();
                    return;
                }
                else if (_currentCell != _currentTargetCell)
                {
                    bool fullyOwned = _currentTargetCell.GetCaptureProgress(_ownerId) >= 1.0f;
                    if (!_currentTargetCell.IsOccupied && _currentStamina >= 1.0f && fullyOwned)
                    {
                        _nextAction.Type = ActionType.Move;
                        _nextAction.TargetCellId = _currentTargetCell.Id;
                        EnqueueAction();
                        return;
                    }
                    else
                    {
                        _currentTargetCell = null;
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
                _previousCell = _currentCell;
            }

            EnqueueAction();
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

        private void EnqueueAction()
        {
            if (_nextAction.TargetCellId >= 0 || _nextAction.TargetUnitId > 0)
                _resolver?.EnqueueAction(_nextAction);
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

        public void SetVisible(bool visible)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers) r.enabled = visible;
        }

        public void SetAudioVisible(bool visible)
        {
            var audioSources = GetComponentsInChildren<AudioSource>();
            foreach (var source in audioSources)
                source.mute = !visible;
        }

        // --- COMBAT -----------------------------------------------------------------

        public void CalculateCombatLogic()
        {
            if (_isDead || _nextAction == null || _nextAction.Type != ActionType.Attack) return;

            var target = GameController.Instance?.GetUnitById(_nextAction.TargetUnitId) as UnitController;
            if (target != null && !target.IsDead)
            {
                FaceCombatTarget(target.Id, target.transform.position);

                float totalDamage = NetworkStats.Value.BaseDamage;
                if (_data.strongAgainst == target.Data.type)
                    totalDamage += NetworkStats.Value.BonusDamage;

                target.RegisterPendingDamage(totalDamage);
                target.FaceCombatTarget(_id, transform.position);

                int targetCellId = target._currentCell?.Id ?? -1;
                int myCellId = _currentCell?.Id ?? -1;

                _unitAnimator?.Play(ActionType.Attack);
                var rpcParams = BuildVisibilityRpcParams(new[] { _ownerId, target._ownerId }, _currentCell, target._currentCell);
                AttackClientRpc(targetCellId, target.Id, rpcParams);
                target.BeAttackedClientRpc(myCellId, _id, rpcParams);
            }
        }

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
                    enemy.RegisterPendingDamage(NetworkStats.Value.BaseDamage);
                    RegisterPendingDamage(enemy.NetworkStats.Value.BaseDamage);
                    _unitAnimator?.Play(ActionType.Attack);

                    int enemyCellId = enemy._currentCell?.Id ?? -1;
                    int myCellId = _currentCell?.Id ?? -1;

                    FaceCombatTarget(enemy._id, enemy.transform.position);
                    enemy.FaceCombatTarget(_id, transform.position);

                    var rpcParams = BuildVisibilityRpcParams(new[] { _ownerId, enemy._ownerId }, _currentCell, enemy._currentCell);
                    AttackClientRpc(enemyCellId, enemy._id, rpcParams);
                    enemy.AttackClientRpc(myCellId, _id, rpcParams);
                }
            }
        }

        [ClientRpc]
        private void AttackClientRpc(int targetCellId, int targetUnitId, ClientRpcParams clientRpcParams = default)
        {
            if (IsServer) return;
            var cell = _gridManager?.GetCellById(targetCellId);
            if (cell == null) return;
            FaceCombatTarget(targetUnitId, _gridManager.GetWorldPosition(cell.Q, cell.R));
            _unitAnimator?.Play(ActionType.Attack);
        }

        [ClientRpc]
        public void BeAttackedClientRpc(int attackerCellId, int attackerUnitId, ClientRpcParams clientRpcParams = default)
        {
            if (IsServer) return;
            if (_gridManager == null) _gridManager = GridManager.Instance;
            var cell = _gridManager?.GetCellById(attackerCellId);
            if (cell == null) return;
            FaceCombatTarget(attackerUnitId, _gridManager.GetWorldPosition(cell.Q, cell.R));
        }

        public void ApplyPendingDamage()
        {
            if (_isDead) return;
            _currentHP -= _pendingDamage;
            _pendingDamage = 0;
            if (_currentHP <= 0) _isDead = true;
            DamageClientRpc(_currentHP, _isDead, BuildVisibilityRpcParams(new[] { _ownerId }, _currentCell));
        }

        [ClientRpc]
        private void DamageClientRpc(float newHp, bool isDead, ClientRpcParams clientRpcParams = default)
        {
            if (IsServer) return;
            _currentHP = newHp;
            if (isDead && !_isDead)
            {
                _isDead = true;
            }
        }

        // --- MOVE --------------------------------------------------------------------

        public void ExecuteFinalMove(CellData next)
        {
            if (!IsServer || next == null || next == _currentCell) return;

            _currentStamina -= 1.0f;
            _previousCell = _currentCell;

            if (_currentCell != null) _currentCell.UnregisterOccupier(this);

            _currentCell = next;
            _currentCell.RegisterOccupier(this);

            NetworkCellId.Value = next.Id;

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
            Vector3 targetPos = _gridManager.GetWorldPosition(next.Q, next.R);
            FaceTarget(targetPos);
            StartCoroutine(MoveToCell(next));
            _facingTargetId = -1;
            _unitAnimator?.Play(ActionType.Move);
            MoveClientRpc(next.Id, BuildVisibilityRpcParams(new[] { _ownerId }, _previousCell, next));
        }

        [ClientRpc]
        private void MoveClientRpc(int targetCellId, ClientRpcParams clientRpcParams = default)
        {
            if (IsServer) return;

            if (_gridManager == null) _gridManager = GridManager.Instance;
            if (_gridManager == null || !_gridManager.IsReady) return;

            CellData next = _gridManager.GetCellById(targetCellId);
            if (next == null) return;

            if (_currentCell != null) _currentCell.UnregisterOccupier(this);
            _currentCell = next;
            _currentCell.RegisterOccupier(this);

            StopAllCoroutines();
            Vector3 targetPos = _gridManager.GetWorldPosition(next.Q, next.R);
            FaceTarget(targetPos);
            StartCoroutine(MoveToCell(next));
            _facingTargetId = -1;
            _unitAnimator?.Play(ActionType.Move);
        }

        // --- CAPTURE -----------------------------------------------------------------

        public void ExecuteFinalCapture(CellData target)
        {
            if (target == null) return;

            Vector3 targetPos = _gridManager.GetWorldPosition(target.Q, target.R);
            if (_facingTargetId < 0) FaceTarget(targetPos);

            if (!target.CapturingUnitIds.Contains(_id))
                target.CapturingUnitIds.Add(_id);

            bool isNeutral = target.OwnerId == -1;
            float speed = isNeutral ? NetworkStats.Value.ExploreSpeed : NetworkStats.Value.ConquerSpeed;
            int previousOwnerId = target.OwnerId;

            target.UpdateCapture(_ownerId, speed);

            bool captured = target.OwnerId == _ownerId;
            if (captured)
            {
                target.SetInfluence(_ownerId, 1f);
                target.CapturingUnitIds.Clear();
                _gridManager.FinalizeCapture(target, _ownerId);
                _resolver?.MarkCellChanged(target.Id);
            }
            _unitAnimator?.Play(ActionType.Capture);

            var rpcParams = BuildVisibilityRpcParams(new[] { _ownerId, previousOwnerId }, target);
            CaptureAnimClientRpc(target.Id, rpcParams);
            if (_gridManager == null) _gridManager = GridManager.Instance;
            _gridManager.CellCapturedClientRpc(target.Id, target.OwnerId, speed, captured, _ownerId, rpcParams);
        }

        [ClientRpc]
        private void CaptureAnimClientRpc(int cellId, ClientRpcParams clientRpcParams = default)
        {
            if (IsServer) return;

            if (_gridManager == null) _gridManager = GridManager.Instance;
            if (_gridManager == null || !_gridManager.IsReady) return;

            CellData cell = _gridManager.GetCellById(cellId);
            if (cell == null) return;

            FaceTarget(_gridManager.GetWorldPosition(cell.Q, cell.R));
            _unitAnimator?.Play(ActionType.Capture);
        }

        // --- DEATH -------------------------------------------------------------------

        public void ExecuteDeath()
        {
            _isDead = true;
            _currentTargetCell?.CapturingUnitIds.Remove(_id);

            var rpcParams = BuildVisibilityRpcParams(new[] { _ownerId }, _currentCell);

            _resolver?.UnregisterUnit(this);
            if (_currentCell != null) _currentCell.UnregisterOccupier(this);
            GameController.Instance?.RemoveUnit(this);

            if (IsServer)
            {
                DeathClientRpc(rpcParams);
                _unitAnimator?.PlayDeath(() =>
                {
                    if (TryGetComponent<NetworkObject>(out var netObj) && netObj.IsSpawned)
                        netObj.Despawn(true);
                });
            }
        }

        [ClientRpc]
        private void DeathClientRpc(ClientRpcParams clientRpcParams = default)
        {
            if (IsServer) return;
            _isDead = true;
            _currentTargetCell?.CapturingUnitIds.Remove(_id);
            if (_currentCell != null) _currentCell.UnregisterOccupier(this);
            GameController.Instance?.RemoveUnit(this);
            _unitAnimator?.PlayDeath();
        }

        [ClientRpc]
        public void IdleClientRpc()
        {
            if (IsServer) return;
            _unitAnimator?.Play(ActionType.Idle);
        }

        // --- HELPERS -----------------------------------------------------------------

        private void ApplyPlayerColor()
        {
            var player = GameController.Instance?.GetPlayerById(_ownerId);
            if (player == null) return;

            if (_colorizableTargets == null || _colorizableTargets.Count == 0) return;

            if (_propBlock == null)
                _propBlock = new MaterialPropertyBlock();

            foreach (var target in _colorizableTargets)
            {
                if (target.renderer == null) continue;

                target.renderer.GetPropertyBlock(_propBlock, target.materialIndex);
                _propBlock.SetColor(BaseColorPropertyId, player.Color);
                target.renderer.SetPropertyBlock(_propBlock, target.materialIndex);
            }
        }

        private IEnumerator MoveToCell(CellData c)
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

        private void FaceCombatTarget(int sourceUnitId, Vector3 pos)
        {
            if (_facingTargetId >= 0 && _facingTargetId != sourceUnitId)
            {
                var current = GameController.Instance?.GetUnitById(_facingTargetId) as UnitController;
                if (current != null && !current.IsDead) return;
            }

            _facingTargetId = sourceUnitId;
            FaceTarget(pos);
        }

        public void FaceTarget(Vector3 targetPos)
        {
            Vector3 dir = (targetPos - transform.position).normalized;
            dir.y = 0;
            if (dir == Vector3.zero) return;

            Quaternion target = Quaternion.LookRotation(dir);
            if (Quaternion.Angle(transform.rotation, target) < 5f)
            {
                transform.rotation = target;
                return;
            }

            if (_rotateCoroutine != null) StopCoroutine(_rotateCoroutine);
            _rotateCoroutine = StartCoroutine(RotateTowards(target));
        }

        private IEnumerator RotateTowards(Quaternion target)
        {
            float duration = TurnManager.Instance != null ? TurnManager.Instance.TickDuration * 0.1f : 0.1f;
            float elapsed = 0f;
            Quaternion start = transform.rotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(start, target, elapsed / duration);
                yield return null;
            }

            transform.rotation = target;
        }

        public UnitController ScanForEnemies()
        {
            var neighbors = _gridManager.GetNeighbors(_currentCell);
            foreach (var n in neighbors)
            {
                if (n.IsOccupied && n.GetOccupier() is UnitController uc && uc._ownerId != _ownerId && !uc._isDead)
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

        public void SyncFromSnapshot(float newHp, float newStamina, bool isDead)
        {
            _currentHP = newHp;
            _currentStamina = newStamina;
            if (isDead && !_isDead) ExecuteDeath();
        }

        public override void OnDestroy()
        {
            _currentTargetCell?.CapturingUnitIds.Remove(_id);

            _resolver?.UnregisterUnit(this);
            if (GameController.Instance != null)
                GameController.Instance.UnregisterUnit(_id);
            if (!_isDead)
                GameController.Instance?.RemoveUnit(this);

            base.OnDestroy();
        }

        private ClientRpcParams BuildVisibilityRpcParams(int[] alwaysIncludePlayerIds, params CellData[] cells)
        {
            if (_gridManager == null) _gridManager = GridManager.Instance;
            bool fowActive = !GameController.IsDebugMode && _gridManager != null && _gridManager.FogOfWarEnabled;

            var targetPlayerIds = new HashSet<int>();
            foreach (var id in alwaysIncludePlayerIds)
            {
                if (id == -1) continue;
                var p = GameController.Instance.GetPlayerById(id);
                if (p != null && !p.IsAI) targetPlayerIds.Add(id);
            }

            foreach (var player in GameController.Instance.Players)
            {
                if (player.IsAI || targetPlayerIds.Contains(player.Id)) continue;

                if (!fowActive) { targetPlayerIds.Add(player.Id); continue; }

                var visibleCells = _gridManager.GetVisibleCells(player.Id);
                foreach (var cell in cells)
                {
                    if (cell != null && visibleCells.Contains(cell)) { targetPlayerIds.Add(player.Id); break; }
                }
            }

            var targetIds = new List<ulong>();
            foreach (var playerId in targetPlayerIds)
            {
                ulong clientId = GlobalNetworkSettings.Instance != null
                    ? GlobalNetworkSettings.Instance.GetClientIdForPlayer(playerId)
                    : ulong.MaxValue;
                if (clientId != ulong.MaxValue) targetIds.Add(clientId);
            }

            return new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = targetIds } };
        }

        public static bool EvaluateNetworkVisibility(ulong clientId, int ownerId, CellData cell, GridManager gm)
        {
            Debug.Log(
                $"[FOW] Evaluate START: client={clientId}, owner={ownerId}, " +
                $"cell={cell?.Id}, server={NetworkManager.ServerClientId}");

            if (clientId == NetworkManager.ServerClientId)
            {
                Debug.Log(
                    $"[FOW] Evaluate RESULT: client={clientId} -> VISIBLE (server)");
                return true;
            }

            int observerPlayerId = GlobalNetworkSettings.Instance != null
                ? GlobalNetworkSettings.Instance.GetPlayerIdForClient(clientId)
                : -1;

            Debug.Log(
                $"[FOW] Evaluate mapping: client={clientId}, " +
                $"observerPlayer={observerPlayerId}, owner={ownerId}");

            if (observerPlayerId == -1)
            {
                Debug.Log(
                    $"[FOW] Evaluate RESULT: client={clientId} -> HIDDEN (no player mapping)");
                return false;
            }

            if (observerPlayerId == ownerId)
            {
                Debug.Log(
                    $"[FOW] Evaluate RESULT: client={clientId} -> VISIBLE (owner)");
                return true;
            }

            bool fowActive = !GameController.IsDebugMode && gm != null && gm.FogOfWarEnabled;
            if (!fowActive)
            {
                Debug.Log(
                    $"[FOW] Evaluate RESULT: client={clientId} -> VISIBLE (FoW disabled)");
                return true;
            }

            bool visible = gm != null && cell != null && gm.IsCellVisibleToPlayer(cell, observerPlayerId);

            Debug.Log(
                $"[FOW] Evaluate RESULT: client={clientId}, " +
                $"observerPlayer={observerPlayerId}, cell={cell?.Id}, visible={visible}");

            return visible;
        }

        public void RefreshNetworkVisibility(GridManager gm, IReadOnlyList<PlayerProfile> players)
        {
            if (!IsServer || !IsSpawned) return;

            bool fowActive = !GameController.IsDebugMode && gm.FogOfWarEnabled;

            foreach (var player in players)
            {
                if (player.IsAI) continue;

                ulong clientId = GlobalNetworkSettings.Instance != null
                    ? GlobalNetworkSettings.Instance.GetClientIdForPlayer(player.Id)
                    : ulong.MaxValue;

                if (clientId == ulong.MaxValue || clientId == NetworkManager.ServerClientId)
                    continue;

                bool shouldSee = player.Id == _ownerId || !fowActive ||
                    (_currentCell != null && gm.IsCellVisibleToPlayer(_currentCell, player.Id));

                bool currentlyObserving = NetworkObject.IsNetworkVisibleTo(clientId);

                Debug.Log(
                    $"[FOW] Refresh: unit={_id}, owner={_ownerId}, " +
                    $"observerPlayer={player.Id}, client={clientId}, " +
                    $"cell={_currentCell?.Id}, shouldSee={shouldSee}, " +
                    $"currentlyObserving={currentlyObserving}");

                if (shouldSee && !currentlyObserving)
                {
                    Debug.Log(
                        $"[FOW] SHOW: unit={_id} -> client={clientId}");

                    NetworkObject.NetworkShow(clientId);
                }
                else if (!shouldSee && currentlyObserving)
                {
                    Debug.Log(
                        $"[FOW] HIDE: unit={_id} -> client={clientId}");

                    NetworkObject.NetworkHide(clientId);
                }
            }
        }
    }
}