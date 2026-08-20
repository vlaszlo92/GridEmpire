using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace GridEmpire.Core
{
    public class GridManager : NetworkBehaviour
    {
        public static GridManager Instance { get; private set; }
        public bool IsReady { get; private set; } = false;

        /// <summary>The Networking layer sets this before calling GenerateGrid.</summary>
        public bool FogOfWarEnabled { get; set; } = true;

        public static event System.Action OnVisibilityUpdated;
        public static event System.Action OnGridReady;

        [Header("Grid Settings")]
        [SerializeField] private int radius = 5;
        [SerializeField] private float hexSize = 1.0f;
        [SerializeField] private GameObject hexPrefab;

        public static bool IsDebugMode = GameController.IsDebugMode;

        private readonly List<CellData> _neighborBuffer = new List<CellData>(6);
        private Dictionary<Vector2Int, CellData> _grid = new Dictionary<Vector2Int, CellData>();
        private Dictionary<CellData, ICellPresenter> _presenterMap = new Dictionary<CellData, ICellPresenter>();
        private Dictionary<int, CellData> _cellByIdLookup = new Dictionary<int, CellData>();

        private static readonly float Sqrt3 = Mathf.Sqrt(3f);
        public Vector3 GetMapCenterWorldPosition() => GetWorldPosition(0, 0);

        private static readonly Vector2Int[] Directions = {
            new Vector2Int(0, -1), new Vector2Int(1, -1), new Vector2Int(1, 0),
            new Vector2Int(0, 1),  new Vector2Int(-1, 1), new Vector2Int(-1, 0)
        };

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            // On the server, the grid is already generated (GameController.ServerInitChain called GenerateGrid)
            // before Spawn(), which also sets IsReady=true).
            // On the client, the Networking layer (GameNetworkBridge) calls GenerateGrid,
            // once it receives the necessary settings.
            Debug.Log($"[GridManager] OnNetworkSpawn. IsServer={IsServer}, IsReady={IsReady}");
        }

        public void GenerateGrid(int radius)
        {
            this.radius = radius;
            GenerateHexGrid();
            IsReady = true;
            Debug.Log($"[GridManager] Grid generated: radius={radius}, cells={_presenterMap.Count}");
            OnGridReady?.Invoke();
        }

        private void GenerateHexGrid()
        {
            _grid.Clear();
            _presenterMap.Clear();
            _cellByIdLookup.Clear();
            foreach (Transform child in transform) Destroy(child.gameObject);

            var hexYOffset = new Vector3(0, -0.099f, 0);
            int cellCounter = 0;

            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    CellData cell = new CellData(q, r, cellCounter++);
                    _grid.Add(new Vector2Int(q, r), cell);
                    _cellByIdLookup.Add(cell.Id, cell);

                    Vector3 worldPos = GetWorldPosition(q, r) + hexYOffset;
                    GameObject obj = Instantiate(hexPrefab, worldPos, hexPrefab.transform.rotation, transform);
                    obj.name = $"Hex_{cell.Id} ({q}_{r})";

                    ICellPresenter presenter = obj.GetComponent<ICellPresenter>();
                    if (presenter != null)
                    {
                        presenter.Initialize(cell);
                        _presenterMap.Add(cell, presenter);
                    }
                }
            }
        }

        /// <summary>GameController.AssignBaseCells calls this – updates the visual representation of a cell.</summary>
        public void RefreshCell(CellData cell)
        {
            if (_presenterMap.TryGetValue(cell, out var presenter))
                presenter.UpdateVisual();
            else
                Debug.LogWarning($"[GridManager] RefreshCell: presenter not found, cell={cell.Id}");
        }

        public void UpdateFogOfWar(int forPlayerId)
        {
            // Debug / FoW kikapcsolva
            if (IsDebugMode || !FogOfWarEnabled)
            {
                foreach (var (cell, presenter) in _presenterMap)
                {
                    cell.CurrentVisibility = VisibilityState.Visible;
                    presenter.UpdateVisual();
                }
                GameController.Instance.UpdateUnitVisibility(null, forPlayerId);
                return;
            }

            // 1. Visible cells collection
            var visibleCells = GetVisibleCells(forPlayerId);

            // 2. Cell visibility + presenter update – in a single loop
            foreach (var (cell, presenter) in _presenterMap)
            {
                cell.CurrentVisibility = visibleCells.Contains(cell)
                    ? VisibilityState.Visible
                    : (cell.OwnerId == forPlayerId ? VisibilityState.Explored : VisibilityState.Hidden);

                presenter.UpdateVisual();
            }

            // 3. Unit visibility
            GameController.Instance.UpdateUnitVisibility(visibleCells, forPlayerId);

            OnVisibilityUpdated?.Invoke();
        }

        /// <summary>Gets the cells currently visible to a player (their base + unit range).
        /// Can be used on the server for network state filtering, not just for visualization.</summary>
        public HashSet<CellData> GetVisibleCells(int forPlayerId)
        {
            var visibleCells = new HashSet<CellData>();
            var player = GameController.Instance.GetPlayerById(forPlayerId);
            if (player == null) return visibleCells;

            if (player.BaseCell != null)
            {
                visibleCells.Add(player.BaseCell);
                foreach (var n in GetNeighbors(player.BaseCell)) visibleCells.Add(n);
            }

            foreach (IUnit unit in player.ActiveUnits)
            {
                if (unit == null || unit.IsDead || unit.CurrentCell == null) continue;
                visibleCells.Add(unit.CurrentCell);
                foreach (var n in GetNeighbors(unit.CurrentCell)) visibleCells.Add(n);
            }

            return visibleCells;
        }

        public void FinalizeCapture(CellData cell, int playerId)
        {
            cell.OwnerId = playerId;
            var localPlayer = GameController.Instance.GetLocalPlayer();
            if (localPlayer != null && localPlayer.Id == playerId) UpdateFogOfWar(playerId);
            if (_presenterMap.TryGetValue(cell, out var p)) p.UpdateVisual();
        }

        public CellData GetCell(int q, int r) => _grid.GetValueOrDefault(new Vector2Int(q, r));
        public CellData GetCellById(int id) => _cellByIdLookup.GetValueOrDefault(id);
        public IEnumerable<CellData> GetAllCells() => _grid.Values;

        public int GetDistance(CellData a, CellData b) =>
            (Mathf.Abs(a.Q - b.Q) + Mathf.Abs(a.Q + a.R - (b.Q + b.R)) + Mathf.Abs(a.R - b.R)) / 2;

        public List<CellData> GetNeighbors(CellData c)
        {
            _neighborBuffer.Clear();
            if (c == null) return _neighborBuffer;
            foreach (var d in Directions)
            {
                CellData n = GetCell(c.Q + d.x, c.R + d.y);
                if (n != null) _neighborBuffer.Add(n);
            }
            return _neighborBuffer;
        }

        public CellData GetNeighborInDirection(CellData fromCell, int directionIndex)
        {
            int[] dq = { 0, 1, 1, 0, -1, -1 };
            int[] dr = { -1, -1, 0, 1, 1, 0 };
            int dir = ((directionIndex % 6) + 6) % 6;
            return GetCell(fromCell.Q + dq[dir], fromCell.R + dr[dir]);
        }

        public int GetDirectionFromCells(CellData from, CellData to)
        {
            int dq = to.Q - from.Q, dr = to.R - from.R;
            if (dq == 0 && dr == -1) return 0;
            if (dq == 1 && dr == -1) return 1;
            if (dq == 1 && dr == 0) return 2;
            if (dq == 0 && dr == 1) return 3;
            if (dq == -1 && dr == 1) return 4;
            if (dq == -1 && dr == 0) return 5;
            return 0;
        }

        public Vector3 GetWorldPosition(int q, int r)
        {
            float rv = hexSize * 0.5f;
            return new Vector3(rv * (Sqrt3 * q + (Sqrt3 / 2f) * r), 0, rv * (1.5f * r));
        }

        public CellData GetCellAtPosition(Vector3 worldPosition)
        {
            float size = hexSize * 0.5f;
            float q = (Sqrt3 / 3f * worldPosition.x - 1f / 3f * worldPosition.z) / size;
            float r = (2f / 3f * worldPosition.z) / size;
            return GetCell(RoundToHex(q, r).x, RoundToHex(q, r).y);
        }

        private Vector2Int RoundToHex(float q, float r)
        {
            float s = -q - r;
            int rq = Mathf.RoundToInt(q), rr = Mathf.RoundToInt(r), rs = Mathf.RoundToInt(s);
            float qd = Mathf.Abs(rq - q), rd = Mathf.Abs(rr - r), sd = Mathf.Abs(rs - s);
            if (qd > rd && qd > sd) rq = -rr - rs;
            else if (rd > sd) rr = -rq - rs;
            return new Vector2Int(rq, rr);
        }

        public List<CellData> FindPath(CellData start, CellData target)
        {
            if (start == null || target == null) return null;
            if (start == target) return new List<CellData>();

            var frontier = new Queue<CellData>();
            frontier.Enqueue(start);
            var cameFrom = new Dictionary<CellData, CellData> { { start, null } };

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                if (current == target) break;

                var neighbors = GetNeighbors(current);
                ShuffleInPlace(neighbors);
                foreach (var next in neighbors)
                    if (!cameFrom.ContainsKey(next)) { frontier.Enqueue(next); cameFrom[next] = current; }
            }

            if (!cameFrom.ContainsKey(target)) return null;

            var path = new List<CellData>();
            for (var curr = target; curr != start && curr != null; curr = cameFrom[curr])
                path.Add(curr);
            path.Reverse();
            return path;
        }

        private static void ShuffleInPlace(List<CellData> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public void DebugGiveAllCellsToPlayer(int playerId, int exceptPlayerId)
        {
            foreach (var cell in _grid.Values)
            {
                if (cell.IsBase && cell.OwnerId == exceptPlayerId) continue;
                cell.OwnerId = playerId;
                cell.SetInfluence(playerId, 1f);
                if (_presenterMap.TryGetValue(cell, out var p)) p.UpdateVisual();
            }
        }
    }
}