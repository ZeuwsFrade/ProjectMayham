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
    /// Builds the corridor template of the open scene: a maze on a 51x51 grid of cells (a wall line and three
    /// tiles of floor each, so the corridors are three tiles wide) with nine room slots put into it, one in every
    /// region of a 3x3 grid.
    ///
    /// The maze fills everything between the rooms; a passage is always a corridor: one cell wide, a wall on both
    /// sides, no open squares and no wall posts standing on their own. What kind of maze it is (how many loops,
    /// how many dead ends, avenues or none) is said by <see cref="MazeSettings"/> and planned by
    /// <see cref="MazeLayout"/>; this class paints the plan and sets up the objects of the scene.
    /// The rooms are kept apart by a margin of maze, and no room lies across the seam of the wrapped level.
    ///
    /// The template is the same every time for the same settings (the seed is one of them); what changes from raid
    /// to raid are the rooms in the slots. The slots are left empty, outer walls included: a room brings its own
    /// walls, doors and windows.
    /// </summary>
    public static class TemplateBuilder
    {
        private const int T = RoomSizes.CellTiles;             // tiles per cell: a wall line and the floor
        private const int Cells = MazeLayout.Cells;            // 51 cells per side of the level
        private const int Tiles = Cells * T;                   // 204 tiles per side
        private const int Origin = MazeLayout.Region * T + (T + 1) / 2;    // tile index -> world cell: index - Origin (the spawn cell lies around 0, 0)
        private const int HoleCount = 12;                      // holes broken through the walls of the maze in a raid
        private const int HoleSpacing = 6;                     // cells between two holes
        private const int HoleMinDetour = 12;                  // cells a hole has to save, or it is not worth making
        private const float HoleClearance = 12f;               // world units around the start and the exit without holes

        public const string WindowLayer = "Window";
        public const string BarricadesFolder = "Assets/Prefabs/Barricades";
        public const string HolesFolder = "Assets/Prefabs/Holes";

        [MenuItem("Mayham/Level/Rebuild Corridor Template")]
        public static void BuildMenu() => Debug.Log(Build(MazeSettings.LoadOrCreate()));

        [MenuItem("Mayham/Level/Maze Presets/Classic")]
        public static void ClassicMenu() => BuildPreset(MazePreset.Classic);

        [MenuItem("Mayham/Level/Maze Presets/More Loops")]
        public static void LoopsMenu() => BuildPreset(MazePreset.Loops);

        [MenuItem("Mayham/Level/Maze Presets/More Loops, Shorter Walls")]
        public static void LoopsShortWallsMenu() => BuildPreset(MazePreset.LoopsShortWalls);

        [MenuItem("Mayham/Level/Maze Presets/Avenues and Loops")]
        public static void AvenuesMenu() => BuildPreset(MazePreset.Avenues);

        /// <summary>Writes a preset into the settings asset and rebuilds the template of the open scene with it.</summary>
        public static string BuildPreset(MazePreset preset)
        {
            var settings = MazeSettings.LoadOrCreate();
            settings.Apply(preset);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            string result = $"{preset}: {Build(settings)}";
            Debug.Log(result);
            return result;
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

        public static string Build(MazeSettings settings)
        {
            int seed = settings.Seed != 0 ? settings.Seed : new Random().Next(1, int.MaxValue);
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
            var layout = new MazeLayout(seed, settings);
            var rng = layout.Rng;
            var slots = layout.Slots;
            var corridor = layout.Corridor;
            var opened = layout.Opened;

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
            var spawn = MazeLayout.Spawn;
            var spawnPoint = maze.transform.Find("PlayerSpawn");
            if (spawnPoint != null) spawnPoint.position = CellCenter(spawn);
            // The player and the camera start in the middle of the spawn cell.
            var player = roots.FirstOrDefault(g => g.name == "Player");
            if (player != null) player.transform.position = CellCenter(spawn);
            var camera = roots.Select(g => g.GetComponent<Camera>()).FirstOrDefault(c => c != null);
            if (camera != null) camera.transform.position = new Vector3(CellCenter(spawn).x, CellCenter(spawn).y, camera.transform.position.z);

            var exitCell = layout.ExitCell();
            var exit = maze.transform.Find("ExitZone");
            if (exit != null) exit.position = CellCenter(exitCell);

            var enemies = roots.FirstOrDefault(g => g.name == "Enemies");
            if (enemies != null)
            {
                // Spread the test enemies over the maze, a good walk away from the start and from each other.
                var candidates = corridor.Where(c => MazeLayout.TorusDistance(c, spawn) >= 12 && c != exitCell).OrderBy(c => c.x * 1000 + c.y).OrderBy(_ => rng.Next()).ToList();
                var taken = new List<Vector2Int>();
                foreach (Transform enemy in enemies.transform)
                {
                    var pick = candidates.FirstOrDefault(c => taken.All(t => MazeLayout.TorusDistance(t, c) >= 14));
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
            float blind = layout.BlindShare(out int deepest);
            return $"Template built (seed {seed}): {Tiles}x{Tiles} tiles, {slots.Count} slots ({string.Join(", ", slots.GroupBy(s => s.Size).Select(g => g.Count() + " " + g.Key))}). " +
                   $"Maze of {corridor.Count} cells with {layout.Loops} loops, {layout.LedOn} dead ends led on and {layout.DeadEnds().Count} left; " +
                   $"blind branches are {100f * blind:0}% of it, the deepest {deepest} cells. " +
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

        /// <summary>True for the tiles of a slot: its floor and the ring of outer walls around it.</summary>
        private static bool InsideSlot(List<MazeLayout.Slot> slots, int tx, int ty)
        {
            foreach (var s in slots)
            {
                int x0 = s.Cell.x * T, x1 = (s.Cell.x + s.Span.x) * T;
                int y0 = s.Cell.y * T, y1 = (s.Cell.y + s.Span.y) * T;
                if (tx >= x0 && tx <= x1 && ty >= y0 && ty <= y1) return true;
            }
            return false;
        }

        /// <summary>Middle of the floor of a cell, in world units.</summary>
        private static Vector3 CellCenter(Vector2Int cell) => new Vector3(cell.x * T + (T + 1) * 0.5f - Origin, cell.y * T + (T + 1) * 0.5f - Origin, 0f);
    }
}
