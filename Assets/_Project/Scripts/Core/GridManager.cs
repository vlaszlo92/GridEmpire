using GridEmpire.Networking;
using GridEmpire.Shared;
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

        [Header("Grid Settings")]
        [SerializeField] private int radius = 5;
        [SerializeField] private float hexSize = 1.0f;
        [SerializeField] private GameObject hexPrefab;

        public static bool IsDebugMode = GameController.IsDebugMode;

        private readonly List<CellData> _neighborBuffer = new List<CellData>(6);
        private Dictionary<Vector2Int, CellData> _grid = new Dictionary<Vector2Int, CellData>();
        private Dictionary<CellData, ICellPresenter> _presenterMap = new Dictionary<CellData, ICellPresenter>();
        private Dictionary<int, CellData> _cellByIdLookup = new Dictionary<int, CellData>();

        Vector2Int[] Directions = {
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
            Debug.Log($"[GridManager] OnNetworkSpawn. IsServer={IsServer}");
            // Kliens oldalon a grid már szerver által lett generálva és spawnolva
            // Csak generálni kell kliens oldalon is a vizuált
            if (!IsServer)
            {
                if (GlobalNetworkSettings.Instance != null)
                    GenerateGrid(GlobalNetworkSettings.Instance.NetworkMapRadius.Value);
                else
                    Debug.LogError("[GridManager] GlobalNetworkSettings NULL kliens oldalon!");
            }

            IsReady = true;
            Debug.Log("[GridManager] IsReady = true.");
        }

        public void GenerateGrid(int radius)
        {
            this.radius = radius;
            GenerateHexGrid();
            Debug.Log($"[GridManager] Grid generálva: radius={radius}, cellák={_presenterMap.Count}");
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

        /// <summary>GameController.AssignBaseCells hívja – vizuál frissítése egy cellán.</summary>
        public void RefreshCell(CellData cell)
        {
            if (_presenterMap.TryGetValue(cell, out var presenter))
                presenter.UpdateVisual();
            else
                Debug.LogWarning($"[GridManager] RefreshCell: presenter nem található, cell={cell.Id}");
        }

        public void UpdateFogOfWar(int forPlayerId)
        {
            if (IsDebugMode || !GlobalNetworkSettings.Instance.FogOfWarEnabled.Value)
            {
                foreach (var c in _grid.Values) c.CurrentVisibility = VisibilityState.Visible;
                foreach (var p in _presenterMap.Values) p.UpdateVisual();
                return;
            }

            foreach (var c in _grid.Values)
                if (c.CurrentVisibility == VisibilityState.Visible)
                    c.CurrentVisibility = VisibilityState.Explored;

            var player = GameController.Instance.GetPlayerById(forPlayerId);
            if (player == null) return;

            if (player.BaseCell != null)
            {
                player.BaseCell.CurrentVisibility = VisibilityState.Visible;
                foreach (var n in GetNeighbors(player.BaseCell)) n.CurrentVisibility = VisibilityState.Visible;
            }

            foreach (IUnit unit in player.ActiveUnits)
            {
                if (unit != null && !unit.IsDead && unit.CurrentCell != null)
                {
                    unit.CurrentCell.CurrentVisibility = VisibilityState.Visible;
                    foreach (var n in GetNeighbors(unit.CurrentCell)) n.CurrentVisibility = VisibilityState.Visible;
                }
            }

            foreach (var p in _presenterMap.Values) p.UpdateVisual();
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
            return new Vector3(rv * (Mathf.Sqrt(3f) * q + (Mathf.Sqrt(3f) / 2f) * r), 0, rv * (1.5f * r));
        }

        public CellData GetCellAtPosition(Vector3 worldPosition)
        {
            float size = hexSize * 0.5f;
            float q = (Mathf.Sqrt(3f) / 3f * worldPosition.x - 1f / 3f * worldPosition.z) / size;
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
                foreach (var next in GetNeighbors(current))
                    if (!cameFrom.ContainsKey(next)) { frontier.Enqueue(next); cameFrom[next] = current; }
            }

            if (!cameFrom.ContainsKey(target)) return null;

            var path = new List<CellData>();
            for (var curr = target; curr != start && curr != null; curr = cameFrom[curr])
                path.Add(curr);
            path.Reverse();
            return path;
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