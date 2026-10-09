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
    /// Builds the corridor template of the open scene: a classic maze on a 51x51 grid of cells (a wall line and three
    /// tiles of floor each, so the corridors are three tiles wide) with nine room slots put into it, one in every
    /// region of a 3x3 grid.
    ///
    /// The maze fills everything between the rooms. It is grown as one winding tree, so a passage is always a
    /// corridor: one cell wide, a wall on both sides, no open squares and no wall posts standing on their own. A few
    /// extra openings give a second way around here and there; they are only made where the walls on both sides
    /// stay part of something big, so no post and no short stump of wall is left standing alone.
    /// The rooms are kept apart by a margin of maze, and no room lies across the seam of the wrapped level.
    ///
    /// The template is the same every time (fixed seed); what changes from raid to raid are the rooms in the slots.
    /// The slots are left empty, outer walls included: a room brings its own walls, doors and windows.
    /// </summary>
    public static class TemplateBuilder
    {
        private const int Regions = 3;                         // rooms are spread over a 3x3 grid of regions, one each
        private const int T = RoomSizes.CellTiles;             // tiles per cell: a wall line and the floor
        private const int Region = 17;                         // cells per region
        private const int Cells = Regions * Region;            // 51 cells per side of the level
        private const int Tiles = Cells * T;                   // 204 tiles per side
        private const int RoomMargin = 3;                      // cells of maze between a room and the border of its region
        private const int Spawn = Region;                      // the spawn cell is a corner of four regions, far from every room
        private const int Origin = Spawn * T + (T + 1) / 2;    // tile index -> world cell: index - Origin (the spawn cell lies around 0, 0)
        private const double StraightBias = 0.55;              // how often a passage goes on straight when it can: longer corridors
        private const double LoopChance = 0.03;                // share of the walls that are tried for a loop
        private const int MinWallPiece = 12;                   // posts a piece of wall must keep after a loop cuts it off
        private const int HoleCount = 12;                      // holes broken through the walls of the maze in a raid
        private const int HoleSpacing = 6;                     // cells between two holes
        private const int HoleMinDetour = 12;                  // cells a hole has to save, or it is not worth making
        private const float HoleClearance = 12f;               // world units around the start and the exit without holes

        public const string WindowLayer = "Window";
        public const string BarricadesFolder = "Assets/Prefabs/Barricades";
        public const string HolesFolder = "Assets/Prefabs/Holes";

        private class Slot
        {
            public RoomSize Size;
            public Vector2Int Cell;   // bottom-left cell
            public Vector2Int Span;   // cells wide and high
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
            var windowMap = EnsureWindowMap(maze.transform);

            var floorTile = AssetDatabase.LoadAssetAtPath<TileBase>(RoomFactory.CorridorFloorTile);
            var wallTile = AssetDatabase.LoadAssetAtPath<TileBase>(RoomFactory.WallTile);

            // Anything left from an earlier build or a preview goes first.
            var oldGenerator = maze.GetComponent<RaidGenerator>();
            if (oldGenerator != null) oldGenerator.Clear();

            // ---- rooms, then the maze around them ----
            var rng = new Random(seed);
            var slots = PlaceSlots(rng);
            var corridor = new HashSet<Vector2Int>();
            for (int x = 0; x < Cells; x++)
                for (int y = 0; y < Cells; y++)
                    if (!slots.Any(s => Contains(s, new Vector2Int(x, y)))) corridor.Add(new Vector2Int(x, y));

            var opened = new HashSet<Vector2Int>();            // wall-line tiles that are open (tile indices)
            var linked = new Dictionary<Vector2Int, List<Vector2Int>>(); // corridor cell -> cells it is open to
            int loops = CarveMaze(rng, corridor, opened, linked);

            // ---- tiles ----
            floorMap.ClearAllTiles();
            wallMap.ClearAllTiles();
            windowMap.ClearAllTiles();
            var floorCells = new List<Vector3Int>();
            var wallCells = new List<Vector3Int>();
            for (int tx = 0; tx < Tiles; tx++)
            {
                for (int ty = 0; ty < Tiles; ty++)
                {
                    if (InsideSlot(slots, tx, ty)) continue;
                    var cell = new Vector3Int(tx - Origin, ty - Origin, 0);
                    bool onWallLine = tx % T == 0 || ty % T == 0;
                    if (onWallLine && !opened.Contains(new Vector2Int(tx, ty))) wallCells.Add(cell);
                    else floorCells.Add(cell);
                }
            }
            floorMap.SetTiles(floorCells.ToArray(), Enumerable.Repeat(floorTile, floorCells.Count).ToArray());
            wallMap.SetTiles(wallCells.ToArray(), Enumerable.Repeat(wallTile, wallCells.Count).ToArray());

            int roomTiles = slots.Sum(s => RoomSizes.Interior(s.Size).x * RoomSizes.Interior(s.Size).y);

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
                component.Configure(slot.Size, new Vector2Int(slot.Cell.x * T + 1 - Origin, slot.Cell.y * T + 1 - Origin));
            }

            // ---- spawn, exit, enemies ----
            var spawn = new Vector2Int(Spawn, Spawn);
            var spawnPoint = maze.transform.Find("PlayerSpawn");
            if (spawnPoint != null) spawnPoint.position = CellCenter(spawn);
            // The player and the camera start in the middle of the spawn cell.
            var player = roots.FirstOrDefault(g => g.name == "Player");
            if (player != null) player.transform.position = CellCenter(spawn);
            var camera = roots.Select(g => g.GetComponent<Camera>()).FirstOrDefault(c => c != null);
            if (camera != null) camera.transform.position = new Vector3(CellCenter(spawn).x, CellCenter(spawn).y, camera.transform.position.z);

            // The exit is as far away as it gets when walking the bird's way, not along the maze: the path through the
            // maze alone says little, the rooms are shortcuts. A dead end, so that it has to be looked for.
            var deadEnds = linked.Where(p => p.Value.Count == 1).Select(p => p.Key).ToList();
            var exitCell = deadEnds.OrderByDescending(c => TorusDistance(c, spawn)).ThenBy(c => c.x).ThenBy(c => c.y).First();
            var exit = maze.transform.Find("ExitZone");
            if (exit != null) exit.position = CellCenter(exitCell);

            var enemies = roots.FirstOrDefault(g => g.name == "Enemies");
            if (enemies != null)
            {
                // Spread the test enemies over the maze, a good walk away from the start and from each other.
                var candidates = corridor.Where(c => TorusDistance(c, spawn) >= 12 && c != exitCell).OrderBy(c => c.x * 1000 + c.y).OrderBy(_ => rng.Next()).ToList();
                var taken = new List<Vector2Int>();
                foreach (Transform enemy in enemies.transform)
                {
                    var pick = candidates.FirstOrDefault(c => taken.All(t => TorusDistance(t, c) >= 14));
                    if (pick == default) pick = candidates.FirstOrDefault();
                    taken.Add(pick);
                    enemy.position = CellCenter(pick);
                }
            }

            // ---- generator ----
            var generator = maze.GetComponent<RaidGenerator>() ?? maze.AddComponent<RaidGenerator>();
            var gso = new SerializedObject(generator);
            gso.FindProperty("library").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RoomLibrary>(RoomFactory.LibraryPath);
            gso.FindProperty("floorMap").objectReferenceValue = floorMap;
            gso.FindProperty("wallMap").objectReferenceValue = wallMap;
            gso.FindProperty("windowMap").objectReferenceValue = windowMap;
            gso.FindProperty("gridOrigin").vector2IntValue = new Vector2Int(-Origin, -Origin);
            gso.FindProperty("cells").intValue = Cells;
            gso.FindProperty("corridorLoot").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LootTable>($"{RoomFactory.LootFolder}/Loot_Corridor.asset");
            gso.FindProperty("corridorLootCount").intValue = 60;
            gso.FindProperty("barricadeCount").intValue = 18;
            // The start is the end of a corridor with one way out: nothing blocks it for the first couple of screens.
            gso.FindProperty("barricadeClearance").floatValue = 20f;
            var keep = gso.FindProperty("keepClear");
            keep.arraySize = 2;
            keep.GetArrayElementAtIndex(0).objectReferenceValue = spawnPoint;
            keep.GetArrayElementAtIndex(1).objectReferenceValue = exit;

            var barricadePrefabs = AssetDatabase.FindAssets("t:GameObject", new[] { BarricadesFolder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(g => g != null && g.GetComponent<Barricade>() != null).ToList();
            var barricades = gso.FindProperty("barricadePrefabs");
            barricades.arraySize = barricadePrefabs.Count;
            for (int i = 0; i < barricadePrefabs.Count; i++) barricades.GetArrayElementAtIndex(i).objectReferenceValue = barricadePrefabs[i];

            // A few shortcuts through the walls of the maze, none of them next to the start or the exit.
            var holePrefabs = AssetDatabase.FindAssets("t:GameObject", new[] { HolesFolder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(g => g != null && g.GetComponent<WallHole>() != null).ToList();
            var holes = gso.FindProperty("holePrefabs");
            holes.arraySize = holePrefabs.Count;
            for (int i = 0; i < holePrefabs.Count; i++) holes.GetArrayElementAtIndex(i).objectReferenceValue = holePrefabs[i];
            gso.FindProperty("holeCount").intValue = HoleCount;
            gso.FindProperty("holeSpacing").intValue = HoleSpacing;
            gso.FindProperty("holeMinDetour").intValue = HoleMinDetour;
            gso.FindProperty("holeClearance").floatValue = HoleClearance;
            gso.ApplyModifiedPropertiesWithoutUndo();

            // The level wraps around: the domain is the whole template, and the windows are copied across the seam too.
            var wrapType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ProjectMayham.World.WrapWorld")).First(t => t != null);
            var wrap = maze.GetComponent(wrapType);
            if (wrap != null)
            {
                var wso = new SerializedObject(wrap);
                wso.FindProperty("center").vector2Value = new Vector2(Tiles * 0.5f - Origin, Tiles * 0.5f - Origin);
                wso.FindProperty("size").vector2Value = new Vector2(Tiles, Tiles);
                var maps = wso.FindProperty("tilemaps");
                var wrapped = new[] { floorMap, wallMap, windowMap };
                maps.arraySize = wrapped.Length;
                for (int i = 0; i < wrapped.Length; i++) maps.GetArrayElementAtIndex(i).objectReferenceValue = wrapped[i];
                wso.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(maze);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            int corridorFloor = floorCells.Count;
            return $"Template built: {Tiles}x{Tiles} tiles, {slots.Count} slots ({string.Join(", ", slots.GroupBy(s => s.Size).Select(g => g.Count() + " " + g.Key))}). " +
                   $"Maze of {corridor.Count} cells with {loops} loops and {deadEnds.Count} dead ends. " +
                   $"Rooms {roomTiles} tiles, maze floor {corridorFloor} tiles: rooms are {100f * roomTiles / (roomTiles + corridorFloor):0}% of the floor and {100f * roomTiles / (Tiles * Tiles):0}% of the map. " +
                   $"{barricadePrefabs.Count} barricade prefabs, {holePrefabs.Count} hole prefabs; exit at {CellCenter(exitCell)}.";
        }

        // ---- scene objects ----

        /// <summary>The tilemap of the windows: a collider like the walls have, but on a layer that does not stop sight.</summary>
        private static Tilemap EnsureWindowMap(Transform maze)
        {
            int layer = EnsureLayer(WindowLayer);
            var existing = maze.Find("Windows");
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject("Windows", typeof(Tilemap), typeof(TilemapRenderer), typeof(Rigidbody2D), typeof(TilemapCollider2D), typeof(CompositeCollider2D));
                go.transform.SetParent(maze, false);
                go.transform.SetSiblingIndex(maze.Find("Walls").GetSiblingIndex() + 1);
            }

            go.layer = layer;
            go.GetComponent<TilemapRenderer>().sortingOrder = 1;
            go.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            go.GetComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
            go.GetComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Polygons;
            var map = go.GetComponent<Tilemap>();
            map.color = RoomFactory.WindowTint;
            return map;
        }

        private static int EnsureLayer(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer >= 0) return layer;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var entry = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(entry.stringValue)) continue;
                entry.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return i;
            }
            throw new InvalidOperationException($"No free layer for '{name}'.");
        }

        // ---- rooms ----

        private static List<Slot> PlaceSlots(Random rng)
        {
            var wanted = new[] { RoomSize.Large, RoomSize.Large, RoomSize.Large, RoomSize.Medium, RoomSize.Medium, RoomSize.Medium, RoomSize.Small, RoomSize.Small, RoomSize.Small };
            var sizes = wanted.OrderBy(_ => rng.Next()).ToList();
            var slots = new List<Slot>();
            for (int r = 0; r < Regions * Regions; r++)
            {
                // The room sits anywhere in its region that keeps the margin: neighbours are at least two margins of
                // maze apart, and no room lies across the seam of the wrapped level.
                var span = RoomSizes.Cells(sizes[r]);
                int slackX = Region - 2 * RoomMargin - span.x, slackY = Region - 2 * RoomMargin - span.y;
                if (slackX < 0 || slackY < 0) throw new InvalidOperationException($"A {sizes[r]} room does not fit into a region.");
                int x0 = (r % Regions) * Region + RoomMargin, y0 = (r / Regions) * Region + RoomMargin;
                slots.Add(new Slot { Size = sizes[r], Span = span, Index = r + 1, Cell = new Vector2Int(x0 + rng.Next(0, slackX + 1), y0 + rng.Next(0, slackY + 1)) });
            }
            return slots;
        }

        private static bool Contains(Slot slot, Vector2Int cell) =>
            cell.x >= slot.Cell.x && cell.x < slot.Cell.x + slot.Span.x && cell.y >= slot.Cell.y && cell.y < slot.Cell.y + slot.Span.y;

        /// <summary>True for the tiles of a slot: its floor and the ring of outer walls around it.</summary>
        private static bool InsideSlot(List<Slot> slots, int tx, int ty)
        {
            foreach (var s in slots)
            {
                int x0 = s.Cell.x * T, x1 = (s.Cell.x + s.Span.x) * T;
                int y0 = s.Cell.y * T, y1 = (s.Cell.y + s.Span.y) * T;
                if (tx >= x0 && tx <= x1 && ty >= y0 && ty <= y1) return true;
            }
            return false;
        }

        // ---- the maze ----

        private static Vector2Int Wrap(Vector2Int cell) => new Vector2Int((cell.x % Cells + Cells) % Cells, (cell.y % Cells + Cells) % Cells);

        // The tiles of the wall line between a cell and its neighbour in direction d: as many as a corridor is wide.
        private static IEnumerable<Vector2Int> SegmentBetween(Vector2Int cell, Vector2Int d)
        {
            for (int i = 1; i < T; i++)
            {
                if (d.x != 0) yield return new Vector2Int((d.x > 0 ? Wrap(cell + d).x : cell.x) * T, cell.y * T + i);
                else yield return new Vector2Int(cell.x * T + i, (d.y > 0 ? Wrap(cell + d).y : cell.y) * T);
            }
        }

        /// <summary>
        /// Grows the maze over every cell that is not a room and returns the number of loops added afterwards.
        /// One passage is dug as far as it goes, then the digging goes back to the last cell that still has a
        /// neighbour to go to: the result is a tree of long winding corridors with dead ends.
        /// </summary>
        private static int CarveMaze(Random rng, HashSet<Vector2Int> corridor, HashSet<Vector2Int> opened, Dictionary<Vector2Int, List<Vector2Int>> linked)
        {
            foreach (var c in corridor) linked[c] = new List<Vector2Int>();

            Action<Vector2Int, Vector2Int> open = (a, b) =>
            {
                var d = DirectionBetween(a, b);
                foreach (var t in SegmentBetween(a, d)) opened.Add(t);
                linked[a].Add(b);
                linked[b].Add(a);
            };

            var start = new Vector2Int(Spawn, Spawn);
            var visited = new HashSet<Vector2Int> { start };
            var stack = new Stack<(Vector2Int cell, Vector2Int heading)>();
            stack.Push((start, Vector2Int.zero));
            while (stack.Count > 0)
            {
                var (cell, heading) = stack.Peek();
                var options = Dirs.Where(d => { var n = Wrap(cell + d); return corridor.Contains(n) && !visited.Contains(n); }).ToList();
                if (options.Count == 0) { stack.Pop(); continue; }

                // Going on straight more often than not makes corridors, not zigzags.
                var direction = options.Contains(heading) && rng.NextDouble() < StraightBias ? heading : options[rng.Next(options.Count)];
                var next = Wrap(cell + direction);
                open(cell, next);
                visited.Add(next);
                stack.Push((next, direction));
            }
            if (visited.Count != corridor.Count) throw new InvalidOperationException("The maze does not reach every cell; try another seed.");

            // A few loops. Opening a wall cuts the walls behind it off from the rest; it is only done when what is cut
            // off stays a big piece, so neither a lonely post nor a short stump of wall is left.
            int loops = 0;
            foreach (var cell in corridor.OrderBy(c => c.x * 1000 + c.y))
            {
                foreach (var d in new[] { Vector2Int.right, Vector2Int.up })
                {
                    var other = Wrap(cell + d);
                    if (!corridor.Contains(other) || linked[cell].Contains(other) || rng.NextDouble() >= LoopChance) continue;
                    if (!WallPiecesStayBig(cell, d, corridor, linked)) continue;
                    open(cell, other);
                    loops++;
                }
            }
            return loops;
        }

        /// <summary>
        /// True when the wall between a cell and its neighbour in direction d (right or up) can go: the walls that
        /// hang on either of its two ends still make up a piece of at least <see cref="MinWallPiece"/> posts.
        /// Walls are followed from post to post; a post is a corner of the grid, the bottom-left one of its cell.
        /// </summary>
        private static bool WallPiecesStayBig(Vector2Int cell, Vector2Int d, HashSet<Vector2Int> corridor, Dictionary<Vector2Int, List<Vector2Int>> linked)
        {
            var other = Wrap(cell + d);

            // A wall between two cells stands unless both are maze cells that are open to each other.
            Func<Vector2Int, Vector2Int, bool> wall = (a, b) =>
            {
                a = Wrap(a); b = Wrap(b);
                if ((a == cell && b == other) || (a == other && b == cell)) return false;   // the one being removed
                return !corridor.Contains(a) || !corridor.Contains(b) || !linked[a].Contains(b);
            };

            // The wall runs from the post of 'other' along the side the two cells share.
            var first = other;
            var second = Wrap(other + (d.x != 0 ? Vector2Int.up : Vector2Int.right));

            foreach (var start in new[] { first, second })
            {
                var seen = new HashSet<Vector2Int> { start };
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(start);
                while (queue.Count > 0 && seen.Count < MinWallPiece)
                {
                    var post = queue.Dequeue();
                    // The four walls that meet at a post, each between the two cells it separates.
                    var steps = new (Vector2Int to, Vector2Int a, Vector2Int b)[]
                    {
                        (post + Vector2Int.right, post, post + Vector2Int.down),
                        (post + Vector2Int.up, post, post + Vector2Int.left),
                        (post + Vector2Int.left, post + Vector2Int.left, post + Vector2Int.left + Vector2Int.down),
                        (post + Vector2Int.down, post + Vector2Int.down, post + Vector2Int.down + Vector2Int.left),
                    };
                    foreach (var (to, a, b) in steps)
                    {
                        var next = Wrap(to);
                        if (!wall(a, b) || !seen.Add(next)) continue;
                        queue.Enqueue(next);
                    }
                }
                if (seen.Count < MinWallPiece) return false;
            }
            return true;
        }

        private static Vector2Int DirectionBetween(Vector2Int a, Vector2Int b)
        {
            foreach (var d in Dirs)
            {
                if (Wrap(a + d) == b) return d;
            }
            throw new InvalidOperationException($"{a} and {b} are not neighbours");
        }

        /// <summary>Cells between two cells of the wrapped level, counted along the axes.</summary>
        private static int TorusDistance(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
            return Mathf.Min(dx, Cells - dx) + Mathf.Min(dy, Cells - dy);
        }

        /// <summary>Middle of the floor of a cell, in world units.</summary>
        private static Vector3 CellCenter(Vector2Int cell) => new Vector3(cell.x * T + (T + 1) * 0.5f - Origin, cell.y * T + (T + 1) * 0.5f - Origin, 0f);
    }
}
