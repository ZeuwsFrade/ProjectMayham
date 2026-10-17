using System.Collections;
using System.Collections.Generic;
using ProjectMayham.World;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectMayham.AI
{
    /// <summary>
    /// The map enemies find their way on: a grid of square cells, each free or blocked, and A* over it.
    /// <para>
    /// Chosen over a NavMesh package on purpose. The level is a torus (<see cref="WrapWorld"/>): a path may run
    /// across the seam, which a baked mesh knows nothing about. The level is built of tiles, so a grid is its natural
    /// shape. Barricades move and are taken apart, so the grid is re-sampled all the time, a few cells every frame.
    /// And it needs no extra package.
    /// </para>
    /// <para>
    /// Cells are blocked when something on <see cref="obstacleMask"/> touches them. Paths are given as points in the
    /// world of whoever asks: across the seam they simply continue on the other side, so an enemy follows them
    /// without knowing that the level wraps.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class NavGrid : MonoBehaviour
    {
        private const float Diagonal = 1.41421356f;

        private static readonly Vector2Int[] Steps =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        public static NavGrid Active { get; private set; }

        [Tooltip("Layers that block movement: walls, windows, furniture. Barricades too, if they are on one of these.")]
        [SerializeField] private LayerMask obstacleMask;
        [Tooltip("Without a WrapWorld: world position of the lower-left corner of the grid. Left at zero size, the grid is " +
                 "fitted around all tilemaps of the scene.")]
        [SerializeField] private Vector2 manualMin;
        [SerializeField] private Vector2 manualSize;
        [Tooltip("Half the width of the smallest body that has to fit through: paths keep at least this far from walls.")]
        [SerializeField, Min(0.1f)] private float agentRadius = 0.38f;
        [Tooltip("Cells re-sampled per frame, to notice barricades that were pushed or taken apart.")]
        [SerializeField, Min(0)] private int refreshPerFrame = 250;
        [Tooltip("Searches that may run in one frame; enemies that do not get one wait for the next.")]
        [SerializeField, Min(1)] private int searchesPerFrame = 4;
        [Tooltip("A search gives up after looking at this many cells.")]
        [SerializeField, Min(100)] private int maxExpansions = 40000;

        private bool[] blocked;
        private int width, height;
        private Vector2 min;
        private bool wrap;
        private bool ready;
        private int refreshCursor;
        private int searchesThisFrame;
        private readonly Collider2D[] probe = new Collider2D[1];
        private ContactFilter2D filter;

        // A* working memory, reused by every search.
        private float[] cost;
        private int[] parent;
        private int[] seen;
        private int stamp;
        private int[] heapNode = new int[1024];
        private float[] heapKey = new float[1024];
        private int heapCount;

        public bool IsReady => ready;
        /// <summary>True when the level wraps around and paths may cross the seam.</summary>
        public bool Wraps => wrap;
        public bool HasSearchBudget => searchesThisFrame < searchesPerFrame;
        public LayerMask ObstacleMask => obstacleMask;
        public float AgentRadius => agentRadius;
        /// <summary>Incremented whenever a cell changes between free and blocked.</summary>
        public int Version { get; private set; }

        private void Awake() => Active = this;

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        private IEnumerator Start()
        {
            // Colliders of tilemaps and of rooms placed at the start take a physics step to exist.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Build();
        }

        private void Update()
        {
            searchesThisFrame = 0;
            if (ready) Refresh();
        }

        // ---- grid ----

        /// <summary>Samples every cell of the level. Takes a few tens of milliseconds for a level of 150 x 150.</summary>
        public void Build()
        {
            var wrapWorld = WrapWorld.Active;
            wrap = wrapWorld != null;
            if (wrap)
            {
                min = wrapWorld.Min;
                width = Mathf.Max(1, Mathf.RoundToInt(wrapWorld.Size.x));
                height = Mathf.Max(1, Mathf.RoundToInt(wrapWorld.Size.y));
            }
            else if (manualSize.x > 0f && manualSize.y > 0f)
            {
                min = manualMin;
                width = Mathf.RoundToInt(manualSize.x);
                height = Mathf.RoundToInt(manualSize.y);
            }
            else if (!FitToTilemaps()) return;

            int count = width * height;
            blocked = new bool[count];
            cost = new float[count];
            parent = new int[count];
            seen = new int[count];
            stamp = 0;

            filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(obstacleMask);
            Physics2D.SyncTransforms();

            float started = Time.realtimeSinceStartup;
            int blockedCount = 0;
            for (int i = 0; i < count; i++)
            {
                blocked[i] = Sample(i);
                if (blocked[i]) blockedCount++;
            }

            ready = true;
            Version++;
            Debug.Log($"NavGrid: {width}x{height} cells{(wrap ? " (wrapped)" : "")}, {blockedCount} blocked, built in {(Time.realtimeSinceStartup - started) * 1000f:0} ms");
        }

        private bool FitToTilemaps()
        {
            var maps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            bool any = false;
            Bounds total = default;
            foreach (var map in maps)
            {
                map.CompressBounds();
                var b = map.cellBounds;
                if (b.size.x <= 0 || b.size.y <= 0) continue;
                Vector3 lo = map.CellToWorld(b.min), hi = map.CellToWorld(b.max);
                var box = new Bounds((lo + hi) * 0.5f, hi - lo);
                if (!any) { total = box; any = true; }
                else total.Encapsulate(box);
            }
            if (!any)
            {
                Debug.LogWarning("NavGrid: no WrapWorld, no manual size and no tilemaps: there is nothing to build a grid on.", this);
                return false;
            }

            min = new Vector2(Mathf.Floor(total.min.x), Mathf.Floor(total.min.y));
            width = Mathf.CeilToInt(total.max.x - min.x);
            height = Mathf.CeilToInt(total.max.y - min.y);
            return true;
        }

        private bool Sample(int index)
        {
            Vector2 center = CellCenter(index % width, index / width);
            // A hair smaller than the cell: a wall that only touches the edge does not block it.
            return Physics2D.OverlapBox(center, new Vector2(0.9f, 0.9f), 0f, filter, probe) > 0;
        }

        private void Refresh()
        {
            int total = blocked.Length;
            for (int n = 0; n < refreshPerFrame; n++)
            {
                refreshCursor = (refreshCursor + 1) % total;
                bool now = Sample(refreshCursor);
                if (now == blocked[refreshCursor]) continue;
                blocked[refreshCursor] = now;
                Version++;
            }
        }

        private Vector2 CellCenter(int x, int y) => new Vector2(min.x + x + 0.5f, min.y + y + 0.5f);

        private int Wrapped(int value, int size) => ((value % size) + size) % size;

        /// <summary>True when the cell (in unwrapped integer coordinates) lies on the grid and is free.</summary>
        private bool IsFreeCell(int ux, int uy)
        {
            if (!wrap && (ux < 0 || uy < 0 || ux >= width || uy >= height)) return false;
            return !blocked[Wrapped(uy, height) * width + Wrapped(ux, width)];
        }

        private Vector2Int UnwrappedCell(Vector2 world) =>
            new Vector2Int(Mathf.FloorToInt(world.x - min.x), Mathf.FloorToInt(world.y - min.y));

        // ---- queries ----

        /// <summary>True when the point is on free ground (the grid has to be built).</summary>
        public bool IsWalkable(Vector2 point)
        {
            if (!ready) return false;
            var cell = UnwrappedCell(point);
            return IsFreeCell(cell.x, cell.y);
        }

        /// <summary>A free spot anywhere on the grid, at random. False after a few tries.</summary>
        public bool TryRandomPoint(out Vector2 point)
        {
            point = default;
            if (!ready) return false;

            for (int i = 0; i < 60; i++)
            {
                int x = Random.Range(0, width), y = Random.Range(0, height);
                if (blocked[y * width + x]) continue;
                point = CellCenter(x, y);
                return true;
            }
            return false;
        }

        /// <summary>A free spot at random within <paramref name="radius"/> of a point. False after a few tries.</summary>
        public bool TryRandomPoint(Vector2 near, float radius, out Vector2 point)
        {
            point = near;
            if (!ready) return false;

            for (int i = 0; i < 12; i++)
            {
                Vector2 candidate = near + Random.insideUnitCircle * radius;
                var cell = UnwrappedCell(candidate);
                if (!IsFreeCell(cell.x, cell.y)) continue;
                point = new Vector2(min.x + cell.x + 0.5f, min.y + cell.y + 0.5f);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Finds a way from one point to another and puts its corner points into <paramref name="result"/> (the start
        /// itself is not in it, the end is the target). Returns false when there is none, the grid is not ready or
        /// the search budget of the frame is spent (<see cref="HasSearchBudget"/>).
        /// </summary>
        public bool TryFindPath(Vector2 from, Vector2 to, List<Vector2> result)
        {
            result.Clear();
            if (!ready || !HasSearchBudget) return false;
            searchesThisFrame++;

            // The goal as the copy of the target that is nearest to the start, so that the way over the seam is found too.
            Vector2 target = wrap ? from + WrapWorld.Active.Delta(from, to) : to;

            var start = UnwrappedCell(from);
            var goal = UnwrappedCell(target);
            // A target inside a wall (a player pressed against it) is reached at the nearest free cell.
            bool targetIsFree = IsFreeCell(goal.x, goal.y);
            if (!NearestFree(start, out start) || !NearestFree(goal, out goal)) return false;

            int goalId = Wrapped(goal.y, height) * width + Wrapped(goal.x, width);
            int startId = Wrapped(start.y, height) * width + Wrapped(start.x, width);

            if (startId == goalId)
            {
                if (targetIsFree) result.Add(target);
                else result.Add(CellCenter(goal.x, goal.y));
                return true;
            }
            if (!Search(startId, goalId)) return false;

            // Walk back from the goal, then lay the cells out in the world of the asker.
            var ids = new List<int>();
            for (int id = goalId; id != startId; id = parent[id]) ids.Add(id);
            ids.Add(startId);
            ids.Reverse();

            var points = new List<Vector2>(ids.Count + 1);
            Vector2 position = CellCenter(start.x, start.y);
            points.Add(position);
            for (int i = 1; i < ids.Count; i++)
            {
                int dx = ids[i] % width - ids[i - 1] % width;
                int dy = ids[i] / width - ids[i - 1] / width;
                // Over the seam the index jumps by a whole width; the step itself is one cell.
                if (dx > width / 2) dx -= width; else if (dx < -width / 2) dx += width;
                if (dy > height / 2) dy -= height; else if (dy < -height / 2) dy += height;
                position += new Vector2(dx, dy);
                points.Add(position);
            }

            // The exact target at the end, not the middle of its cell.
            if (targetIsFree)
            {
                Vector2 last = points[points.Count - 1];
                points[points.Count - 1] = last + (wrap ? WrapWorld.Active.Delta(last, target) : target - last);
            }

            Smooth(points, result);
            return result.Count > 0;
        }

        /// <summary>The cell itself when it is free, else the nearest free one within a few cells.</summary>
        private bool NearestFree(Vector2Int cell, out Vector2Int free)
        {
            free = cell;
            if (IsFreeCell(cell.x, cell.y)) return true;

            for (int ring = 1; ring <= 3; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    for (int dy = -ring; dy <= ring; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue;
                        if (!IsFreeCell(cell.x + dx, cell.y + dy)) continue;
                        free = new Vector2Int(cell.x + dx, cell.y + dy);
                        return true;
                    }
                }
            }
            return false;
        }

        // ---- A* ----

        private bool Search(int startId, int goalId)
        {
            stamp++;
            heapCount = 0;
            cost[startId] = 0f;
            parent[startId] = startId;
            seen[startId] = stamp;
            Push(startId, Heuristic(startId, goalId));

            int expansions = 0;
            while (heapCount > 0)
            {
                float key = heapKey[0];
                int current = Pop();
                if (current == goalId) return true;
                // A cell can be queued twice; the older, worse entry is skipped.
                if (key > cost[current] + Heuristic(current, goalId) + 0.0001f) continue;
                if (++expansions > maxExpansions) return false;

                int cx = current % width, cy = current / width;
                for (int s = 0; s < Steps.Length; s++)
                {
                    var step = Steps[s];
                    int nx = cx + step.x, ny = cy + step.y;
                    if (!wrap && (nx < 0 || ny < 0 || nx >= width || ny >= height)) continue;
                    int wx = Wrapped(nx, width), wy = Wrapped(ny, height);
                    int next = wy * width + wx;
                    if (blocked[next]) continue;
                    // No cutting of corners: both cells beside a diagonal step must be free too.
                    if (step.x != 0 && step.y != 0
                        && (blocked[wy * width + Wrapped(cx, width)] || blocked[Wrapped(cy, height) * width + wx])) continue;

                    float stepCost = cost[current] + (step.x != 0 && step.y != 0 ? Diagonal : 1f);
                    if (seen[next] == stamp && stepCost >= cost[next]) continue;

                    seen[next] = stamp;
                    cost[next] = stepCost;
                    parent[next] = current;
                    Push(next, stepCost + Heuristic(next, goalId));
                }
            }
            return false;
        }

        private float Heuristic(int a, int b)
        {
            int dx = Mathf.Abs(a % width - b % width), dy = Mathf.Abs(a / width - b / width);
            if (wrap)
            {
                dx = Mathf.Min(dx, width - dx);
                dy = Mathf.Min(dy, height - dy);
            }
            int lo = Mathf.Min(dx, dy), hi = Mathf.Max(dx, dy);
            return hi + (Diagonal - 1f) * lo;
        }

        private void Push(int node, float key)
        {
            if (heapCount == heapNode.Length)
            {
                System.Array.Resize(ref heapNode, heapCount * 2);
                System.Array.Resize(ref heapKey, heapCount * 2);
            }

            int i = heapCount++;
            while (i > 0)
            {
                int up = (i - 1) / 2;
                if (heapKey[up] <= key) break;
                heapNode[i] = heapNode[up];
                heapKey[i] = heapKey[up];
                i = up;
            }
            heapNode[i] = node;
            heapKey[i] = key;
        }

        private int Pop()
        {
            int top = heapNode[0];
            heapCount--;
            if (heapCount == 0) return top;

            int node = heapNode[heapCount];
            float key = heapKey[heapCount];
            int i = 0;
            while (true)
            {
                int child = i * 2 + 1;
                if (child >= heapCount) break;
                if (child + 1 < heapCount && heapKey[child + 1] < heapKey[child]) child++;
                if (heapKey[child] >= key) break;
                heapNode[i] = heapNode[child];
                heapKey[i] = heapKey[child];
                i = child;
            }
            heapNode[i] = node;
            heapKey[i] = key;
            return top;
        }

        // ---- path smoothing ----

        /// <summary>Keeps only the corners: a point stays when the straight line from the last kept one to the next is blocked.</summary>
        private void Smooth(List<Vector2> points, List<Vector2> result)
        {
            int anchor = 0;
            while (anchor < points.Count - 1)
            {
                int far = anchor + 1;
                for (int i = points.Count - 1; i > anchor + 1; i--)
                {
                    if (!LineIsFree(points[anchor], points[i])) continue;
                    far = i;
                    break;
                }
                result.Add(points[far]);
                anchor = far;
            }
        }

        /// <summary>True when a body of the agent's width can go straight from one point to the other.</summary>
        private bool LineIsFree(Vector2 a, Vector2 b)
        {
            Vector2 line = b - a;
            float length = line.magnitude;
            if (length < 0.01f) return true;

            Vector2 along = line / length;
            Vector2 side = new Vector2(-along.y, along.x) * agentRadius;
            int samples = Mathf.CeilToInt(length / 0.25f);
            for (int i = 0; i <= samples; i++)
            {
                Vector2 p = a + along * (length * i / samples);
                if (!IsWalkable(p) || !IsWalkable(p + side) || !IsWalkable(p - side)) return false;
            }
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (!ready) return;
            var cam = Camera.main;
            if (cam == null) return;

            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.3f);
            Vector2 centre = cam.transform.position;
            int cx = Mathf.FloorToInt(centre.x - min.x), cy = Mathf.FloorToInt(centre.y - min.y);
            for (int dx = -25; dx <= 25; dx++)
            {
                for (int dy = -15; dy <= 15; dy++)
                {
                    if (IsFreeCell(cx + dx, cy + dy)) continue;
                    Gizmos.DrawCube(new Vector3(min.x + cx + dx + 0.5f, min.y + cy + dy + 0.5f, 0f), new Vector3(0.9f, 0.9f, 0.1f));
                }
            }
        }
    }
}
