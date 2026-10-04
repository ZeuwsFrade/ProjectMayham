using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = System.Random;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Builds the corridor template of the open scene: a 39x39 grid of cells (a wall line and two tiles of floor each)
    /// with room slots of three sizes, corridors between them and doors from every slot into the corridors.
    /// The template is the same every time (fixed seed); what changes from raid to raid are the rooms in the slots.
    /// </summary>
    public static class TemplateBuilder
    {
        private const int Cells = 39;                      // cells per side of the level
        private const int Tiles = Cells * RoomSizes.CellTiles; // 117 tiles per side
        private const int Half = (Tiles + 1) / 2;          // tile index -> world cell: index - Half (spawn lands on 0, 0)
        private const int Spawn = Cells / 2;               // the centre cell stays a corridor
        private const int Regions = 3;                     // rooms are spread over a 3x3 grid of regions, one each
        private const int RegionMargin = 2;                // cells between a room and the border of its region
        private const int SpawnMargin = 2;                 // cells kept free around the spawn cell

        private class Slot
        {
            public RoomSize Size;
            public Vector2Int Cell;   // bottom-left cell
            public Vector2Int Span;   // cells wide and high
            public List<Vector2Int> DoorTiles = new List<Vector2Int>();
            public int Index;
        }

        private static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        [MenuItem("Mayham/Level/Rebuild Corridor Template")]
        public static void BuildMenu()
        {
            Debug.Log(Build(4242));
        }

        [MenuItem("Mayham/Level/Preview Raid (random seed)")]
        public static void PreviewMenu()
        {
            var generator = UnityEngine.Object.FindFirstObjectByType<RaidGenerator>();
            if (generator == null) { Debug.LogWarning("No RaidGenerator in the open scene."); return; }
            generator.Generate(UnityEngine.Random.Range(1, 100000));
        }

        [MenuItem("Mayham/Level/Clear Raid Preview")]
        public static void ClearPreviewMenu()
        {
            var generator = UnityEngine.Object.FindFirstObjectByType<RaidGenerator>();
            if (generator != null) generator.Clear();
        }

        public static string Build(int seed)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var maze = roots.FirstOrDefault(g => g.name == "Maze");
            if (maze == null) return "No 'Maze' object in the open scene.";
            var floorMap = maze.transform.Find("Floor").GetComponent<Tilemap>();
            var wallMap = maze.transform.Find("Walls").GetComponent<Tilemap>();

            var floorTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileSet/Rule Tiles/Floors/Floor-Stone_01.asset");
            var wallTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileSet/Rule Tiles/Walls/Wall-Concrete_01.asset");

            // Anything left from an earlier build or a preview goes first.
            var oldGenerator = maze.GetComponent<RaidGenerator>();
            if (oldGenerator != null) oldGenerator.Clear();

            // ---- slots ----
            var slots = PlaceSlots(seed, out var corridor);
            if (slots == null) return "Could not place the room slots; try another seed.";

            // ---- corridors ----
            var rng = new Random(seed + 1);
            var opened = new HashSet<Vector2Int>();            // wall-line tiles that are open (tile indices)
            var linked = new Dictionary<Vector2Int, List<Vector2Int>>(); // corridor cell -> connected corridor cells
            CarveCorridors(rng, corridor, slots, opened, linked);

            // ---- tiles ----
            floorMap.ClearAllTiles();
            wallMap.ClearAllTiles();
            for (int tx = 0; tx < Tiles; tx++)
            {
                for (int ty = 0; ty < Tiles; ty++)
                {
                    if (InsideSlot(slots, tx, ty)) continue;
                    var cell = new Vector3Int(tx - Half, ty - Half, 0);
                    bool onWallLine = tx % 3 == 0 || ty % 3 == 0;
                    if (onWallLine && !opened.Contains(new Vector2Int(tx, ty))) wallMap.SetTile(cell, wallTile);
                    else floorMap.SetTile(cell, floorTile);
                }
            }

            // ---- slot objects ----
            var slotsRoot = maze.transform.Find("Slots");
            if (slotsRoot != null) UnityEngine.Object.DestroyImmediate(slotsRoot.gameObject);
            slotsRoot = new GameObject("Slots").transform;
            slotsRoot.SetParent(maze.transform, false);
            foreach (var slot in slots)
            {
                var go = new GameObject($"Slot_{slot.Index:00}_{slot.Size}");
                go.transform.SetParent(slotsRoot, false);
                var component = go.AddComponent<RoomSlot>();
                var origin = new Vector2Int(slot.Cell.x * 3 + 1 - Half, slot.Cell.y * 3 + 1 - Half);
                component.Configure(slot.Size, origin, slot.DoorTiles.Select(t => new Vector2Int(t.x - Half, t.y - Half)));
            }

            // ---- spawn, exit, enemies ----
            var distance = CellDistances(linked, new Vector2Int(Spawn, Spawn));
            var spawnCenter = CellCenter(new Vector2Int(Spawn, Spawn));
            var spawnPoint = maze.transform.Find("PlayerSpawn");
            if (spawnPoint != null) spawnPoint.position = spawnCenter;

            var far = distance.OrderByDescending(p => p.Value).ThenBy(p => p.Key.x).ThenBy(p => p.Key.y).Select(p => p.Key).ToList();
            var exit = maze.transform.Find("ExitZone");
            if (exit != null && far.Count > 0) exit.position = CellCenter(far[0]);

            var enemies = roots.FirstOrDefault(g => g.name == "Enemies");
            if (enemies != null)
            {
                // Spread the test enemies over corridor cells that are a good walk away from the start.
                var candidates = distance.Where(p => p.Value >= 12 && p.Key != far[0]).Select(p => p.Key).OrderBy(_ => rng.Next()).ToList();
                var taken = new List<Vector2Int>();
                foreach (Transform enemy in enemies.transform)
                {
                    var pick = candidates.FirstOrDefault(c => taken.All(t => Mathf.Abs(t.x - c.x) + Mathf.Abs(t.y - c.y) >= 10));
                    if (pick == default) pick = candidates.FirstOrDefault();
                    taken.Add(pick);
                    enemy.position = CellCenter(pick);
                }
            }

            // The baked pickups of the old maze give way to the loot of the raid.
            var oldInteractables = roots.FirstOrDefault(g => g.name == "Interactables");
            int removed = oldInteractables != null ? oldInteractables.transform.childCount : 0;
            if (oldInteractables != null) UnityEngine.Object.DestroyImmediate(oldInteractables);

            // ---- generator ----
            var generator = maze.GetComponent<RaidGenerator>() ?? maze.AddComponent<RaidGenerator>();
            var gso = new SerializedObject(generator);
            gso.FindProperty("library").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RoomLibrary>(RoomFactory.LibraryPath);
            gso.FindProperty("floorMap").objectReferenceValue = floorMap;
            gso.FindProperty("wallMap").objectReferenceValue = wallMap;
            gso.FindProperty("corridorLoot").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LootTable>($"{RoomFactory.LootFolder}/Loot_Corridor.asset");
            gso.FindProperty("corridorLootCount").intValue = 30;
            var keep = gso.FindProperty("keepClear");
            keep.arraySize = 2;
            keep.GetArrayElementAtIndex(0).objectReferenceValue = spawnPoint;
            keep.GetArrayElementAtIndex(1).objectReferenceValue = exit;
            gso.ApplyModifiedPropertiesWithoutUndo();

            // The level wraps around: the domain is the whole template.
            var wrapType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ProjectMayham.World.WrapWorld")).First(t => t != null);
            var wrap = maze.GetComponent(wrapType);
            if (wrap != null)
            {
                var wso = new SerializedObject(wrap);
                wso.FindProperty("center").vector2Value = new Vector2((-Half + Tiles - Half) * 0.5f, (-Half + Tiles - Half) * 0.5f);
                wso.FindProperty("size").vector2Value = new Vector2(Tiles, Tiles);
                wso.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(maze);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            return $"Template built: {slots.Count} slots ({string.Join(", ", slots.GroupBy(s => s.Size).Select(g => g.Count() + " " + g.Key))}), " +
                   $"{corridor.Count} corridor cells, {slots.Sum(s => s.DoorTiles.Count) / 2} doors, exit at {CellCenter(far[0])}, removed {removed} old pickups.";
        }

        // ---- slot placement ----

        private static List<Slot> PlaceSlots(int seed, out HashSet<Vector2Int> corridor)
        {
            var wanted = new[] { RoomSize.Large, RoomSize.Large, RoomSize.Medium, RoomSize.Medium, RoomSize.Medium, RoomSize.Small, RoomSize.Small, RoomSize.Small, RoomSize.Small };
            int region = Cells / Regions;

            for (int attempt = 0; attempt < 2000; attempt++)
            {
                var rng = new Random(seed + attempt * 7919);
                // Every region of the level gets one room, so the rooms are spread out and far apart.
                var sizes = wanted.OrderBy(_ => rng.Next()).ToList();
                var slots = new List<Slot>();
                bool ok = true;
                for (int r = 0; r < Regions * Regions && ok; r++)
                {
                    var span = RoomSizes.Cells(sizes[r]);
                    int x0 = (r % Regions) * region, y0 = (r / Regions) * region;
                    int minX = Mathf.Max(1, x0 + RegionMargin), maxX = Mathf.Min(Cells - 2 - span.x + 1, x0 + region - RegionMargin - span.x);
                    int minY = Mathf.Max(1, y0 + RegionMargin), maxY = Mathf.Min(Cells - 2 - span.y + 1, y0 + region - RegionMargin - span.y);

                    bool placed = false;
                    for (int tries = 0; tries < 40 && !placed; tries++)
                    {
                        var candidate = new Slot { Size = sizes[r], Span = span, Cell = new Vector2Int(rng.Next(minX, maxX + 1), rng.Next(minY, maxY + 1)) };
                        if (NearSpawn(candidate)) continue;
                        slots.Add(candidate);
                        placed = true;
                    }
                    ok = placed;
                }
                if (!ok) continue;

                corridor = new HashSet<Vector2Int>();
                for (int x = 0; x < Cells; x++)
                    for (int y = 0; y < Cells; y++)
                        if (!slots.Any(s => Contains(s, new Vector2Int(x, y)))) corridor.Add(new Vector2Int(x, y));
                if (!Connected(corridor)) continue;

                for (int i = 0; i < slots.Count; i++) slots[i].Index = i + 1;
                return slots;
            }
            corridor = null;
            return null;
        }

        private static bool NearSpawn(Slot slot) =>
            Spawn >= slot.Cell.x - SpawnMargin && Spawn < slot.Cell.x + slot.Span.x + SpawnMargin &&
            Spawn >= slot.Cell.y - SpawnMargin && Spawn < slot.Cell.y + slot.Span.y + SpawnMargin;

        private static bool Contains(Slot slot, Vector2Int cell) =>
            cell.x >= slot.Cell.x && cell.x < slot.Cell.x + slot.Span.x && cell.y >= slot.Cell.y && cell.y < slot.Cell.y + slot.Span.y;

        private static bool InsideSlot(List<Slot> slots, int tx, int ty)
        {
            foreach (var s in slots)
            {
                int x0 = s.Cell.x * 3 + 1, x1 = (s.Cell.x + s.Span.x) * 3 - 1;
                int y0 = s.Cell.y * 3 + 1, y1 = (s.Cell.y + s.Span.y) * 3 - 1;
                if (tx >= x0 && tx <= x1 && ty >= y0 && ty <= y1) return true;
            }
            return false;
        }

        private static Vector2Int Wrap(Vector2Int cell) => new Vector2Int((cell.x % Cells + Cells) % Cells, (cell.y % Cells + Cells) % Cells);

        private static bool Connected(HashSet<Vector2Int> cells)
        {
            var seen = new HashSet<Vector2Int> { new Vector2Int(Spawn, Spawn) };
            var queue = new Queue<Vector2Int>(seen);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in Dirs)
                {
                    var n = Wrap(c + d);
                    if (cells.Contains(n) && seen.Add(n)) queue.Enqueue(n);
                }
            }
            return seen.Count == cells.Count;
        }

        // ---- corridors and doors ----

        // The two tiles of the wall line between a cell and its neighbour in direction d.
        private static Vector2Int[] SegmentBetween(Vector2Int cell, Vector2Int d)
        {
            if (d.x != 0)
            {
                int line = d.x > 0 ? Wrap(cell + d).x : cell.x;
                return new[] { new Vector2Int(line * 3, cell.y * 3 + 1), new Vector2Int(line * 3, cell.y * 3 + 2) };
            }
            int row = d.y > 0 ? Wrap(cell + d).y : cell.y;
            return new[] { new Vector2Int(cell.x * 3 + 1, row * 3), new Vector2Int(cell.x * 3 + 2, row * 3) };
        }

        private static void CarveCorridors(Random rng, HashSet<Vector2Int> corridor, List<Slot> slots,
            HashSet<Vector2Int> opened, Dictionary<Vector2Int, List<Vector2Int>> linked)
        {
            foreach (var c in corridor) linked[c] = new List<Vector2Int>();

            Action<Vector2Int, Vector2Int> open = (a, b) =>
            {
                var d = DirectionBetween(a, b);
                foreach (var t in SegmentBetween(a, d)) opened.Add(t);
                if (!linked[a].Contains(b)) linked[a].Add(b);
                if (!linked[b].Contains(a)) linked[b].Add(a);
            };

            // A random spanning tree over the corridor cells, then loops so that nothing has to be walked twice.
            var start = new Vector2Int(Spawn, Spawn);
            var visited = new HashSet<Vector2Int> { start };
            var stack = new Stack<Vector2Int>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var c = stack.Peek();
                var options = Dirs.Select(d => Wrap(c + d)).Where(n => corridor.Contains(n) && !visited.Contains(n)).OrderBy(_ => rng.Next()).ToList();
                if (options.Count == 0) { stack.Pop(); continue; }
                var next = options[0];
                open(c, next);
                visited.Add(next);
                stack.Push(next);
            }

            foreach (var c in corridor.OrderBy(c => c.x * 100 + c.y))
            {
                foreach (var d in Dirs)
                {
                    var n = Wrap(c + d);
                    if (!corridor.Contains(n) || linked[c].Contains(n)) continue;
                    if (rng.NextDouble() < 0.30) open(c, n);
                }
            }
            foreach (var c in corridor.OrderBy(c => c.x * 100 + c.y))
            {
                if (linked[c].Count != 1 || rng.NextDouble() > 0.7) continue;
                var closed = Dirs.Select(d => Wrap(c + d)).Where(n => corridor.Contains(n) && !linked[c].Contains(n)).ToList();
                if (closed.Count > 0) open(c, closed[rng.Next(closed.Count)]);
            }

            // Two doors into every room, on different sides when possible.
            foreach (var slot in slots)
            {
                var segments = new List<(Vector2Int inside, Vector2Int d)>();
                for (int x = 0; x < slot.Span.x; x++)
                {
                    segments.Add((slot.Cell + new Vector2Int(x, 0), Vector2Int.down));
                    segments.Add((slot.Cell + new Vector2Int(x, slot.Span.y - 1), Vector2Int.up));
                }
                for (int y = 0; y < slot.Span.y; y++)
                {
                    segments.Add((slot.Cell + new Vector2Int(0, y), Vector2Int.left));
                    segments.Add((slot.Cell + new Vector2Int(slot.Span.x - 1, y), Vector2Int.right));
                }
                segments = segments.Where(s => corridor.Contains(Wrap(s.inside + s.d))).OrderBy(_ => rng.Next()).ToList();

                var chosen = new List<(Vector2Int inside, Vector2Int d)>();
                foreach (var s in segments)
                {
                    if (chosen.Count == 2) break;
                    if (chosen.Any(c => c.d == s.d) && segments.Any(o => o.d != s.d && !chosen.Contains(o))) continue;
                    chosen.Add(s);
                }
                foreach (var s in chosen)
                {
                    foreach (var tile in SegmentBetween(s.inside, s.d))
                    {
                        opened.Add(tile);
                        slot.DoorTiles.Add(tile);
                    }
                }
            }
        }

        private static Vector2Int DirectionBetween(Vector2Int a, Vector2Int b)
        {
            foreach (var d in Dirs)
            {
                if (Wrap(a + d) == b) return d;
            }
            throw new InvalidOperationException($"{a} and {b} are not neighbours");
        }

        private static Dictionary<Vector2Int, int> CellDistances(Dictionary<Vector2Int, List<Vector2Int>> linked, Vector2Int from)
        {
            var dist = new Dictionary<Vector2Int, int> { { from, 0 } };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var n in linked[c])
                {
                    if (dist.ContainsKey(n)) continue;
                    dist[n] = dist[c] + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }

        private static Vector3 CellCenter(Vector2Int cell) => new Vector3(cell.x * 3 + 2 - Half, cell.y * 3 + 2 - Half, 0f);
    }
}
