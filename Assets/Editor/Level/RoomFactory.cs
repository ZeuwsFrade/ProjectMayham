using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Items;
using ProjectMayham.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = System.Random;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Generates the first versions of the room prefabs (two variants of every kind and size), the room library and the
    /// loot tables. Existing prefabs are never overwritten, so rooms that have been edited by hand are safe; delete a
    /// prefab to get a fresh one. The generated rooms follow one rule: the outer ring of floor stays free of anything
    /// that blocks movement, so every door leads to everything inside.
    /// </summary>
    public static class RoomFactory
    {
        public const string RoomsFolder = "Assets/Prefabs/Rooms";
        public const string DataFolder = "Assets/Data/Level";
        public const string LootFolder = "Assets/Data/Level/Loot";
        public const string LibraryPath = DataFolder + "/RoomLibrary.asset";

        private const string RuleTiles = "Assets/TileSet/Rule Tiles/";

        // ---- what exists ----

        private class PropSpec
        {
            public string Folder;
            public int W, H;
            public bool Blocks;
            public bool Decor;
        }

        private static PropSpec F(int w, int h, bool blocks = false) => new PropSpec { Folder = "Furniture", W = w, H = h, Blocks = blocks };
        private static PropSpec I(int w, int h, bool blocks = false) => new PropSpec { Folder = "Interactable", W = w, H = h, Blocks = blocks };
        private static PropSpec D() => new PropSpec { Folder = "Furniture", W = 1, H = 1, Decor = true };

        private static readonly Dictionary<string, PropSpec> Specs = new Dictionary<string, PropSpec>
        {
            { "Desk", F(2, 2, true) }, { "Desk-2", F(2, 2, true) }, { "Boss-Desk", F(2, 2, true) }, { "Boss-Chair", F(2, 2) },
            { "Chair", F(1, 1) }, { "Chair-2", F(1, 1) }, { "Small-Table", F(1, 1, true) }, { "Big-Round-Table", F(2, 2, true) },
            { "Big-Sofa", F(2, 2, true) }, { "Big-Sofa-2", F(2, 2, true) }, { "Small-Sofa", F(2, 2, true) },
            { "Tall-Bookshelf", F(2, 2, true) }, { "Bookshelf", F(1, 1, true) }, { "Big-Plant", F(2, 2, true) }, { "Small-Plant", F(1, 1) },
            { "Toilet-Closed", F(2, 2, true) }, { "Toilet-Open", F(2, 2, true) }, { "WC-Sink", F(1, 1, true) },
            { "Car-Blue", F(2, 3, true) }, { "Car-Red", F(2, 3, true) }, { "Car-Gray", F(2, 3, true) },
            { "Crate", F(1, 1, true) }, { "Box", F(1, 1, true) }, { "ParkingSpot", F(2, 3) },
            { "Board", D() }, { "Mirror", D() }, { "Wall-Clock", D() }, { "Wall-Graph", D() }, { "Wall-Shelf", D() },
            { "Big-Filing-Cabinet", I(2, 2, true) }, { "Big-Office-Printer", I(2, 2, true) }, { "Bin", I(1, 1) },
            { "Coffee-Machine", I(1, 1) }, { "Filing-Cabinet-Open", I(2, 2, true) }, { "Filing-Cabinet-Small", I(1, 1, true) },
            { "Filing-Cabinet-Tall", I(2, 2, true) }, { "Printer", I(1, 1) }, { "Printer-Furniture", I(2, 2, true) },
            { "Vending-Machine", I(2, 2, true) }, { "Water-Dispenser", I(2, 2, true) }, { "WC-Paper", I(1, 1) },
            { "Wide-Filing-Cabinet", I(2, 2, true) },
        };

        private static readonly Dictionary<RoomKind, string> FloorTiles = new Dictionary<RoomKind, string>
        {
            { RoomKind.Office, "Floors/Floor-Stone_03" }, { RoomKind.BreakRoom, "Floors/Floor-Wood_01" },
            { RoomKind.BossOffice, "Floors/Floor-Wood_02" }, { RoomKind.Toilet, "Floors/Floor-Glass_01" },
            { RoomKind.Parking, "Floors/Floor-Metal_02" }, { RoomKind.Storage, "Floors/Floor-Metal_01" },
            { RoomKind.CopyRoom, "Floors/Floor-Stone_02" },
        };

        private static readonly (RoomKind kind, RoomSize[] sizes, float weight)[] Kinds =
        {
            (RoomKind.Office, new[] { RoomSize.Small, RoomSize.Medium, RoomSize.Large }, 5f),
            (RoomKind.BreakRoom, new[] { RoomSize.Medium, RoomSize.Large }, 3f),
            (RoomKind.BossOffice, new[] { RoomSize.Medium, RoomSize.Large }, 1f),
            (RoomKind.Toilet, new[] { RoomSize.Small, RoomSize.Medium }, 3f),
            (RoomKind.Parking, new[] { RoomSize.Large }, 1f),
            (RoomKind.Storage, new[] { RoomSize.Medium, RoomSize.Large }, 3f),
            (RoomKind.CopyRoom, new[] { RoomSize.Small, RoomSize.Medium }, 3f),
        };

        private const int Variants = 2;

        // ---- entry points ----

        [MenuItem("Mayham/Level/Generate Missing Rooms")]
        public static void GenerateMissing() => Generate(false);

        /// <summary>Rebuilds every room prefab. Hand edits are lost.</summary>
        [MenuItem("Mayham/Level/Regenerate All Rooms (loses edits)")]
        public static void RegenerateAll()
        {
            if (!EditorUtility.DisplayDialog("Regenerate all rooms", "Every room prefab is rebuilt from scratch. Hand edits are lost.", "Regenerate", "Cancel")) return;
            Generate(true);
        }

        public static string Generate(bool force)
        {
            EnsureFolder(RoomsFolder);
            EnsureFolder(DataFolder);
            EnsureFolder(LootFolder);

            var created = new List<string>();
            foreach (var (kind, sizes, _) in Kinds)
            {
                foreach (var size in sizes)
                {
                    for (int variant = 0; variant < Variants; variant++)
                    {
                        string name = $"Room_{kind}_{size}_{(char)('A' + variant)}";
                        string path = $"{RoomsFolder}/{name}.prefab";
                        if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                        BuildPrefab(name, path, kind, size, variant);
                        created.Add(name);
                    }
                }
            }
            BuildLootTables();
            BuildLibrary();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return $"{created.Count} room prefabs created";
        }

        // ---- layout of one room ----

        private class Layout
        {
            public readonly int W, H;
            public readonly Random Rng;
            public readonly bool[,] Occupied;
            public readonly bool[,] Blocked;
            public readonly bool[,] Wall;
            public readonly List<(string prop, float x, float y)> Props = new List<(string, float, float)>();
            public readonly List<(string prop, float x, float y)> Decor = new List<(string, float, float)>();
            public readonly List<Vector2Int> Loot = new List<Vector2Int>();
            public readonly Dictionary<string, List<Vector2Int>> Placed = new Dictionary<string, List<Vector2Int>>();

            public Layout(int w, int h, Random rng)
            {
                W = w; H = h; Rng = rng;
                Occupied = new bool[w, h];
                Blocked = new bool[w, h];
                Wall = new bool[w, h];
            }

            public bool InRoom(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;

            // Blocking things may only stand inside the block that the free ring surrounds.
            public bool InBlock(int x, int y) => x >= 1 && y >= 1 && x <= W - 2 && y <= H - 2;

            public bool Fits(PropSpec spec, int x, int y)
            {
                for (int dx = 0; dx < spec.W; dx++)
                {
                    for (int dy = 0; dy < spec.H; dy++)
                    {
                        int tx = x + dx, ty = y + dy;
                        if (!InRoom(tx, ty) || Occupied[tx, ty] || Wall[tx, ty]) return false;
                        if (spec.Blocks && !InBlock(tx, ty)) return false;
                    }
                }
                if (!spec.Blocks) return true;
                return KeepsConnected(spec, x, y);
            }

            private bool KeepsConnected(PropSpec spec, int x, int y)
            {
                var blocked = (bool[,])Blocked.Clone();
                for (int dx = 0; dx < spec.W; dx++)
                    for (int dy = 0; dy < spec.H; dy++) blocked[x + dx, y + dy] = true;
                return AllReachable(blocked, Wall);
            }

            public bool AllReachable(bool[,] blocked, bool[,] wall)
            {
                int free = 0;
                for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (!blocked[x, y] && !wall[x, y]) free++;
                var seen = new bool[W, H];
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(Vector2Int.zero);
                seen[0, 0] = true;
                int reached = 0;
                var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    reached++;
                    foreach (var d in dirs)
                    {
                        int nx = c.x + d.x, ny = c.y + d.y;
                        if (!InRoom(nx, ny) || seen[nx, ny] || blocked[nx, ny] || wall[nx, ny]) continue;
                        seen[nx, ny] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
                return reached == free;
            }

            public bool Place(string name, int x, int y)
            {
                var spec = Specs[name];
                if (!Fits(spec, x, y)) return false;
                Commit(name, spec, x, y);
                return true;
            }

            private void Commit(string name, PropSpec spec, int x, int y)
            {
                for (int dx = 0; dx < spec.W; dx++)
                {
                    for (int dy = 0; dy < spec.H; dy++)
                    {
                        Occupied[x + dx, y + dy] = true;
                        if (spec.Blocks) Blocked[x + dx, y + dy] = true;
                    }
                }
                Props.Add((name, x + spec.W * 0.5f, y + spec.H * 0.5f));
                if (!Placed.TryGetValue(name, out var list)) Placed[name] = list = new List<Vector2Int>();
                list.Add(new Vector2Int(x, y));
            }

            public bool TryPlaceRandom(string name, int attempts = 60)
            {
                var spec = Specs[name];
                for (int i = 0; i < attempts; i++)
                {
                    int x = Rng.Next(0, W - spec.W + 1), y = Rng.Next(0, H - spec.H + 1);
                    if (Place(name, x, y)) return true;
                }
                return false;
            }

            public void PlaceRandom(string name, int count)
            {
                for (int i = 0; i < count; i++) TryPlaceRandom(name);
            }

            /// <summary>Puts a non-blocking prop (chair...) on a free tile next to a placed prop.</summary>
            public bool PlaceBeside(string name, Vector2Int anchor, string anchorName)
            {
                var anchorSpec = Specs[anchorName];
                var spec = Specs[name];
                var candidates = new List<Vector2Int>();
                for (int dx = 0; dx < anchorSpec.W; dx++)
                {
                    candidates.Add(new Vector2Int(anchor.x + dx, anchor.y - spec.H));        // south
                    candidates.Add(new Vector2Int(anchor.x + dx, anchor.y + anchorSpec.H));  // north
                }
                for (int dy = 0; dy < anchorSpec.H; dy++)
                {
                    candidates.Add(new Vector2Int(anchor.x - spec.W, anchor.y + dy));        // west
                    candidates.Add(new Vector2Int(anchor.x + anchorSpec.W, anchor.y + dy));  // east
                }
                foreach (var c in candidates.OrderBy(_ => Rng.Next()))
                {
                    if (Place(name, c.x, c.y)) return true;
                }
                return false;
            }

            /// <summary>Puts a small machine on top of a table: it shares the table's tile.</summary>
            public bool PlaceOnTop(string name, string tableName)
            {
                if (!Placed.TryGetValue(tableName, out var tables) || tables.Count == 0) return false;
                var table = tables[Rng.Next(tables.Count)];
                Props.Add((name, table.x + 0.5f, table.y + 0.5f));
                return true;
            }

            /// <summary>Marks a tile of the inner walls (partitions, pillars).</summary>
            public bool AddWall(int x, int y)
            {
                if (!InRoom(x, y) || Occupied[x, y] || !InBlock(x, y)) return false;
                var wall = (bool[,])Wall.Clone();
                wall[x, y] = true;
                if (!AllReachable(Blocked, wall)) return false;
                Wall[x, y] = true;
                Occupied[x, y] = true;
                return true;
            }

            public void AddDecor(string[] names, int count)
            {
                var spots = new List<Vector2>();
                for (int x = 1; x < W - 1; x++) { spots.Add(new Vector2(x + 0.5f, H + 0.5f)); spots.Add(new Vector2(x + 0.5f, -0.5f)); }
                for (int y = 1; y < H - 1; y++) { spots.Add(new Vector2(-0.5f, y + 0.5f)); spots.Add(new Vector2(W + 0.5f, y + 0.5f)); }
                spots = spots.OrderBy(_ => Rng.Next()).ToList();
                for (int i = 0; i < count && i < spots.Count; i++)
                {
                    Decor.Add((names[Rng.Next(names.Length)], spots[i].x, spots[i].y));
                }
            }

            /// <summary>Loot goes on free floor, mostly next to furniture, at least two tiles apart.</summary>
            public void AddLootSpots(int count)
            {
                var free = new List<Vector2Int>();
                for (int x = 0; x < W; x++)
                    for (int y = 0; y < H; y++)
                        if (!Blocked[x, y] && !Wall[x, y] && !Occupied[x, y]) free.Add(new Vector2Int(x, y));

                Func<Vector2Int, bool> nextToFurniture = t =>
                    new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down }
                        .Any(d => InRoom(t.x + d.x, t.y + d.y) && (Blocked[t.x + d.x, t.y + d.y]));

                var ordered = free.OrderBy(t => (nextToFurniture(t) ? 0 : 1) + Rng.NextDouble() * 1.4).ToList();
                foreach (var tile in ordered)
                {
                    if (Loot.Count >= count) break;
                    if (Loot.Any(l => Mathf.Abs(l.x - tile.x) < 2 && Mathf.Abs(l.y - tile.y) < 2)) continue;
                    Loot.Add(tile);
                }
            }
        }

        private static Layout Compose(RoomKind kind, RoomSize size, int variant)
        {
            var interior = RoomSizes.Interior(size);
            var rng = new Random(((int)kind * 31 + (int)size) * 97 + variant * 13 + 7);
            var L = new Layout(interior.x, interior.y, rng);
            int s = (int)size; // 0 small, 1 medium, 2 large
            Func<int, int, int, int> by = (small, medium, large) => s == 0 ? small : s == 1 ? medium : large;
            Func<string[], string> pick = names => names[rng.Next(names.Length)];

            switch (kind)
            {
                case RoomKind.Office:
                {
                    int desks = by(1, 2, 3);
                    for (int i = 0; i < desks; i++)
                    {
                        string desk = pick(new[] { "Desk", "Desk-2" });
                        if (!L.TryPlaceRandom(desk)) continue;
                        L.PlaceBeside(pick(new[] { "Chair", "Chair-2" }), L.Placed[desk].Last(), desk);
                    }
                    L.PlaceRandom("Filing-Cabinet-Small", by(1, 2, 3));
                    if (s == 2) L.PlaceRandom("Filing-Cabinet-Tall", 1);
                    if (s >= 1) L.PlaceRandom("Big-Plant", rng.Next(0, 2));
                    L.PlaceRandom("Small-Plant", 1);
                    L.PlaceRandom("Bin", 1);
                    L.AddDecor(new[] { "Board", "Wall-Clock", "Wall-Graph" }, by(1, 2, 3));
                    L.AddLootSpots(by(3, 5, 8));
                    break;
                }
                case RoomKind.BreakRoom:
                {
                    L.PlaceRandom("Big-Sofa", s == 2 ? 2 : 1);
                    if (s == 2) L.PlaceRandom("Small-Sofa", 1);
                    string round = "Big-Round-Table";
                    if (L.TryPlaceRandom(round)) { L.PlaceBeside("Chair", L.Placed[round].Last(), round); L.PlaceBeside("Chair-2", L.Placed[round].Last(), round); }
                    if (L.TryPlaceRandom("Small-Table")) L.PlaceOnTop("Coffee-Machine", "Small-Table");
                    L.PlaceRandom("Vending-Machine", 1);
                    L.PlaceRandom("Water-Dispenser", s == 2 ? 2 : 1);
                    if (s == 2) L.PlaceRandom("Big-Plant", 1);
                    L.PlaceRandom("Bin", 1);
                    L.AddDecor(new[] { "Wall-Clock", "Wall-Graph", "Board" }, by(1, 2, 3));
                    L.AddLootSpots(by(3, 5, 8));
                    break;
                }
                case RoomKind.BossOffice:
                {
                    string desk = "Boss-Desk";
                    if (L.TryPlaceRandom(desk)) L.PlaceBeside("Boss-Chair", L.Placed[desk].Last(), desk);
                    L.PlaceRandom("Big-Sofa-2", 1);
                    L.PlaceRandom("Tall-Bookshelf", s == 2 ? 2 : 1);
                    L.PlaceRandom("Bookshelf", s == 2 ? 3 : 1);
                    if (s == 2) L.PlaceRandom("Filing-Cabinet-Tall", 1);
                    L.PlaceRandom("Big-Plant", 1);
                    L.PlaceRandom("Small-Plant", 1);
                    L.AddDecor(new[] { "Mirror", "Wall-Clock", "Wall-Shelf" }, by(1, 2, 3));
                    L.AddLootSpots(by(4, 6, 9));
                    break;
                }
                case RoomKind.Toilet:
                {
                    string toilet = pick(new[] { "Toilet-Closed", "Toilet-Open" });
                    if (s == 0)
                    {
                        L.Place(toilet, 1, 2);
                        L.Place("WC-Sink", 3, 3);
                        L.Place("WC-Sink", 3, 2);
                    }
                    else
                    {
                        // Two stalls separated by a partition, sinks along the right side.
                        L.Place(toilet, 1, 2);
                        L.AddWall(3, 2); L.AddWall(3, 3);
                        L.Place(pick(new[] { "Toilet-Closed", "Toilet-Open" }), 4, 2);
                        L.Place("WC-Sink", 6, 3); L.Place("WC-Sink", 6, 2);
                    }
                    if (L.Placed.TryGetValue(toilet, out var placed)) L.PlaceBeside("WC-Paper", placed[0], toilet);
                    L.PlaceRandom("Bin", 1);
                    L.AddDecor(new[] { "Mirror" }, by(1, 2, 2));
                    L.AddLootSpots(by(2, 3, 4));
                    break;
                }
                case RoomKind.CopyRoom:
                {
                    L.PlaceRandom("Big-Office-Printer", s == 0 ? 1 : 2);
                    if (s >= 1) { L.PlaceRandom("Printer-Furniture", 1); L.PlaceRandom("Wide-Filing-Cabinet", 1); }
                    if (L.TryPlaceRandom("Small-Table")) L.PlaceOnTop("Printer", "Small-Table");
                    L.PlaceRandom("Filing-Cabinet-Small", s == 0 ? 1 : 2);
                    L.PlaceRandom("Bin", 1);
                    L.AddDecor(new[] { "Board", "Wall-Graph" }, by(1, 2, 2));
                    L.AddLootSpots(by(3, 5, 5));
                    break;
                }
                case RoomKind.Storage:
                {
                    L.PlaceRandom("Tall-Bookshelf", by(1, 2, 4));
                    L.PlaceRandom("Wide-Filing-Cabinet", by(1, 1, 2));
                    if (s == 2) L.PlaceRandom("Filing-Cabinet-Open", 1);
                    L.PlaceRandom("Crate", by(2, 3, 6));
                    L.PlaceRandom("Box", by(2, 3, 6));
                    L.PlaceRandom("Bin", 1);
                    L.AddDecor(new[] { "Wall-Shelf" }, by(1, 2, 3));
                    L.AddLootSpots(by(4, 7, 11));
                    break;
                }
                case RoomKind.Parking:
                {
                    // Four marked bays (two columns, two rows) with an aisle between the columns.
                    var bays = new[] { new Vector2Int(1, 1), new Vector2Int(5, 1), new Vector2Int(1, 4), new Vector2Int(5, 4) };
                    var cars = new[] { "Car-Blue", "Car-Red", "Car-Gray" };
                    int occupiedBays = variant == 0 ? 3 : 2 + rng.Next(0, 2);
                    var order = bays.OrderBy(_ => rng.Next()).ToList();
                    for (int i = 0; i < bays.Length; i++)
                    {
                        L.Props.Add(("ParkingSpot", order[i].x + 1f, order[i].y + 1.5f));
                        if (i < occupiedBays) L.Place(pick(cars), order[i].x, order[i].y);
                    }
                    // A pillar or two in the aisle.
                    L.AddWall(3, 3);
                    if (variant == 1) L.AddWall(4, 4);
                    L.AddLootSpots(7);
                    break;
                }
            }
            return L;
        }

        // ---- prefab ----

        private static void BuildPrefab(string name, string path, RoomKind kind, RoomSize size, int variant)
        {
            var layout = Compose(kind, size, variant);
            var interior = RoomSizes.Interior(size);

            var root = new GameObject(name);
            var template = root.AddComponent<RoomTemplate>();

            var tiles = new GameObject("Tiles", typeof(Grid)); tiles.transform.SetParent(root.transform, false);
            var floor = MakeTilemap("Floor", tiles.transform, -10);
            var walls = MakeTilemap("Walls", tiles.transform, 0);
            var props = new GameObject("Props"); props.transform.SetParent(root.transform, false);
            var decor = new GameObject("Decor"); decor.transform.SetParent(root.transform, false);
            var loot = new GameObject("LootSpots"); loot.transform.SetParent(root.transform, false);

            var floorTile = AssetDatabase.LoadAssetAtPath<TileBase>(RuleTiles + FloorTiles[kind] + ".asset");
            var wallName = kind == RoomKind.Toilet ? "Walls/Wall-Metal_01" : "Walls/Wall-Concrete_01";
            var wallTile = AssetDatabase.LoadAssetAtPath<TileBase>(RuleTiles + wallName + ".asset");
            for (int x = 0; x < layout.W; x++)
            {
                for (int y = 0; y < layout.H; y++)
                {
                    if (layout.Wall[x, y]) walls.SetTile(new Vector3Int(x, y, 0), wallTile);
                    else floor.SetTile(new Vector3Int(x, y, 0), floorTile);
                }
            }

            var so = new SerializedObject(template);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("size").enumValueIndex = (int)size;
            so.FindProperty("tiles").objectReferenceValue = tiles.transform;
            so.FindProperty("floor").objectReferenceValue = floor;
            so.FindProperty("walls").objectReferenceValue = walls;
            so.FindProperty("props").objectReferenceValue = props.transform;
            so.FindProperty("decor").objectReferenceValue = decor.transform;
            so.FindProperty("lootSpots").objectReferenceValue = loot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);

            // Nested prefab instances can only be added to a prefab that is open in a (preview) scene.
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var (prop, x, y) in layout.Props) Spawn(prop, contents.transform.Find("Props"), x, y);
                foreach (var (prop, x, y) in layout.Decor) Spawn(prop, contents.transform.Find("Decor"), x, y);
                int index = 1;
                foreach (var tile in layout.Loot)
                {
                    var spot = new GameObject("Loot " + index++);
                    spot.transform.SetParent(contents.transform.Find("LootSpots"), false);
                    spot.transform.localPosition = new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f);
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static Tilemap MakeTilemap(string name, Transform parent, int order)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = order;
            return go.GetComponent<Tilemap>();
        }

        private static void Spawn(string prop, Transform parent, float x, float y)
        {
            var spec = Specs[prop];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{spec.Folder}/{prop}.prefab");
            if (prefab == null) { Debug.LogWarning($"RoomFactory: prefab {prop} not found"); return; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = new Vector3(x, y, 0f);
        }

        // ---- loot and library ----

        private static void BuildLootTables()
        {
            Func<string, ItemDefinition> item = n => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Data/Items/{n}.asset");
            var tables = new Dictionary<string, (string item, float weight, int min, int max)[]>
            {
                { "Office", new[] { ("Papers", 6f, 1, 3), ("Folders", 3f, 1, 1), ("Notes", 3f, 1, 2), ("Mug", 3f, 1, 1), ("Books", 1f, 1, 1) } },
                { "BreakRoom", new[] { ("Mug", 6f, 1, 1), ("Books", 2f, 1, 1), ("Notes", 2f, 1, 1), ("Papers", 1f, 1, 2) } },
                { "BossOffice", new[] { ("Books", 4f, 1, 2), ("Folders", 4f, 1, 2), ("Mug", 2f, 1, 1), ("Notes", 2f, 1, 1), ("Papers", 2f, 2, 4) } },
                { "Toilet", new[] { ("Papers", 4f, 1, 2), ("Mop", 2f, 1, 1), ("Mug", 1f, 1, 1), ("Notes", 1f, 1, 1) } },
                { "CopyRoom", new[] { ("Papers", 8f, 2, 5), ("Folders", 3f, 1, 2), ("Notes", 2f, 1, 1) } },
                { "Storage", new[] { ("Books", 3f, 1, 2), ("Folders", 3f, 1, 2), ("Mop", 3f, 1, 1), ("Papers", 2f, 1, 3), ("Mug", 1f, 1, 1) } },
                { "Parking", new[] { ("Notes", 3f, 1, 1), ("Mop", 1f, 1, 1), ("Mug", 2f, 1, 1), ("Papers", 1f, 1, 2) } },
                { "Corridor", new[] { ("Papers", 5f, 1, 2), ("Notes", 4f, 1, 1), ("Mug", 1f, 1, 1), ("Folders", 1f, 1, 1) } },
            };

            foreach (var pair in tables)
            {
                string path = $"{LootFolder}/Loot_{pair.Key}.asset";
                var table = AssetDatabase.LoadAssetAtPath<LootTable>(path);
                bool isNew = table == null;
                if (isNew) { table = ScriptableObject.CreateInstance<LootTable>(); AssetDatabase.CreateAsset(table, path); }
                else continue; // hand-tuned tables are not overwritten

                var so = new SerializedObject(table);
                var list = so.FindProperty("entries");
                list.arraySize = pair.Value.Length;
                for (int i = 0; i < pair.Value.Length; i++)
                {
                    var e = list.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("item").objectReferenceValue = item(pair.Value[i].item);
                    e.FindPropertyRelative("weight").floatValue = pair.Value[i].weight;
                    e.FindPropertyRelative("minCount").intValue = pair.Value[i].min;
                    e.FindPropertyRelative("maxCount").intValue = pair.Value[i].max;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(table);
            }
        }

        private static void BuildLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<RoomLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<RoomLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            // Kind settings are kept when they already exist, so weights tuned by hand survive a regeneration.
            var existing = library.Kinds.ToDictionary(k => k.kind);
            var infos = new List<RoomLibrary.KindInfo>();
            foreach (var (kind, sizes, weight) in Kinds)
            {
                if (existing.TryGetValue(kind, out var info)) { infos.Add(info); continue; }
                infos.Add(new RoomLibrary.KindInfo
                {
                    kind = kind,
                    weight = weight,
                    sizes = sizes,
                    loot = AssetDatabase.LoadAssetAtPath<LootTable>($"{LootFolder}/Loot_{kind}.asset"),
                    lootChance = 0.75f,
                });
            }

            var rooms = AssetDatabase.FindAssets("t:GameObject", new[] { RoomsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Select(g => g.GetComponent<RoomTemplate>())
                .Where(t => t != null)
                .OrderBy(t => t.name, StringComparer.Ordinal)
                .ToList();
            library.Set(infos, rooms);
            EditorUtility.SetDirty(library);
        }

        // ---- checking rooms ----

        /// <summary>Checks the rule of the free ring and that the whole floor of every room can be walked.</summary>
        [MenuItem("Mayham/Level/Validate Rooms")]
        public static void ValidateMenu()
        {
            var report = Validate();
            if (report.Count == 0) Debug.Log("Rooms OK: the outer ring is free and every floor tile is reachable.");
            else foreach (var line in report) Debug.LogWarning(line);
        }

        public static List<string> Validate()
        {
            var problems = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { RoomsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var template = prefab.GetComponent<RoomTemplate>();
                if (template == null) continue;

                var size = template.Interior;
                var layout = new Layout(size.x, size.y, new Random(1));
                foreach (var cell in template.Walls.cellBounds.allPositionsWithin)
                    if (template.Walls.HasTile(cell) && layout.InRoom(cell.x, cell.y)) layout.Wall[cell.x, cell.y] = true;

                foreach (var box in template.Props.GetComponentsInChildren<BoxCollider2D>())
                {
                    if (box.isTrigger) continue;
                    // box.bounds is not available for a prefab asset, so the box is worked out by hand.
                    Vector3 center = box.transform.TransformPoint(box.offset) - template.transform.position;
                    Vector2 half = Vector2.Scale(box.size, box.transform.lossyScale) * 0.5f;
                    for (int x = Mathf.FloorToInt(center.x - Mathf.Abs(half.x) + 0.05f); x <= Mathf.CeilToInt(center.x + Mathf.Abs(half.x) - 0.05f) - 1; x++)
                        for (int y = Mathf.FloorToInt(center.y - Mathf.Abs(half.y) + 0.05f); y <= Mathf.CeilToInt(center.y + Mathf.Abs(half.y) - 0.05f) - 1; y++)
                            if (layout.InRoom(x, y)) layout.Blocked[x, y] = true;
                }

                for (int x = 0; x < size.x; x++)
                {
                    for (int y = 0; y < size.y; y++)
                    {
                        bool ring = x == 0 || y == 0 || x == size.x - 1 || y == size.y - 1;
                        if (ring && (layout.Blocked[x, y] || layout.Wall[x, y]))
                        {
                            problems.Add($"{prefab.name}: the outer ring is blocked at ({x}, {y}).");
                            x = size.x; break;
                        }
                    }
                }
                if (!layout.AllReachable(layout.Blocked, layout.Wall)) problems.Add($"{prefab.name}: part of the floor cannot be reached.");
            }
            return problems;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
