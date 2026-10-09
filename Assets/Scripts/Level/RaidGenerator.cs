using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Items;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectMayham.Level
{
    /// <summary>
    /// Fills the room slots of the corridor template with rooms, blocks some corridors with barricades, scatters
    /// loot and breaks a few holes through the walls of the maze. Runs when the scene starts, before
    /// <see cref="World.WrapWorld"/> copies the tilemaps around the seam, so every raid has a different set of rooms
    /// in the same places. The same seed always gives the same raid.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class RaidGenerator : MonoBehaviour
    {
        /// <summary>A room that was put into a slot.</summary>
        public class PlacedRoom
        {
            public RoomSlot Slot;
            public RoomKind Kind;
            public RoomTemplate Prefab;
            public GameObject Instance;
            public int LootCount;
        }

        private const string RootName = "Raid";

        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        [Header("Level")]
        [SerializeField] private RoomLibrary library;
        [SerializeField] private Tilemap floorMap;
        [SerializeField] private Tilemap wallMap;
        [Tooltip("Tiles that stop movement but not sight.")]
        [SerializeField] private Tilemap windowMap;

        [Header("Template grid (set by the template builder)")]
        [Tooltip("World cell of the bottom-left tile of the level.")]
        [SerializeField] private Vector2Int gridOrigin;
        [Tooltip("Maze cells per side. A cell is a wall line and the tiles of floor behind it.")]
        [SerializeField, Min(1)] private int cells = 39;

        [Header("Barricades")]
        [Tooltip("Prefabs that lie along their X axis and are as wide as a corridor.")]
        [SerializeField] private GameObject[] barricadePrefabs;
        [SerializeField, Min(0)] private int barricadeCount = 12;
        [Tooltip("Cells between two barricades.")]
        [SerializeField, Min(1)] private int barricadeSpacing = 4;

        [Header("Holes in walls")]
        [Tooltip("Prefabs that lie along their X axis and fill one tile of a wall.")]
        [SerializeField] private GameObject[] holePrefabs;
        [SerializeField, Min(0)] private int holeCount = 12;
        [Tooltip("Cells between two holes.")]
        [SerializeField, Min(1)] private int holeSpacing = 6;
        [Tooltip("A hole is only made where walking to the other side of the wall takes at least this many cells.")]
        [SerializeField, Min(2)] private int holeMinDetour = 12;

        [Header("Corridor loot")]
        [SerializeField] private LootTable corridorLoot;
        [SerializeField, Min(0)] private int corridorLootCount = 10;
        [Tooltip("How much more often loot lies in a dead end than in a passage.")]
        [SerializeField, Min(1f)] private float deadEndLootWeight = 6f;

        [Header("Start and exit")]
        [Tooltip("No loot within a few tiles of these points and no barricades or holes near them (player spawn, exit).")]
        [SerializeField] private Transform[] keepClear;
        [Tooltip("Distance from the points above that stays free of barricades, in world units.")]
        [SerializeField, Min(0f)] private float barricadeClearance = 9f;
        [Tooltip("Distance from the points above that stays free of holes, in world units.")]
        [SerializeField, Min(0f)] private float holeClearance = 12f;

        [Header("Seed")]
        [Tooltip("Off: every raid gets a new random seed (printed to the console).")]
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int fixedSeed = 1;

        // Cells painted into the maze tilemaps, so a preview made in the editor can be taken back.
        [SerializeField, HideInInspector] private List<Vector3Int> paintedFloor = new List<Vector3Int>();
        [SerializeField, HideInInspector] private List<Vector3Int> paintedWalls = new List<Vector3Int>();
        [SerializeField, HideInInspector] private List<Vector3Int> paintedWindows = new List<Vector3Int>();
        // Wall tiles taken out for the holes, to be put back.
        [SerializeField, HideInInspector] private List<Vector3Int> holeCells = new List<Vector3Int>();
        [SerializeField, HideInInspector] private List<TileBase> holeTiles = new List<TileBase>();

        private readonly List<PlacedRoom> rooms = new List<PlacedRoom>();
        private readonly List<GameObject> barricades = new List<GameObject>();
        private readonly List<GameObject> holes = new List<GameObject>();

        public int Seed { get; private set; }
        public IReadOnlyList<PlacedRoom> Rooms => rooms;
        public IReadOnlyList<GameObject> Barricades => barricades;
        public IReadOnlyList<GameObject> Holes => holes;

        private int Tiles => cells * RoomSizes.CellTiles;

        private void Awake()
        {
            if (!Application.isPlaying) return;
            Generate(useFixedSeed ? fixedSeed : Random.Range(1, int.MaxValue));
        }

        /// <summary>Builds a raid. Anything left from an earlier one (also from an editor preview) is removed first.</summary>
        public void Generate(int seed)
        {
            Clear();
            Seed = seed;
            var rng = new System.Random(seed);

            var root = new GameObject(RootName);
            root.transform.SetParent(transform, false);

            var slots = GetComponentsInChildren<RoomSlot>().OrderBy(s => s.name, System.StringComparer.Ordinal).ToList();
            foreach (var slot in slots)
            {
                var placed = PlaceRoom(slot, root.transform, rng);
                if (placed != null) rooms.Add(placed);
            }
            // After the rooms: their doors decide which corridor cells are passages.
            PlaceBarricades(root.transform, slots, rng);
            ScatterCorridorLoot(root.transform, slots, rng);
            // Last: a hole opens a wall, and what is a passage or a dead end was decided with the walls whole.
            PlaceHoles(root.transform, slots, rng);

            string summary = string.Join(", ", rooms.Select(r => $"{r.Slot.name}={RoomSizes.Title(r.Kind)}"));
            Debug.Log($"Raid seed {seed}: {summary}; barricades: {barricades.Count}; holes: {holes.Count}");
        }

        /// <summary>Removes the rooms, barricades, holes, props and loot of the current raid and restores the empty template.</summary>
        public void Clear()
        {
            for (int i = 0; i < holeCells.Count; i++) wallMap.SetTile(holeCells[i], holeTiles[i]);
            holeCells.Clear();
            holeTiles.Clear();
            foreach (var cell in paintedFloor) floorMap.SetTile(cell, null);
            foreach (var cell in paintedWalls) wallMap.SetTile(cell, null);
            if (windowMap != null)
            {
                foreach (var cell in paintedWindows) windowMap.SetTile(cell, null);
            }
            paintedFloor.Clear();
            paintedWalls.Clear();
            paintedWindows.Clear();
            rooms.Clear();
            barricades.Clear();
            holes.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name != RootName) continue;
                Remove(child.gameObject);
            }
        }

        // ---- rooms ----

        private PlacedRoom PlaceRoom(RoomSlot slot, Transform root, System.Random rng)
        {
            var info = library.PickKind(slot.Size, rng);
            if (info == null)
            {
                Debug.LogWarning($"RaidGenerator: no room fits {slot.name} ({slot.Size}).", slot);
                return null;
            }
            var prefab = library.PickRoom(info.kind, slot.Size, rng);

            var origin = slot.InteriorOrigin;
            var instance = Instantiate(prefab, new Vector3(origin.x, origin.y, 0f), Quaternion.identity, root);
            instance.name = $"{slot.name} - {RoomSizes.Title(info.kind)}";

            // The room brings its ring of outer walls, so the copy also covers the tiles around the floor.
            CopyTiles(instance.Floor, floorMap, origin, paintedFloor);
            CopyTiles(instance.Walls, wallMap, origin, paintedWalls);
            CopyTiles(instance.Windows, windowMap, origin, paintedWindows);
            Remove(instance.Tiles.gameObject);

            var placed = new PlacedRoom { Slot = slot, Kind = info.kind, Prefab = prefab, Instance = instance.gameObject };
            placed.LootCount = SpawnRoomLoot(instance, info, rng);
            return placed;
        }

        private static void CopyTiles(Tilemap source, Tilemap target, Vector2Int origin, List<Vector3Int> painted)
        {
            if (source == null || target == null) return;
            source.CompressBounds();
            foreach (var cell in source.cellBounds.allPositionsWithin)
            {
                var tile = source.GetTile(cell);
                if (tile == null) continue;
                var destination = new Vector3Int(origin.x + cell.x, origin.y + cell.y, 0);
                target.SetTile(destination, tile);
                painted.Add(destination);
            }
        }

        private int SpawnRoomLoot(RoomTemplate room, RoomLibrary.KindInfo info, System.Random rng)
        {
            int spawned = 0;
            if (room.LootSpots == null) return 0;

            // Collected first: the markers are removed while the loop runs.
            var spots = room.LootSpots.Cast<Transform>().ToList();
            foreach (var spot in spots)
            {
                if (info.loot != null && rng.NextDouble() <= info.lootChance && SpawnLoot(info.loot, spot.position, room.transform, rng)) spawned++;
                Remove(spot.gameObject);
            }
            return spawned;
        }

        // ---- barricades ----

        private void PlaceBarricades(Transform root, List<RoomSlot> slots, System.Random rng)
        {
            if (barricadePrefabs == null || barricadePrefabs.Length == 0 || barricadeCount <= 0) return;

            // A barricade goes where a corridor is a plain passage: open at two opposite sides, walls at the others.
            var spots = new List<(Vector2Int cell, bool runsAlongX)>();
            for (int x = 0; x < cells; x++)
            {
                for (int y = 0; y < cells; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (InsideSlot(slots, cell)) continue;

                    bool right = IsOpen(cell, Vector2Int.right), left = IsOpen(cell, Vector2Int.left);
                    bool up = IsOpen(cell, Vector2Int.up), down = IsOpen(cell, Vector2Int.down);
                    if (left && right && !up && !down) spots.Add((cell, true));
                    else if (up && down && !left && !right) spots.Add((cell, false));
                }
            }

            var chosen = new List<Vector2Int>();
            foreach (var spot in spots.OrderBy(_ => rng.Next()))
            {
                if (chosen.Count >= barricadeCount) break;
                if (chosen.Any(c => CellDistance(c, spot.cell) < barricadeSpacing)) continue;

                Vector3 position = CellCenter(spot.cell);
                if (NearKeepClear(position, barricadeClearance)) continue;

                // The prefab lies along X: that blocks a corridor running along Y. Other corridors need a quarter turn.
                var prefab = barricadePrefabs[rng.Next(barricadePrefabs.Length)];
                if (prefab == null) continue;
                var rotation = spot.runsAlongX ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
                barricades.Add(Instantiate(prefab, position, rotation, root));
                chosen.Add(spot.cell);
            }
        }

        /// <summary>True when the wall line between a cell and its neighbour has a way through: a gap or a door.</summary>
        private bool IsOpen(Vector2Int cell, Vector2Int direction)
        {
            int tiles = Tiles, step = RoomSizes.CellTiles;
            for (int i = 1; i < step; i++)
            {
                Vector2Int tile;
                if (direction.x != 0)
                {
                    int line = direction.x > 0 ? cell.x + 1 : cell.x;
                    tile = new Vector2Int(line * step % tiles, cell.y * step + i);
                }
                else
                {
                    int line = direction.y > 0 ? cell.y + 1 : cell.y;
                    tile = new Vector2Int(cell.x * step + i, line * step % tiles);
                }

                var world = new Vector3Int(gridOrigin.x + tile.x, gridOrigin.y + tile.y, 0);
                if (!wallMap.HasTile(world) && (windowMap == null || !windowMap.HasTile(world))) return true;
            }
            return false;
        }

        private bool InsideSlot(List<RoomSlot> slots, Vector2Int cell)
        {
            // The first floor tile of the cell tells where it is.
            var world = new Vector3Int(gridOrigin.x + cell.x * RoomSizes.CellTiles + 1, gridOrigin.y + cell.y * RoomSizes.CellTiles + 1, 0);
            return slots.Any(s => s.Covers(world));
        }

        /// <summary>Middle of the floor of a cell.</summary>
        private Vector3 CellCenter(Vector2Int cell)
        {
            float middle = (RoomSizes.CellTiles + 1) * 0.5f;
            return new Vector3(gridOrigin.x + cell.x * RoomSizes.CellTiles + middle, gridOrigin.y + cell.y * RoomSizes.CellTiles + middle, 0f);
        }

        /// <summary>Cells between two cells of the wrapped level (the larger of the two axes).</summary>
        private int CellDistance(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
            return Mathf.Max(Mathf.Min(dx, cells - dx), Mathf.Min(dy, cells - dy));
        }

        private bool NearKeepClear(Vector2 position, float distance)
        {
            if (keepClear == null) return false;
            foreach (var point in keepClear)
            {
                if (point != null && WrappedDistance(point.position, position) < distance) return true;
            }
            return false;
        }

        /// <summary>Distance between two points of the wrapped level, across the seam when that is nearer.</summary>
        private float WrappedDistance(Vector2 a, Vector2 b)
        {
            float size = Tiles;
            Vector2 delta = a - b;
            delta.x -= size * Mathf.Round(delta.x / size);
            delta.y -= size * Mathf.Round(delta.y / size);
            return delta.magnitude;
        }

        // ---- holes ----

        private void PlaceHoles(Transform root, List<RoomSlot> slots, System.Random rng)
        {
            if (holePrefabs == null || holePrefabs.Length == 0 || holeCount <= 0) return;

            // Which sides of the cells can be walked through. A hole that is made counts as well, so that the next one
            // is not spent on the same shortcut.
            var openRight = new bool[cells, cells];
            var openUp = new bool[cells, cells];
            var inMaze = new bool[cells, cells];
            for (int x = 0; x < cells; x++)
            {
                for (int y = 0; y < cells; y++)
                {
                    var cell = new Vector2Int(x, y);
                    openRight[x, y] = IsOpen(cell, Vector2Int.right);
                    openUp[x, y] = IsOpen(cell, Vector2Int.up);
                    inMaze[x, y] = !InsideSlot(slots, cell);
                }
            }

            // A hole goes into a wall between two corridors; the walls of the rooms stay whole.
            var spots = new List<(Vector2Int cell, Vector2Int direction)>();
            for (int x = 0; x < cells; x++)
            {
                for (int y = 0; y < cells; y++)
                {
                    if (!inMaze[x, y]) continue;
                    if (!openRight[x, y] && inMaze[(x + 1) % cells, y]) spots.Add((new Vector2Int(x, y), Vector2Int.right));
                    if (!openUp[x, y] && inMaze[x, (y + 1) % cells]) spots.Add((new Vector2Int(x, y), Vector2Int.up));
                }
            }

            int tiles = Tiles, step = RoomSizes.CellTiles, middle = (step + 1) / 2;
            var chosen = new List<Vector2Int>();
            foreach (var spot in spots.OrderBy(_ => rng.Next()))
            {
                if (chosen.Count >= holeCount) break;
                if (chosen.Any(c => CellDistance(c, spot.cell) < holeSpacing)) continue;

                // The middle tile of the piece of wall between the two cells.
                var tile = spot.direction.x != 0
                    ? new Vector3Int(gridOrigin.x + (spot.cell.x + 1) * step % tiles, gridOrigin.y + spot.cell.y * step + middle, 0)
                    : new Vector3Int(gridOrigin.x + spot.cell.x * step + middle, gridOrigin.y + (spot.cell.y + 1) * step % tiles, 0);
                var wall = wallMap.GetTile(tile);
                if (wall == null) continue;

                var position = new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f);
                if (NearKeepClear(position, holeClearance)) continue;
                // A barricade in one of the two cells would stand right where the player comes out.
                if (barricades.Any(b => WrappedDistance(b.transform.position, position) < step)) continue;

                var other = new Vector2Int((spot.cell.x + spot.direction.x) % cells, (spot.cell.y + spot.direction.y) % cells);
                if (WalkIsShorter(spot.cell, other, holeMinDetour, openRight, openUp)) continue;

                var prefab = holePrefabs[rng.Next(holePrefabs.Length)];
                if (prefab == null) continue;

                wallMap.SetTile(tile, null);
                holeCells.Add(tile);
                holeTiles.Add(wall);
                if (!floorMap.HasTile(tile))
                {
                    // The floor of the corridor behind the wall goes on under the hole.
                    floorMap.SetTile(tile, floorMap.GetTile(tile + new Vector3Int(spot.direction.x, spot.direction.y, 0)));
                    paintedFloor.Add(tile);
                }

                // The prefab lies along X like a wall between a cell and the one above it. Other walls need a quarter turn.
                var rotation = spot.direction.x != 0 ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
                holes.Add(Instantiate(prefab, position, rotation, root));
                chosen.Add(spot.cell);
                if (spot.direction.x != 0) openRight[spot.cell.x, spot.cell.y] = true;
                else openUp[spot.cell.x, spot.cell.y] = true;
            }
        }

        /// <summary>True when one cell can be reached from the other in fewer than <paramref name="limit"/> cells of walking.</summary>
        private bool WalkIsShorter(Vector2Int from, Vector2Int to, int limit, bool[,] openRight, bool[,] openUp)
        {
            var seen = new HashSet<Vector2Int> { from };
            var queue = new Queue<(Vector2Int cell, int walked)>();
            queue.Enqueue((from, 0));
            while (queue.Count > 0)
            {
                var (cell, walked) = queue.Dequeue();
                if (cell == to) return true;
                if (walked + 1 >= limit) continue;

                foreach (var direction in Directions)
                {
                    var next = new Vector2Int((cell.x + direction.x + cells) % cells, (cell.y + direction.y + cells) % cells);
                    // The side between two cells belongs to the left or the lower one of them.
                    bool open = direction.x > 0 ? openRight[cell.x, cell.y]
                        : direction.x < 0 ? openRight[next.x, next.y]
                        : direction.y > 0 ? openUp[cell.x, cell.y]
                        : openUp[next.x, next.y];
                    if (!open || !seen.Add(next)) continue;
                    queue.Enqueue((next, walked + 1));
                }
            }
            return false;
        }

        // ---- loot ----

        private void ScatterCorridorLoot(Transform root, List<RoomSlot> slots, System.Random rng)
        {
            if (corridorLoot == null || corridorLootCount <= 0) return;

            // A dead end is worth a look: loot lies there far more often than in a passage.
            var candidates = new List<Vector3Int>();
            var weights = new List<float>();
            float total = 0f;
            foreach (var cell in floorMap.cellBounds.allPositionsWithin)
            {
                if (!floorMap.HasTile(cell) || wallMap.HasTile(cell)) continue;
                if (windowMap != null && windowMap.HasTile(cell)) continue;
                if (slots.Any(s => s.Covers(cell))) continue;
                var center = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                if (NearKeepClear(center, 3f)) continue;
                if (barricades.Any(b => Vector2.Distance(b.transform.position, center) < 1.6f)) continue;

                float weight = IsDeadEnd(cell) ? deadEndLootWeight : 1f;
                candidates.Add(cell);
                weights.Add(weight);
                total += weight;
            }

            for (int i = 0; i < corridorLootCount && candidates.Count > 0; i++)
            {
                double pick = rng.NextDouble() * total;
                int index = 0;
                while (index < candidates.Count - 1 && pick >= weights[index]) pick -= weights[index++];

                var cell = candidates[index];
                total -= weights[index];
                candidates.RemoveAt(index);
                weights.RemoveAt(index);
                SpawnLoot(corridorLoot, new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f), root, rng);
            }
        }

        /// <summary>True for a floor tile of a corridor cell that has a way out through one side only.</summary>
        private bool IsDeadEnd(Vector3Int tile)
        {
            int tx = tile.x - gridOrigin.x, ty = tile.y - gridOrigin.y;
            int size = Tiles;
            if (tx < 0 || ty < 0 || tx >= size || ty >= size) return false;
            // Tiles of the wall lines belong to the passage between two cells, not to a cell.
            if (tx % RoomSizes.CellTiles == 0 || ty % RoomSizes.CellTiles == 0) return false;

            var cell = new Vector2Int(tx / RoomSizes.CellTiles, ty / RoomSizes.CellTiles);
            int open = 0;
            foreach (var direction in Directions)
            {
                if (IsOpen(cell, direction)) open++;
            }
            return open == 1;
        }

        private static bool SpawnLoot(LootTable table, Vector3 position, Transform parent, System.Random rng)
        {
            var entry = table.Roll(rng);
            if (entry == null || entry.item == null || entry.item.WorldPrefab == null) return false;

            var instance = Instantiate(entry.item.WorldPrefab, position, Quaternion.identity, parent);
            if (instance.TryGetComponent<ItemPickup>(out var pickup)) pickup.Set(entry.item, table.RollCount(entry, rng));
            return true;
        }

        // Destroy is not allowed in edit mode (preview), DestroyImmediate is not wanted while playing.
        private static void Remove(GameObject target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        [ContextMenu("Preview raid (random seed)")]
        private void PreviewRandom() => Generate(Random.Range(1, 100000));

        [ContextMenu("Clear preview")]
        private void ClearPreview() => Clear();
    }
}
