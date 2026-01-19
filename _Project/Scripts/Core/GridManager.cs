using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace GridEmpire.Core
{
    public class GridManager : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int radius = 5;
        [SerializeField] private float hexSize = 1.0f;
        [SerializeField] private GameObject hexPrefab;

        private Dictionary<Vector2Int, CellData> _grid = new Dictionary<Vector2Int, CellData>();
        private Dictionary<CellData, ICellPresenter> _presenterMap = new Dictionary<CellData, ICellPresenter>();

        private void Start()
        {
            GenerateHexGrid();
            SetupPlayers();

            // Kezdõ köd frissítése a helyi játékosnak
            // A GameController-en keresztül érjük el a profilokat
            var localPlayer = GameController.Instance.GetLocalPlayer();
            if (localPlayer != null) UpdateFogOfWar(localPlayer.Id);
        }

        private void GenerateHexGrid()
        {
            _grid.Clear();
            _presenterMap.Clear();
            foreach (Transform child in transform) Destroy(child.gameObject);
            var hexYOffset = new Vector3(0, -0.099f, 0);

            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);

                for (int r = r1; r <= r2; r++)
                {
                    CellData cell = new CellData(q, r);
                    _grid.Add(new Vector2Int(q, r), cell);

                    Vector3 worldPos = GetWorldPosition(q, r) + hexYOffset;
                    GameObject obj = Instantiate(hexPrefab, worldPos, hexPrefab.transform.rotation, transform);
                    obj.name = $"Hex_{q}_{r}";

                    ICellPresenter presenter = obj.GetComponent<ICellPresenter>();
                    if (presenter != null)
                    {
                        presenter.Initialize(cell);
                        _presenterMap.Add(cell, presenter);
                    }
                }
            }
        }

        private void SetupPlayers()
        {
            var players = GameController.Instance.Players;
            if (players == null || players.Count == 0) return;

            int count = players.Count;

            // A hexagon 6 sarka axial koordinátákban
            Vector2Int[] corners = new Vector2Int[]
            {
                new Vector2Int(0, -radius),      // Észak
                new Vector2Int(radius, -radius), // Északkelet
                new Vector2Int(radius, 0),       // Délkelet
                new Vector2Int(0, radius),       // Dél
                new Vector2Int(-radius, radius), // Délnyugat
                new Vector2Int(-radius, 0)       // Északnyugat
            };

            // Szimmetrikus kiosztás a játékosszám alapján
            int[] indices;
            switch (count)
            {
                case 2: indices = new int[] { 0, 3 }; break;
                case 3: indices = new int[] { 0, 2, 4 }; break;
                case 4: indices = new int[] { 0, 1, 3, 4 }; break;
                case 6: indices = new int[] { 0, 1, 2, 3, 4, 5 }; break;
                default: indices = Enumerable.Range(0, count).ToArray(); break;
            }

            for (int i = 0; i < count; i++)
            {
                Vector2Int pos = corners[indices[i]];
                AssignBase(pos.x, pos.y, players[i]);
            }
        }

        private void AssignBase(int q, int r, PlayerProfile player)
        {
            CellData cell = GetCell(q, r);
            if (cell == null) return;

            cell.OwnerId = player.Id;
            cell.IsBase = true;
            cell.UpdateCapture(player.Id, 1.0f);
            player.BaseCell = cell;

            if (_presenterMap.TryGetValue(cell, out var p)) p.UpdateVisual();
        }

        public void UpdateFogOfWar(int forPlayerId)
        {
            foreach (var c in _grid.Values)
                c.CurrentVisibility = VisibilityState.Visible;

            foreach (var p in _presenterMap.Values) p.UpdateVisual();
            return;

            // 1. Minden mezõ, ami eddig látható volt, most "felfedezett" (szürke) lesz
            foreach (var c in _grid.Values)
                if (c.CurrentVisibility == VisibilityState.Visible) c.CurrentVisibility = VisibilityState.Explored;

            var player = GameController.Instance.GetPlayerById(forPlayerId);
            if (player == null) return;

            // 2. Bázis körüli látás
            if (player.BaseCell != null)
            {
                player.BaseCell.CurrentVisibility = VisibilityState.Visible;
                foreach (var n in GetNeighbors(player.BaseCell)) n.CurrentVisibility = VisibilityState.Visible;
            }

            // 3. Egységek körüli látás (Interfészen keresztül!)
            foreach (IUnit unit in player.ActiveUnits)
            {
                if (unit != null && !unit.IsDead && unit.CurrentCell != null)
                {
                    unit.CurrentCell.CurrentVisibility = VisibilityState.Visible;
                    foreach (var n in GetNeighbors(unit.CurrentCell)) n.CurrentVisibility = VisibilityState.Visible;
                }
            }

            // 4. Vizuális frissítés
            foreach (var p in _presenterMap.Values) p.UpdateVisual();
        }

        // --- Segédfüggvények ---

        public CellData GetCell(int q, int r) => _grid.GetValueOrDefault(new Vector2Int(q, r));
        public int GetDistance(CellData a, CellData b)
        {
            return (Mathf.Abs(a.Q - b.Q) + Mathf.Abs(a.Q + a.R - (b.Q + b.R)) + Mathf.Abs(a.R - b.R)) / 2;
        }
        public IEnumerable<CellData> GetAllCells() => _grid.Values;

        public List<CellData> GetNeighbors(CellData c)
        {
            List<CellData> neighbors = new List<CellData>();
            if (c == null) return neighbors;

            Vector2Int[] directions = {
                new Vector2Int(0, -1), new Vector2Int(1, -1), new Vector2Int(1, 0),
                new Vector2Int(0, 1), new Vector2Int(-1, 1), new Vector2Int(-1, 0)
            };

            foreach (var d in directions)
            {
                CellData neighbor = GetCell(c.Q + d.x, c.R + d.y);
                if (neighbor != null) neighbors.Add(neighbor);
            }
            return neighbors;
        }

        public void FinalizeCapture(CellData cell, int playerId)
        {
            cell.OwnerId = playerId;

            // Ha a helyi játékos foglalt, frissítsük a ködöt azonnal
            var localPlayer = GameController.Instance.GetLocalPlayer();
            if (localPlayer != null && localPlayer.Id == playerId) UpdateFogOfWar(playerId);

            if (_presenterMap.TryGetValue(cell, out var p)) p.UpdateVisual();
        }

        public Vector3 GetWorldPosition(int q, int r)
        {
            float radiusValue = hexSize * 0.5f;
            float x = radiusValue * (Mathf.Sqrt(3f) * q + (Mathf.Sqrt(3f) / 2f) * r);
            float z = radiusValue * (1.5f * r);
            return new Vector3(x, 0, z);
        }

        public CellData GetCellAtPosition(Vector3 worldPosition)
        {
            float size = hexSize * 0.5f;
            float x = worldPosition.x;
            float z = worldPosition.z;

            float q = (Mathf.Sqrt(3f) / 3f * x - 1f / 3f * z) / size;
            float r = (2f / 3f * z) / size;

            Vector2Int hex = RoundToHex(q, r);
            return GetCell(hex.x, hex.y);
        }

        private Vector2Int RoundToHex(float q, float r)
        {
            float s = -q - r;
            int rq = Mathf.RoundToInt(q);
            int rr = Mathf.RoundToInt(r);
            int rs = Mathf.RoundToInt(s);

            float q_diff = Mathf.Abs(rq - q);
            float r_diff = Mathf.Abs(rr - r);
            float s_diff = Mathf.Abs(rs - s);

            if (q_diff > r_diff && q_diff > s_diff) rq = -rr - rs;
            else if (r_diff > s_diff) rr = -rq - rs;

            return new Vector2Int(rq, rr);
        }

        public CellData GetNeighborInDirection(CellData fromCell, int directionIndex)
        {
            int[] dq = { 0, 1, 1, 0, -1, -1 };
            int[] dr = { -1, -1, 0, 1, 1, 0 };

            int dir = directionIndex % 6;
            if (dir < 0) dir += 6;

            return GetCell(fromCell.Q + dq[dir], fromCell.R + dr[dir]);
        }

        public int GetDirectionFromCells(CellData from, CellData to)
        {
            int dq = to.Q - from.Q;
            int dr = to.R - from.R;

            if (dq == 0 && dr == -1) return 0; // É
            if (dq == 1 && dr == -1) return 1; // ÉK
            if (dq == 1 && dr == 0) return 2; // DK
            if (dq == 0 && dr == 1) return 3; // D
            if (dq == -1 && dr == 1) return 4; // DNY
            if (dq == -1 && dr == 0) return 5; // ÉNY
            return 0;
        }

        public List<CellData> FindPath(CellData start, CellData target)
        {
            if (start == null || target == null) return null;
            if (start == target) return new List<CellData>(); // Ha már ott vagyunk

            var frontier = new Queue<CellData>();
            frontier.Enqueue(start);
            var cameFrom = new Dictionary<CellData, CellData> { { start, null } };

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                if (current == target) break;

                foreach (var next in GetNeighbors(current))
                {
                    if (!cameFrom.ContainsKey(next))
                    {
                        frontier.Enqueue(next);
                        cameFrom[next] = current;
                    }
                }
            }

            if (!cameFrom.ContainsKey(target)) return null;

            var path = new List<CellData>();
            for (var curr = target; curr != start && curr != null; curr = cameFrom[curr])
            {
                path.Add(curr);
            }

            path.Reverse();
            return path;
        }
    }
}