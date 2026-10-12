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
    /// prefab to get a fresh one.
    ///
    /// A room is a mini level of about thirty by thirty tiles and a labyrinth of its own. It owns its ring of outer
    /// walls with a few doors and windows; inside, partitions cut it again and again into small rooms, and every
    /// partition has one door (now and then two), so the way through has to be found. One bigger chamber is kept whole
    /// as the hall of the room (open space, lounge, reception...). Storages and parkings are cut into fewer, larger
    /// chambers with rows of shelves and cars. Doors and partitions stand on the lattice of the maze (see
    /// <see cref="RoomTemplate"/>), and nothing that blocks movement is placed where it would cut floor off.
    /// </summary>
    public static class RoomFactory
    {
        public const string RoomsFolder = "Assets/Prefabs/Rooms";
        public const string DataFolder = "Assets/Data/Level";
        public const string LootFolder = "Assets/Data/Level/Loot";
        public const string LibraryPath = DataFolder + "/RoomLibrary.asset";

        private const string RuleTiles = "Assets/TileSet/Rule Tiles/";
        public const string WallTile = RuleTiles + "Walls/Wall-Concrete_01.asset";
        public const string WindowTile = RuleTiles + "Walls/Wall-Glass_03.asset";
        public const string CorridorFloorTile = RuleTiles + "Floors/Floor-Stone_01.asset";
        /// <summary>The glass is a little see-through, so the floor under a window shows.</summary>
        public static readonly Color WindowTint = new Color(1f, 1f, 1f, 0.8f);

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

        // Tiles a prop takes. Tall narrow things (shelves, cabinets, dispensers) take one tile by two: their sprites
        // are two tiles wide with empty sides, so they may stand shoulder to shoulder.
        private static readonly Dictionary<string, PropSpec> Specs = new Dictionary<string, PropSpec>
        {
            { "Desk", F(2, 2, true) }, { "Desk-2", F(2, 2, true) }, { "Boss-Desk", F(2, 2, true) }, { "Boss-Chair", F(2, 2) },
            { "Chair", F(1, 1) }, { "Chair-2", F(1, 1) }, { "Small-Table", F(1, 1, true) }, { "Big-Round-Table", F(2, 2, true) },
            { "Big-Sofa", F(2, 2, true) }, { "Big-Sofa-2", F(1, 2, true) }, { "Small-Sofa", F(1, 2, true) },
            { "Tall-Bookshelf", F(1, 2, true) }, { "Bookshelf", F(1, 1, true) }, { "Big-Plant", F(1, 2, true) }, { "Small-Plant", F(1, 1) },
            { "Toilet-Closed", F(2, 2, true) }, { "Toilet-Open", F(2, 2, true) }, { "WC-Sink", F(1, 1, true) },
            { "Car-Blue", F(2, 3, true) }, { "Car-Red", F(2, 3, true) }, { "Car-Gray", F(2, 3, true) },
            { "Crate", F(1, 1, true) }, { "Box", F(1, 1, true) }, { "ParkingSpot", F(2, 3) },
            { "Board", D() }, { "Mirror", D() }, { "Wall-Clock", D() }, { "Wall-Graph", D() }, { "Wall-Shelf", D() },
            { "Big-Filing-Cabinet", I(1, 2, true) }, { "Big-Office-Printer", I(2, 2, true) }, { "Bin", I(1, 1) },
            { "Coffee-Machine", I(1, 1) }, { "Filing-Cabinet-Open", I(1, 2, true) }, { "Filing-Cabinet-Small", I(1, 1, true) },
            { "Filing-Cabinet-Tall", I(1, 2, true) }, { "Printer", I(1, 1) }, { "Printer-Furniture", I(2, 2, true) },
            { "Vending-Machine", I(2, 2, true) }, { "Water-Dispenser", I(1, 2, true) }, { "WC-Paper", I(1, 1) },
            { "Wide-Filing-Cabinet", I(1, 2, true) },
        };

        private static readonly Dictionary<RoomKind, string> FloorTiles = new Dictionary<RoomKind, string>
        {
            { RoomKind.Office, "Floors/Floor-Stone_03" }, { RoomKind.BreakRoom, "Floors/Floor-Wood_01" },
            { RoomKind.BossOffice, "Floors/Floor-Wood_02" }, { RoomKind.Toilet, "Floors/Floor-Glass_01" },
            { RoomKind.Parking, "Floors/Floor-Metal_02" }, { RoomKind.Storage, "Floors/Floor-Metal_01" },
            { RoomKind.CopyRoom, "Floors/Floor-Stone_02" },
        };

        // Side rooms may have a floor of their own; the rest takes the floor of the kind.
        private static readonly Dictionary<string, string> RoleFloors = new Dictionary<string, string>
        {
            { "Cabinet", "Floors/Floor-Wood_01" }, { "Meeting", "Floors/Floor-Wood_02" }, { "BossCabinet", "Floors/Floor-Wood_01" },
            { "Stalls", "Floors/Floor-Glass_02" }, { "Janitor", "Floors/Floor-Metal_01" }, { "Kitchen", "Floors/Floor-Glass_01" },
            { "Archive", "Floors/Floor-Metal_01" }, { "PaperStore", "Floors/Floor-Metal_01" }, { "Closet", "Floors/Floor-Metal_01" },
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

        /// <summary>How a kind of room is cut into a labyrinth of small rooms.</summary>
        private class Cut
        {
            public string Hall;                  // what the one bigger chamber is; null when the kind has none
            public int HallMin, HallMax;         // its area in cells
            public int LeafMin = 2, LeafMax = 3; // sides of the small rooms in cells
            public (string role, int weight)[] Side = new (string, int)[0];
            public string MustHave;              // one small room always gets this role
            public bool MustHaveLargest;         // ... the largest one
            public string[] Decor = new string[0];
            public double Glass = 0.3;           // chance of a window in a partition
            public double SecondDoor = 0.15;     // chance of a second door in a long partition: a loop in the labyrinth
        }

        private static readonly Dictionary<RoomKind, Cut> Cuts = new Dictionary<RoomKind, Cut>
        {
            { RoomKind.Office, new Cut { Hall = "OpenSpace", HallMin = 9, HallMax = 20, Glass = 0.35,
                Side = new[] { ("Cabinet", 5), ("Meeting", 2), ("Archive", 2), ("Kitchen", 1), ("Closet", 1) },
                Decor = new[] { "Board", "Wall-Clock", "Wall-Graph" } } },
            { RoomKind.BreakRoom, new Cut { Hall = "Lounge", HallMin = 12, HallMax = 25, Glass = 0.35,
                Side = new[] { ("Lounge", 3), ("Kitchen", 2), ("Meeting", 2), ("Closet", 1) }, MustHave = "Kitchen",
                Decor = new[] { "Wall-Clock", "Wall-Graph", "Board" } } },
            { RoomKind.BossOffice, new Cut { Hall = "Reception", HallMin = 9, HallMax = 16, Glass = 0.4,
                Side = new[] { ("Cabinet", 3), ("Meeting", 2), ("Archive", 2), ("Stalls", 1), ("Closet", 1) },
                MustHave = "BossCabinet", MustHaveLargest = true, Decor = new[] { "Mirror", "Wall-Clock", "Wall-Shelf" } } },
            { RoomKind.Toilet, new Cut { Hall = "Washroom", HallMin = 6, HallMax = 12, Glass = 0.05,
                Side = new[] { ("Stalls", 5), ("Closet", 1) }, MustHave = "Janitor", Decor = new[] { "Mirror" } } },
            { RoomKind.CopyRoom, new Cut { Hall = "PrintHall", HallMin = 9, HallMax = 20, Glass = 0.25,
                Side = new[] { ("PaperStore", 3), ("Archive", 2), ("Cabinet", 2), ("Closet", 1) }, Decor = new[] { "Board", "Wall-Graph" } } },
            { RoomKind.Storage, new Cut { LeafMin = 3, LeafMax = 5, Glass = 0.15,
                Side = new[] { ("Warehouse", 8), ("Archive", 1), ("Closet", 1) }, Decor = new[] { "Wall-Shelf" } } },
            { RoomKind.Parking, new Cut { LeafMin = 3, LeafMax = 5, Glass = 0.2,
                Side = new[] { ("ParkingHall", 1) }, Decor = new[] { "Wall-Clock" } } },
        };

        private const int Variants = 2;
        private const int T = RoomSizes.CellTiles;   // tiles per cell of the maze: a wall line and the floor

        // ---- entry points ----

        [MenuItem("Mayham/Level/Generate Missing Rooms")]
        public static void GenerateMissing() => Debug.Log(Generate(false));

        /// <summary>Rebuilds every room prefab. Hand edits are lost.</summary>
        [MenuItem("Mayham/Level/Regenerate All Rooms (loses edits)")]
        public static void RegenerateAll()
        {
            if (!EditorUtility.DisplayDialog("Regenerate all rooms", "Every room prefab is rebuilt from scratch. Hand edits are lost.", "Regenerate", "Cancel")) return;
            Debug.Log(Generate(true));
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

        // ---- plan of one room ----

        /// <summary>A hall or a small room, in room cells (the last tile of a cell being the partition line).</summary>
        private class Area
        {
            public RectInt Cells;
            public string Role;
            public bool Hall;
            public int Doors;   // ways in and out; one means a dead end

            public int X0 => Cells.xMin * T;
            public int Y0 => Cells.yMin * T;
            public int X1 => Cells.xMax * T - 2;
            public int Y1 => Cells.yMax * T - 2;
            public int W => X1 - X0 + 1;
            public int H => Y1 - Y0 + 1;
            public int Tiles => W * H;
            public bool Contains(int x, int y) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1;
        }

        private class Plan
        {
            public readonly int W, H, CW, CH;
            public readonly Random Rng;
            public readonly List<Area> Areas = new List<Area>();
            public readonly List<(string prop, float x, float y)> Props = new List<(string, float, float)>();
            public readonly List<(string prop, float x, float y)> Decor = new List<(string, float, float)>();
            public readonly List<Vector2Int> Loot = new List<Vector2Int>();
            public readonly Dictionary<string, List<Vector2Int>> Placed = new Dictionary<string, List<Vector2Int>>();
            /// <summary>First tile of every door and whether it is in a wall that runs up.</summary>
            public readonly List<(Vector2Int tile, bool vertical)> Doors = new List<(Vector2Int, bool)>();

            // Indexed with an offset of one: the ring of outer walls lies at -1 and at W / H.
            private readonly bool[,] wall, window, blocked, occupied, reserved;
            private Vector2Int entrance = new Vector2Int(int.MinValue, 0);
            private static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

            public Plan(Vector2Int cells, Random rng)
            {
                CW = cells.x; CH = cells.y;
                W = CW * T - 1; H = CH * T - 1;
                Rng = rng;
                wall = new bool[W + 2, H + 2];
                window = new bool[W + 2, H + 2];
                blocked = new bool[W + 2, H + 2];
                occupied = new bool[W + 2, H + 2];
                reserved = new bool[W + 2, H + 2];
                for (int x = -1; x <= W; x++) { wall[x + 1, 0] = true; wall[x + 1, H + 1] = true; }
                for (int y = -1; y <= H; y++) { wall[0, y + 1] = true; wall[W + 1, y + 1] = true; }
            }

            public bool InGrid(int x, int y) => x >= -1 && y >= -1 && x <= W && y <= H;
            public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
            public bool IsWall(int x, int y) => wall[x + 1, y + 1];
            public bool IsWindow(int x, int y) => window[x + 1, y + 1];
            public bool IsBlocked(int x, int y) => blocked[x + 1, y + 1];
            public bool IsOccupied(int x, int y) => occupied[x + 1, y + 1];
            public bool IsFree(int x, int y) => Inside(x, y) && !IsWall(x, y) && !IsWindow(x, y) && !IsOccupied(x, y);

            public Area AreaAt(int x, int y) => Areas.FirstOrDefault(a => a.Contains(x, y));

            // ---- walls, doors, windows ----

            public void SetWall(int x, int y) { wall[x + 1, y + 1] = true; window[x + 1, y + 1] = false; }

            /// <summary>Opens two tiles of a wall and keeps the floor on both sides of the opening free.</summary>
            public void OpenDoor(Vector2Int a, Vector2Int b, bool vertical)
            {
                Doors.Add((a, vertical));
                foreach (var t in new[] { a, b })
                {
                    wall[t.x + 1, t.y + 1] = false;
                    window[t.x + 1, t.y + 1] = false;
                    reserved[t.x + 1, t.y + 1] = true;
                    if (entrance.x == int.MinValue && !Inside(t.x, t.y)) entrance = t;
                    // 'vertical' is a door in a wall that runs up: the way through it is along X.
                    for (int depth = 1; depth <= 2; depth++)
                    {
                        Reserve(vertical ? t.x + depth : t.x, vertical ? t.y : t.y + depth);
                        Reserve(vertical ? t.x - depth : t.x, vertical ? t.y : t.y - depth);
                    }
                }
            }

            public void SetWindow(int x, int y)
            {
                wall[x + 1, y + 1] = false;
                window[x + 1, y + 1] = true;
            }

            private void Reserve(int x, int y)
            {
                if (Inside(x, y)) reserved[x + 1, y + 1] = true;
            }

            // ---- reachability ----

            private bool Walkable(int x, int y, bool[,] blockedNow, bool[,] wallNow)
            {
                if (!InGrid(x, y) || wallNow[x + 1, y + 1] || window[x + 1, y + 1] || blockedNow[x + 1, y + 1]) return false;
                return true;
            }

            /// <summary>True when every tile that can be stood on is reachable from the first outer door.</summary>
            public bool AllReachable(bool[,] blockedNow = null, bool[,] wallNow = null)
            {
                blockedNow = blockedNow ?? blocked;
                wallNow = wallNow ?? wall;
                if (entrance.x == int.MinValue) return false;

                int free = 0;
                for (int x = -1; x <= W; x++)
                    for (int y = -1; y <= H; y++)
                        if (Walkable(x, y, blockedNow, wallNow)) free++;

                var seen = new bool[W + 2, H + 2];
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(entrance);
                seen[entrance.x + 1, entrance.y + 1] = true;
                int reached = 0;
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    reached++;
                    foreach (var d in Dirs)
                    {
                        int nx = c.x + d.x, ny = c.y + d.y;
                        if (!Walkable(nx, ny, blockedNow, wallNow) || seen[nx + 1, ny + 1]) continue;
                        seen[nx + 1, ny + 1] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
                return reached == free;
            }

            // ---- props ----

            public bool Fits(PropSpec spec, int x, int y, Area area)
            {
                for (int dx = 0; dx < spec.W; dx++)
                {
                    for (int dy = 0; dy < spec.H; dy++)
                    {
                        int tx = x + dx, ty = y + dy;
                        if (!IsFree(tx, ty)) return false;
                        if (area != null && !area.Contains(tx, ty)) return false;
                        if (spec.Blocks && reserved[tx + 1, ty + 1]) return false;
                    }
                }
                if (!spec.Blocks) return true;

                var blockedNow = (bool[,])blocked.Clone();
                for (int dx = 0; dx < spec.W; dx++)
                    for (int dy = 0; dy < spec.H; dy++) blockedNow[x + dx + 1, y + dy + 1] = true;
                return AllReachable(blockedNow);
            }

            public bool Place(string name, int x, int y, Area area = null)
            {
                var spec = Specs[name];
                if (!Fits(spec, x, y, area)) return false;
                for (int dx = 0; dx < spec.W; dx++)
                {
                    for (int dy = 0; dy < spec.H; dy++)
                    {
                        occupied[x + dx + 1, y + dy + 1] = true;
                        if (spec.Blocks) blocked[x + dx + 1, y + dy + 1] = true;
                    }
                }
                Props.Add((name, x + spec.W * 0.5f, y + spec.H * 0.5f));
                if (!Placed.TryGetValue(name, out var list)) Placed[name] = list = new List<Vector2Int>();
                list.Add(new Vector2Int(x, y));
                return true;
            }

            /// <summary>A marking on the floor (parking bay): it takes no room and blocks nothing.</summary>
            public void PlaceFlat(string name, int x, int y)
            {
                var spec = Specs[name];
                Props.Add((name, x + spec.W * 0.5f, y + spec.H * 0.5f));
            }

            public bool TryPlaceRandom(Area area, string name, int attempts = 40)
            {
                var spec = Specs[name];
                if (area.W < spec.W || area.H < spec.H) return false;
                for (int i = 0; i < attempts; i++)
                {
                    int x = Rng.Next(area.X0, area.X1 - spec.W + 2), y = Rng.Next(area.Y0, area.Y1 - spec.H + 2);
                    if (Place(name, x, y, area)) return true;
                }
                return false;
            }

            public int PlaceRandom(Area area, string name, int count)
            {
                int placed = 0;
                for (int i = 0; i < count; i++) if (TryPlaceRandom(area, name)) placed++;
                return placed;
            }

            /// <summary>Puts props against the walls of an area.</summary>
            public int PlaceAlongWalls(Area area, Func<string> pick, int count)
            {
                int placed = 0;
                for (int i = 0; i < count; i++)
                {
                    string name = pick();
                    var spec = Specs[name];
                    var spots = new List<Vector2Int>();
                    for (int x = area.X0; x <= area.X1 - spec.W + 1; x++)
                    {
                        spots.Add(new Vector2Int(x, area.Y0));
                        spots.Add(new Vector2Int(x, area.Y1 - spec.H + 1));
                    }
                    for (int y = area.Y0; y <= area.Y1 - spec.H + 1; y++)
                    {
                        spots.Add(new Vector2Int(area.X0, y));
                        spots.Add(new Vector2Int(area.X1 - spec.W + 1, y));
                    }
                    foreach (var s in spots.OrderBy(_ => Rng.Next()).Take(30))
                    {
                        if (!Place(name, s.x, s.y, area)) continue;
                        placed++;
                        break;
                    }
                }
                return placed;
            }

            public int PlaceAlongWalls(Area area, string name, int count) => PlaceAlongWalls(area, () => name, count);

            /// <summary>Puts a non-blocking prop (chair...) on a free tile next to a placed prop.</summary>
            public bool PlaceBeside(string name, Vector2Int anchor, string anchorName, Area area = null)
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
                    if (Place(name, c.x, c.y, area)) return true;
                }
                return false;
            }

            /// <summary>Puts a small machine on top of the table that was placed last: it shares the table's tile.</summary>
            public bool PlaceOnTop(string name, string tableName)
            {
                if (!Placed.TryGetValue(tableName, out var tables) || tables.Count == 0) return false;
                var table = tables[tables.Count - 1];
                Props.Add((name, table.x + 0.5f, table.y + 0.5f));
                return true;
            }

            /// <summary>A tile of wall inside the room (pillar, stall divider). Refused when it would cut floor off.</summary>
            public bool AddWallTile(int x, int y)
            {
                if (!IsFree(x, y) || reserved[x + 1, y + 1]) return false;
                var wallNow = (bool[,])wall.Clone();
                wallNow[x + 1, y + 1] = true;
                if (!AllReachable(null, wallNow)) return false;
                wall[x + 1, y + 1] = true;
                occupied[x + 1, y + 1] = true;
                return true;
            }

            // ---- decor and loot ----

            /// <summary>Things that hang on the outer walls, away from the doors, the windows and the partitions.</summary>
            public void AddDecor(string[] names, int count)
            {
                if (names.Length == 0) return;
                var spots = new List<Vector2>();
                Action<int, int, int, int> consider = (x, y, insideX, insideY) =>
                {
                    if (!IsWall(x, y) || IsWall(insideX, insideY) || IsWindow(insideX, insideY)) return;
                    spots.Add(new Vector2(x + 0.5f, y + 0.5f));
                };
                for (int x = 1; x < W - 1; x++) { consider(x, H, x, H - 1); consider(x, -1, x, 0); }
                for (int y = 1; y < H - 1; y++) { consider(-1, y, 0, y); consider(W, y, W - 1, y); }

                var chosen = new List<Vector2>();
                foreach (var spot in spots.OrderBy(_ => Rng.Next()))
                {
                    if (chosen.Count >= count) break;
                    if (chosen.Any(c => Vector2.Distance(c, spot) < 3f)) continue;
                    chosen.Add(spot);
                    Decor.Add((names[Rng.Next(names.Length)], spot.x, spot.y));
                }
            }

            /// <summary>Loot goes on free floor of an area, mostly next to furniture, at least two tiles apart.</summary>
            public void AddLootSpots(Area area, int count)
            {
                var free = new List<Vector2Int>();
                for (int x = area.X0; x <= area.X1; x++)
                    for (int y = area.Y0; y <= area.Y1; y++)
                        if (IsFree(x, y) && !IsBlocked(x, y)) free.Add(new Vector2Int(x, y));

                Func<Vector2Int, bool> nextToFurniture = t =>
                    Dirs.Any(d => Inside(t.x + d.x, t.y + d.y) && IsBlocked(t.x + d.x, t.y + d.y));

                int added = 0;
                foreach (var tile in free.OrderBy(t => (nextToFurniture(t) ? 0 : 1) + Rng.NextDouble() * 1.4))
                {
                    if (added >= count) break;
                    if (Loot.Any(l => Mathf.Abs(l.x - tile.x) < 2 && Mathf.Abs(l.y - tile.y) < 2)) continue;
                    Loot.Add(tile);
                    added++;
                }
            }
        }

        // ---- composing a room ----

        private static Plan Compose(RoomKind kind, RoomSize size, int variant)
        {
            var rng = new Random(((int)kind * 31 + (int)size) * 97 + variant * 13 + 7);
            var plan = new Plan(RoomSizes.Cells(size), rng);
            var cut = Cuts[kind];

            bool hallTaken = false;
            Divide(plan, cut, new RectInt(0, 0, plan.CW, plan.CH), ref hallTaken);
            AssignRoles(plan, cut, hallTaken);
            var outerDoors = AddOuterDoors(plan);
            AddOuterWindows(plan, outerDoors);
            CountDoors(plan);

            foreach (var area in plan.Areas) Furnish(plan, area, variant);
            plan.AddDecor(cut.Decor, (plan.W + plan.H) / 6);

            if (!plan.AllReachable()) throw new InvalidOperationException($"Room {kind} {size} {variant}: part of the floor cannot be reached.");
            return plan;
        }

        /// <summary>
        /// Cuts a region in two with a partition that has one door, then does the same to both parts until the rooms
        /// are small. Every partition is the only way between its two sides, which is what makes the room a labyrinth.
        /// </summary>
        private static void Divide(Plan plan, Cut cut, RectInt rect, ref bool hallTaken)
        {
            var rng = plan.Rng;
            int w = rect.width, h = rect.height, area = w * h;
            bool canCutX = w >= 2 * cut.LeafMin, canCutY = h >= 2 * cut.LeafMin;

            // The hall: the first region of a fitting size is kept whole.
            if (cut.Hall != null && !hallTaken && area >= cut.HallMin && area <= cut.HallMax && Mathf.Min(w, h) >= 3 && rng.NextDouble() < 0.7)
            {
                hallTaken = true;
                plan.Areas.Add(new Area { Cells = rect, Role = cut.Hall, Hall = true });
                return;
            }

            bool small = w <= cut.LeafMax && h <= cut.LeafMax;
            if ((!canCutX && !canCutY) || (small && (area <= cut.LeafMin * (cut.LeafMin + 1) || rng.NextDouble() < 0.55)))
            {
                plan.Areas.Add(new Area { Cells = rect });
                return;
            }

            // A partition that runs up cuts the region into a left and a right part.
            bool up;
            if (!canCutY) up = true;
            else if (!canCutX) up = false;
            else if (w > h * 1.25f) up = true;
            else if (h > w * 1.25f) up = false;
            else up = rng.Next(2) == 0;

            int at = rng.Next(cut.LeafMin, (up ? w : h) - cut.LeafMin + 1);   // cells of the first part
            int length = up ? h : w;                                         // cells along the partition
            int line = ((up ? rect.xMin : rect.yMin) + at) * T - 1;
            int from = (up ? rect.yMin : rect.xMin) * T - 1, to = (up ? rect.yMax : rect.xMax) * T - 1;
            for (int t = from; t <= to; t++)
            {
                if (up) plan.SetWall(line, t);
                else plan.SetWall(t, line);
            }

            // One cell of the partition is a door; a long one may get a second door and a window.
            var places = Enumerable.Range(up ? rect.yMin : rect.xMin, length).OrderBy(_ => rng.Next()).ToList();
            int doors = length >= 5 && rng.NextDouble() < cut.SecondDoor ? 2 : 1;
            int windows = places.Count > doors && rng.NextDouble() < cut.Glass ? (length >= 6 && rng.Next(2) == 0 ? 2 : 1) : 0;
            for (int i = 0; i < places.Count && i < doors + windows; i++)
            {
                int cellStart = places[i] * T;
                if (i < doors)
                {
                    // A door is two tiles of the floor of its cell.
                    int a = cellStart + rng.Next(0, T - 2);
                    plan.OpenDoor(up ? new Vector2Int(line, a) : new Vector2Int(a, line), up ? new Vector2Int(line, a + 1) : new Vector2Int(a + 1, line), up);
                }
                else
                {
                    // A window is as wide as the cell.
                    for (int t = cellStart; t <= cellStart + T - 2; t++)
                    {
                        if (up) plan.SetWindow(line, t);
                        else plan.SetWindow(t, line);
                    }
                }
            }

            if (up)
            {
                Divide(plan, cut, new RectInt(rect.xMin, rect.yMin, at, h), ref hallTaken);
                Divide(plan, cut, new RectInt(rect.xMin + at, rect.yMin, w - at, h), ref hallTaken);
            }
            else
            {
                Divide(plan, cut, new RectInt(rect.xMin, rect.yMin, w, at), ref hallTaken);
                Divide(plan, cut, new RectInt(rect.xMin, rect.yMin + at, w, h - at), ref hallTaken);
            }
        }

        private static void AssignRoles(Plan plan, Cut cut, bool hallTaken)
        {
            var rng = plan.Rng;
            // No region happened to fit the hall: the largest room becomes it.
            if (cut.Hall != null && !hallTaken)
            {
                var largest = plan.Areas.OrderByDescending(a => a.Tiles).First();
                largest.Role = cut.Hall;
                largest.Hall = true;
            }

            var free = plan.Areas.Where(a => a.Role == null).ToList();
            if (free.Count > 0 && cut.MustHave != null)
            {
                var chosen = cut.MustHaveLargest ? free.OrderByDescending(a => a.Tiles).First() : free[rng.Next(free.Count)];
                chosen.Role = cut.MustHave;
                free.Remove(chosen);
            }

            int total = cut.Side.Sum(s => s.weight);
            foreach (var area in free)
            {
                int pick = rng.Next(total);
                foreach (var (role, weight) in cut.Side)
                {
                    pick -= weight;
                    if (pick >= 0) continue;
                    area.Role = role;
                    break;
                }
            }
        }

        private class OuterDoor
        {
            public int Side;   // 0 bottom, 1 top, 2 left, 3 right
            public int Cell;   // along the side
        }

        /// <summary>Doors in the ring of outer walls: one on every side, a second one on some long sides.</summary>
        private static List<OuterDoor> AddOuterDoors(Plan plan)
        {
            var rng = plan.Rng;
            var doors = new List<OuterDoor>();
            for (int sideIndex = 0; sideIndex < 4; sideIndex++)
            {
                int cells = sideIndex < 2 ? plan.CW : plan.CH;
                int count = cells >= 11 && rng.Next(2) == 0 ? 2 : 1;
                for (int i = 0; i < count; i++)
                {
                    // The side is split into equal parts with a door somewhere in each, away from the corners.
                    int from = 1 + i * (cells - 2) / count, to = 1 + (i + 1) * (cells - 2) / count - 1;
                    if (count > 1) { from += i == 0 ? 0 : 1; to -= i == count - 1 ? 0 : 1; }
                    int cell = rng.Next(from, Mathf.Max(from, to) + 1);
                    doors.Add(new OuterDoor { Side = sideIndex, Cell = cell });

                    int a = cell * T + rng.Next(0, T - 2), b = a + 1;
                    if (sideIndex == 0) plan.OpenDoor(new Vector2Int(a, -1), new Vector2Int(b, -1), false);
                    else if (sideIndex == 1) plan.OpenDoor(new Vector2Int(a, plan.H), new Vector2Int(b, plan.H), false);
                    else if (sideIndex == 2) plan.OpenDoor(new Vector2Int(-1, a), new Vector2Int(-1, b), true);
                    else plan.OpenDoor(new Vector2Int(plan.W, a), new Vector2Int(plan.W, b), true);
                }
            }
            return doors;
        }

        /// <summary>Glass in the ring of outer walls: windows as wide as a corridor, between the doors.</summary>
        private static void AddOuterWindows(Plan plan, List<OuterDoor> doors)
        {
            var rng = plan.Rng;
            for (int sideIndex = 0; sideIndex < 4; sideIndex++)
            {
                int cells = sideIndex < 2 ? plan.CW : plan.CH;
                var free = Enumerable.Range(0, cells).Where(c => doors.All(d => d.Side != sideIndex || d.Cell != c)).OrderBy(_ => rng.Next()).ToList();
                int count = Mathf.Max(1, cells / 4);
                foreach (int cell in free.Take(count))
                {
                    for (int t = cell * T; t <= cell * T + T - 2; t++)
                    {
                        if (sideIndex == 0) plan.SetWindow(t, -1);
                        else if (sideIndex == 1) plan.SetWindow(t, plan.H);
                        else if (sideIndex == 2) plan.SetWindow(-1, t);
                        else plan.SetWindow(plan.W, t);
                    }
                }
            }
        }

        /// <summary>How many doors every room has: the ones with a single door are the dead ends of the labyrinth.</summary>
        private static void CountDoors(Plan plan)
        {
            foreach (var (tile, vertical) in plan.Doors)
            {
                var sides = vertical
                    ? new[] { new Vector2Int(tile.x - 1, tile.y), new Vector2Int(tile.x + 1, tile.y) }
                    : new[] { new Vector2Int(tile.x, tile.y - 1), new Vector2Int(tile.x, tile.y + 1) };
                foreach (var side in sides)
                {
                    var area = plan.AreaAt(side.x, side.y);
                    if (area != null) area.Doors++;
                }
            }
        }

        // ---- furniture ----

        private static void Furnish(Plan p, Area a, int variant)
        {
            var rng = p.Rng;
            Func<string[], string> pick = names => names[rng.Next(names.Length)];
            Func<int, int, int, int> clamp = (value, min, max) => Mathf.Clamp(value, min, max);
            int perimeter = 2 * (a.W + a.H);
            // A little loot in every room and more where the way ends: a dead end is worth the walk.
            Func<int, int> loot = basic => basic + (a.Doors <= 1 ? 2 : 0);

            switch (a.Role)
            {
                case "OpenSpace":
                {
                    // Desks in a grid with two-tile aisles, a chair at each.
                    for (int x = a.X0 + 1; x + 1 <= a.X1 - 1; x += 4)
                    {
                        for (int y = a.Y0 + 1; y + 1 <= a.Y1 - 1; y += 4)
                        {
                            if (rng.NextDouble() > 0.72) continue;
                            string desk = pick(new[] { "Desk", "Desk-2" });
                            if (p.Place(desk, x, y, a)) p.PlaceBeside(pick(new[] { "Chair", "Chair-2" }), new Vector2Int(x, y), desk, a);
                        }
                    }
                    p.PlaceAlongWalls(a, "Filing-Cabinet-Small", clamp(a.Tiles / 70, 2, 10));
                    p.PlaceAlongWalls(a, () => pick(new[] { "Filing-Cabinet-Tall", "Printer-Furniture", "Water-Dispenser" }), clamp(a.Tiles / 150, 1, 5));
                    p.PlaceAlongWalls(a, "Big-Plant", clamp(a.Tiles / 200, 1, 4));
                    p.PlaceRandom(a, "Small-Plant", 2);
                    p.PlaceRandom(a, "Bin", clamp(a.Tiles / 150, 1, 4));
                    p.AddLootSpots(a, loot(clamp(a.Tiles / 45, 3, 10)));
                    break;
                }
                case "Cabinet":
                {
                    string desk = pick(new[] { "Desk", "Desk-2" });
                    if (p.TryPlaceRandom(a, desk)) p.PlaceBeside(pick(new[] { "Chair", "Chair-2" }), p.Placed[desk].Last(), desk, a);
                    p.PlaceAlongWalls(a, () => pick(new[] { "Bookshelf", "Filing-Cabinet-Small" }), 3);
                    if (a.Tiles > 80) p.PlaceAlongWalls(a, "Tall-Bookshelf", 1);
                    p.PlaceRandom(a, "Small-Plant", 1);
                    p.PlaceRandom(a, "Bin", 1);
                    p.AddLootSpots(a, loot(1));
                    break;
                }
                case "Meeting":
                {
                    string table = "Big-Round-Table";
                    int tables = a.Tiles > 100 ? 2 : 1;
                    for (int i = 0; i < tables; i++)
                    {
                        bool placed = i == 0 ? p.Place(table, a.X0 + a.W / 2 - 1, a.Y0 + a.H / 2 - 1, a) : p.TryPlaceRandom(a, table);
                        if (!placed && !p.TryPlaceRandom(a, table)) continue;
                        for (int c = 0; c < 4; c++) p.PlaceBeside(pick(new[] { "Chair", "Chair-2" }), p.Placed[table].Last(), table, a);
                    }
                    p.PlaceAlongWalls(a, "Water-Dispenser", 1);
                    p.PlaceRandom(a, "Small-Plant", 2);
                    p.AddLootSpots(a, loot(1));
                    break;
                }
                case "Archive":
                {
                    p.PlaceAlongWalls(a, () => pick(new[] { "Filing-Cabinet-Tall", "Wide-Filing-Cabinet", "Big-Filing-Cabinet", "Filing-Cabinet-Open" }), clamp(perimeter / 6, 3, 9));
                    p.PlaceAlongWalls(a, "Filing-Cabinet-Small", 2);
                    p.PlaceRandom(a, "Box", 2);
                    p.AddLootSpots(a, loot(2));
                    break;
                }
                case "Lounge":
                {
                    // Too small for the grid below: one seating group wherever it fits.
                    if (a.W < 8 || a.H < 7)
                    {
                        if (p.TryPlaceRandom(a, "Big-Sofa"))
                        {
                            p.PlaceBeside("Small-Table", p.Placed["Big-Sofa"].Last(), "Big-Sofa", a);
                            p.TryPlaceRandom(a, "Small-Sofa");
                        }
                    }
                    // Seating groups on a grid: a table with chairs, or a sofa, a coffee table and an armchair in a row.
                    for (int x = a.X0 + 2; x + 3 <= a.X1 - 2; x += 6)
                    {
                        for (int y = a.Y0 + 2; y + 2 <= a.Y1 - 2; y += 5)
                        {
                            double roll = rng.NextDouble();
                            if (roll < 0.15) continue;
                            if (roll < 0.55) SeatingRow(p, a, x, y);
                            else if (roll < 0.9) TableWithChairs(p, a, x + 1, y, 4);
                            else p.Place("Big-Plant", x + 1, y, a);
                        }
                    }
                    p.PlaceAlongWalls(a, () => pick(new[] { "Vending-Machine", "Water-Dispenser" }), clamp(a.Tiles / 160, 1, 6));
                    for (int i = 0; i < 2; i++)
                        if (p.TryPlaceRandom(a, "Small-Table")) p.PlaceOnTop("Coffee-Machine", "Small-Table");
                    p.PlaceAlongWalls(a, "Big-Plant", clamp(a.Tiles / 180, 1, 4));
                    p.PlaceRandom(a, "Small-Plant", 2);
                    p.PlaceRandom(a, "Bin", 2);
                    p.AddLootSpots(a, loot(clamp(a.Tiles / 50, 1, 10)));
                    break;
                }
                case "Kitchen":
                {
                    p.PlaceAlongWalls(a, () => pick(new[] { "Vending-Machine", "Water-Dispenser" }), 3);
                    for (int i = 0; i < 2; i++)
                        if (p.TryPlaceRandom(a, "Small-Table")) p.PlaceOnTop("Coffee-Machine", "Small-Table");
                    if (p.TryPlaceRandom(a, "Big-Round-Table"))
                        for (int c = 0; c < 3; c++) p.PlaceBeside(pick(new[] { "Chair", "Chair-2" }), p.Placed["Big-Round-Table"].Last(), "Big-Round-Table", a);
                    p.PlaceRandom(a, "Bin", 2);
                    p.AddLootSpots(a, loot(1));
                    break;
                }
                case "BossCabinet":
                {
                    string desk = "Boss-Desk";
                    if (p.Place(desk, a.X0 + a.W / 2 - 1, a.Y0 + a.H / 2 - 1, a) || p.TryPlaceRandom(a, desk))
                        p.PlaceBeside("Boss-Chair", p.Placed[desk].Last(), desk, a);
                    p.PlaceRandom(a, "Big-Sofa-2", 2);
                    p.PlaceAlongWalls(a, "Tall-Bookshelf", 3);
                    p.PlaceAlongWalls(a, "Bookshelf", 3);
                    p.PlaceAlongWalls(a, "Filing-Cabinet-Tall", 1);
                    p.PlaceAlongWalls(a, "Big-Plant", 2);
                    p.PlaceRandom(a, "Small-Plant", 1);
                    p.AddLootSpots(a, loot(4));
                    break;
                }
                case "Reception":
                {
                    for (int i = 0; i < clamp(a.Tiles / 200, 1, 3); i++)
                    {
                        string desk = pick(new[] { "Desk", "Desk-2" });
                        if (p.TryPlaceRandom(a, desk)) p.PlaceBeside(pick(new[] { "Chair", "Chair-2" }), p.Placed[desk].Last(), desk, a);
                    }
                    // Places to wait on a grid: a sofa, a coffee table and an armchair in a row, a table or a plant.
                    for (int x = a.X0 + 2; x + 3 <= a.X1 - 2; x += 7)
                    {
                        for (int y = a.Y0 + 2; y + 2 <= a.Y1 - 2; y += 6)
                        {
                            double roll = rng.NextDouble();
                            if (roll < 0.25) continue;
                            if (roll < 0.7) SeatingRow(p, a, x, y);
                            else if (roll < 0.85) TableWithChairs(p, a, x + 1, y, 3);
                            else p.Place("Big-Plant", x + 1, y, a);
                        }
                    }
                    p.PlaceAlongWalls(a, "Big-Plant", clamp(a.Tiles / 120, 2, 5));
                    p.PlaceAlongWalls(a, "Water-Dispenser", 1);
                    p.PlaceAlongWalls(a, "Bookshelf", 3);
                    p.PlaceRandom(a, "Small-Plant", 2);
                    p.PlaceRandom(a, "Bin", 1);
                    p.AddLootSpots(a, loot(clamp(a.Tiles / 55, 3, 8)));
                    break;
                }
                case "Washroom":
                {
                    // Rows of stalls in the middle of the hall, sinks along its walls.
                    for (int y = a.Y0 + 3; y + 1 <= a.Y1 - 3; y += 6) StallRow(p, a, y, a.X0 + 3, a.X1 - 3);
                    p.PlaceAlongWalls(a, "WC-Sink", clamp(perimeter / 7, 4, 16));
                    p.PlaceRandom(a, "Bin", 3);
                    p.PlaceRandom(a, "Small-Plant", 1);
                    p.AddLootSpots(a, loot(clamp(a.Tiles / 60, 2, 6)));
                    break;
                }
                case "Stalls":
                {
                    StallRow(p, a, a.Y1 - 1, a.X0, a.X1);
                    if (a.H >= 8) p.PlaceAlongWalls(a, "WC-Sink", 3);
                    p.PlaceRandom(a, "Bin", 1);
                    p.AddLootSpots(a, loot(1));
                    break;
                }
                case "Janitor":
                case "Closet":
                {
                    p.PlaceAlongWalls(a, () => pick(new[] { "Crate", "Box" }), 5);
                    p.PlaceAlongWalls(a, a.Role == "Janitor" ? "WC-Sink" : "Filing-Cabinet-Small", 1);
                    p.PlaceRandom(a, "Bin", 1);
                    p.AddLootSpots(a, loot(2));
                    break;
                }
                case "PrintHall":
                {
                    // Big printers in rows, smaller machines and paper along the walls.
                    for (int x = a.X0 + 2; x + 1 <= a.X1 - 2; x += 5)
                    {
                        for (int y = a.Y0 + 2; y + 1 <= a.Y1 - 2; y += 4)
                        {
                            if (rng.NextDouble() > 0.6) continue;
                            p.Place(pick(new[] { "Big-Office-Printer", "Big-Office-Printer", "Printer-Furniture" }), x, y, a);
                        }
                    }
                    p.PlaceAlongWalls(a, () => pick(new[] { "Wide-Filing-Cabinet", "Printer-Furniture", "Big-Filing-Cabinet" }), clamp(perimeter / 14, 2, 8));
                    p.PlaceAlongWalls(a, "Filing-Cabinet-Small", clamp(perimeter / 20, 2, 5));
                    for (int i = 0; i < clamp(a.Tiles / 150, 1, 4); i++)
                        if (p.TryPlaceRandom(a, "Small-Table")) p.PlaceOnTop("Printer", "Small-Table");
                    p.PlaceRandom(a, "Bin", clamp(a.Tiles / 150, 1, 4));
                    p.AddLootSpots(a, loot(clamp(a.Tiles / 45, 3, 10)));
                    break;
                }
                case "PaperStore":
                {
                    p.PlaceAlongWalls(a, "Tall-Bookshelf", clamp(perimeter / 8, 2, 6));
                    p.PlaceRandom(a, "Box", 4);
                    p.PlaceRandom(a, "Crate", 2);
                    p.AddLootSpots(a, loot(2));
                    break;
                }
                case "Warehouse":
                {
                    // Rows of shelves standing shoulder to shoulder, with three-tile aisles between the rows.
                    // Every few shelves a gap lets one cross from aisle to aisle.
                    int margin = Mathf.Min(a.W, a.H) < 11 ? 1 : 2;
                    for (int y = a.Y0 + margin; y + 1 <= a.Y1 - margin; y += 5)
                    {
                        int run = 0, runLength = rng.Next(5, 9);
                        for (int x = a.X0 + margin; x <= a.X1 - margin; x++)
                        {
                            if (run == runLength) { run = 0; runLength = rng.Next(5, 9); x++; continue; }
                            run++;
                            double roll = rng.NextDouble();
                            if (roll < 0.08) continue;
                            if (roll < 0.22)
                            {
                                p.Place(pick(new[] { "Crate", "Box" }), x, y, a);
                                if (rng.Next(2) == 0) p.Place(pick(new[] { "Crate", "Box" }), x, y + 1, a);
                            }
                            else p.Place(pick(new[] { "Tall-Bookshelf", "Tall-Bookshelf", "Tall-Bookshelf", "Wide-Filing-Cabinet", "Big-Filing-Cabinet" }), x, y, a);
                        }
                    }
                    p.PlaceAlongWalls(a, () => pick(new[] { "Crate", "Box" }), clamp(perimeter / 10, 2, 12));
                    p.PlaceAlongWalls(a, "Filing-Cabinet-Open", 1);
                    p.PlaceRandom(a, "Bin", 1);
                    p.AddLootSpots(a, loot(clamp(a.Tiles / 35, 2, 6)));
                    break;
                }
                case "ParkingHall":
                {
                    // Rows of bays (two by three tiles, a tile apart) with four-tile lanes between the rows.
                    var cars = new[] { "Car-Blue", "Car-Red", "Car-Gray" };
                    double full = variant == 0 ? 0.6 : 0.4;
                    var rows = new List<int>();
                    for (int y = a.Y0 + 1; y + 2 <= a.Y1 - 1; y += 7) rows.Add(y);
                    // One more row against the far wall when the lane in front of it stays wide enough.
                    if (rows.Count > 0 && a.Y1 - 3 - (rows[rows.Count - 1] + 3) >= 3) rows.Add(a.Y1 - 3);
                    foreach (int y in rows)
                    {
                        int index = 0;
                        for (int x = a.X0 + 1; x + 1 <= a.X1 - 1; x += 3, index++)
                        {
                            // A pillar instead of every fifth bay.
                            if (index % 5 == 4) { p.AddWallTile(x, y + 1); continue; }
                            p.PlaceFlat("ParkingSpot", x, y);
                            if (rng.NextDouble() < full) p.Place(pick(cars), x, y, a);
                        }
                    }
                    p.PlaceAlongWalls(a, () => pick(new[] { "Crate", "Box" }), 2);
                    p.PlaceRandom(a, "Bin", 1);
                    p.AddLootSpots(a, loot(clamp(a.Tiles / 70, 1, 4)));
                    break;
                }
            }
        }

        /// <summary>A sofa, a coffee table and an armchair in a row, four tiles wide.</summary>
        private static void SeatingRow(Plan p, Area a, int x, int y)
        {
            if (!p.Place("Big-Sofa", x, y, a)) return;
            p.Place("Small-Table", x + 2, y, a);
            p.Place(p.Rng.Next(2) == 0 ? "Small-Sofa" : "Big-Sofa-2", x + 3, y, a);
        }

        private static void TableWithChairs(Plan p, Area a, int x, int y, int chairs)
        {
            if (!p.Place("Big-Round-Table", x, y, a)) return;
            for (int c = 0; c < chairs; c++) p.PlaceBeside(p.Rng.Next(2) == 0 ? "Chair" : "Chair-2", new Vector2Int(x, y), "Big-Round-Table", a);
        }

        /// <summary>A row of toilets facing down, with a divider of wall after each.</summary>
        private static void StallRow(Plan p, Area a, int y, int fromX, int toX)
        {
            for (int x = fromX; x + 1 <= toX; x += 3)
            {
                string toilet = p.Rng.Next(2) == 0 ? "Toilet-Closed" : "Toilet-Open";
                if (!p.Place(toilet, x, y, a)) continue;
                if (x + 2 <= toX)
                {
                    p.AddWallTile(x + 2, y);
                    p.AddWallTile(x + 2, y + 1);
                }
                if (p.Rng.Next(2) == 0) p.PlaceBeside("WC-Paper", new Vector2Int(x, y), toilet, a);
            }
        }

        // ---- prefab ----

        private static void BuildPrefab(string name, string path, RoomKind kind, RoomSize size, int variant)
        {
            var plan = Compose(kind, size, variant);

            var root = new GameObject(name);
            var template = root.AddComponent<RoomTemplate>();

            var tiles = new GameObject("Tiles", typeof(Grid)); tiles.transform.SetParent(root.transform, false);
            var floor = MakeTilemap("Floor", tiles.transform, -10);
            var walls = MakeTilemap("Walls", tiles.transform, 0);
            var windows = MakeTilemap("Windows", tiles.transform, 1);
            windows.color = WindowTint;
            var props = new GameObject("Props"); props.transform.SetParent(root.transform, false);
            var decor = new GameObject("Decor"); decor.transform.SetParent(root.transform, false);
            var loot = new GameObject("LootSpots"); loot.transform.SetParent(root.transform, false);

            var wallTile = AssetDatabase.LoadAssetAtPath<TileBase>(WallTile);
            var windowTile = AssetDatabase.LoadAssetAtPath<TileBase>(WindowTile);
            var kindFloor = AssetDatabase.LoadAssetAtPath<TileBase>(RuleTiles + FloorTiles[kind] + ".asset");
            var roleFloors = new Dictionary<string, TileBase>();
            Func<int, int, TileBase> floorAt = (x, y) =>
            {
                var area = plan.AreaAt(x, y);
                if (area == null || area.Hall || !RoleFloors.TryGetValue(area.Role, out string tilePath)) return kindFloor;
                if (!roleFloors.TryGetValue(tilePath, out var tile)) roleFloors[tilePath] = tile = AssetDatabase.LoadAssetAtPath<TileBase>(RuleTiles + tilePath + ".asset");
                return tile != null ? tile : kindFloor;
            };

            // The ring of outer walls is part of the room: tiles -1 and W / H.
            for (int x = -1; x <= plan.W; x++)
            {
                for (int y = -1; y <= plan.H; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    if (plan.IsWall(x, y)) { walls.SetTile(cell, wallTile); continue; }
                    floor.SetTile(cell, floorAt(x, y));
                    if (plan.IsWindow(x, y)) windows.SetTile(cell, windowTile);
                }
            }

            var so = new SerializedObject(template);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("size").enumValueIndex = (int)size;
            so.FindProperty("tiles").objectReferenceValue = tiles.transform;
            so.FindProperty("floor").objectReferenceValue = floor;
            so.FindProperty("walls").objectReferenceValue = walls;
            so.FindProperty("windows").objectReferenceValue = windows;
            so.FindProperty("props").objectReferenceValue = props.transform;
            so.FindProperty("decor").objectReferenceValue = decor.transform;
            so.FindProperty("lootSpots").objectReferenceValue = loot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var (prop, x, y) in plan.Props) Spawn(prop, props.transform, x, y);
            foreach (var (prop, x, y) in plan.Decor) Spawn(prop, decor.transform, x, y);
            int index = 1;
            foreach (var tile in plan.Loot)
            {
                var spot = new GameObject("Loot " + index++);
                spot.transform.SetParent(loot.transform, false);
                spot.transform.localPosition = new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
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

        /// <summary>Checks that the doors stand on the lattice and that the whole floor of every room can be walked.</summary>
        [MenuItem("Mayham/Level/Validate Rooms")]
        public static void ValidateMenu()
        {
            var report = Validate();
            if (report.Count == 0) Debug.Log("Rooms OK: the doors face the corridors and every floor tile is reachable.");
            else foreach (var line in report) Debug.LogWarning(line);
        }

        public static List<string> Validate()
        {
            var problems = new List<string>();
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { RoomsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var template = prefab.GetComponent<RoomTemplate>();
                if (template == null) continue;

                var size = template.Interior;
                int w = size.x, h = size.y;
                // Offset of one: the ring of outer walls lies at -1 and at w / h.
                var solid = new bool[w + 2, h + 2];
                Func<int, int, bool> inGrid = (x, y) => x >= -1 && y >= -1 && x <= w && y <= h;

                for (int x = -1; x <= w; x++)
                {
                    for (int y = -1; y <= h; y++)
                    {
                        var cell = new Vector3Int(x, y, 0);
                        bool glass = template.Windows != null && template.Windows.HasTile(cell);
                        solid[x + 1, y + 1] = template.Walls.HasTile(cell) || glass || !template.Floor.HasTile(cell);
                    }
                }

                foreach (var box in template.Props.GetComponentsInChildren<BoxCollider2D>())
                {
                    if (box.isTrigger) continue;
                    // box.bounds is not available for a prefab asset, so the box is worked out by hand.
                    Vector3 center = box.transform.TransformPoint(box.offset) - template.transform.position;
                    Vector2 half = Vector2.Scale(box.size, box.transform.lossyScale) * 0.5f;
                    for (int x = Mathf.FloorToInt(center.x - Mathf.Abs(half.x) + 0.05f); x <= Mathf.CeilToInt(center.x + Mathf.Abs(half.x) - 0.05f) - 1; x++)
                        for (int y = Mathf.FloorToInt(center.y - Mathf.Abs(half.y) + 0.05f); y <= Mathf.CeilToInt(center.y + Mathf.Abs(half.y) - 0.05f) - 1; y++)
                            if (inGrid(x, y)) solid[x + 1, y + 1] = true;
                }

                // Doors: gaps in the ring. They have to face the floor of a corridor cell.
                var doors = new List<Vector2Int>();
                for (int x = -1; x <= w; x++)
                {
                    for (int y = -1; y <= h; y++)
                    {
                        bool ring = x == -1 || y == -1 || x == w || y == h;
                        if (!ring || solid[x + 1, y + 1]) continue;
                        doors.Add(new Vector2Int(x, y));
                        int along = (x == -1 || x == w) ? y : x;
                        if (along < 0 || along % T == T - 1) problems.Add($"{prefab.name}: the door at ({x}, {y}) faces a corridor wall; doors keep off the offsets {T}k + {T - 1}.");
                    }
                }
                if (doors.Count == 0) { problems.Add($"{prefab.name}: no door in the outer walls."); continue; }

                int free = 0;
                for (int x = -1; x <= w; x++)
                    for (int y = -1; y <= h; y++)
                        if (!solid[x + 1, y + 1]) free++;

                var seen = new bool[w + 2, h + 2];
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(doors[0]);
                seen[doors[0].x + 1, doors[0].y + 1] = true;
                int reached = 0;
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    reached++;
                    foreach (var d in dirs)
                    {
                        int nx = c.x + d.x, ny = c.y + d.y;
                        if (!inGrid(nx, ny) || solid[nx + 1, ny + 1] || seen[nx + 1, ny + 1]) continue;
                        seen[nx + 1, ny + 1] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
                if (reached != free) problems.Add($"{prefab.name}: {free - reached} floor tiles cannot be reached from the doors.");
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
