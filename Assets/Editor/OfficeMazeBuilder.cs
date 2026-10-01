using System.Collections.Generic;
using ProjectMayham.Vision;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Rebuilds the office maze (rooms + corridors-by-doors, tilemaps, furniture, interactables, test enemies)
    /// in the active scene. Deterministic for a given seed and safe to re-run.
    /// </summary>
    public static class OfficeMazeBuilder
    {
        [MenuItem("Mayham/Build Office Maze (active scene)")]
        private static void BuildFromMenu() => Debug.Log(Build());

        public static string Build()
        {
            var b = new Builder(20261001);
            return b.Run();
        }

        private enum RoomType { Lobby, Office, Boss, Lounge, Storage, Bathroom, Break, Print, Library }

        private sealed class Room
        {
            public int col, row, x0, y0, x1, y1;
            public RoomType type;
            public readonly List<Vector2Int> doorCells = new List<Vector2Int>();
            public Vector2Int Center => new Vector2Int((x0 + x1) / 2, (y0 + y1) / 2);
        }

        private sealed class Placed
        {
            public Vector2Int pos;
            public int w, h;
        }

        private sealed class Builder
        {
            private const string SpriteDir = "Assets/Sprites/Office Furniture/";
            private static readonly int[] ColW = { 9, 7, 11, 8, 9 };
            private static readonly int[] RowH = { 8, 10, 7, 9 };

            // Interactables are hidden in the fog until first seen; everything else is always drawn like the walls.
            private static readonly HashSet<string> Interactable = new HashSet<string>
            {
                "Filing-Cabinet-Small", "Filing-Cabinet-Tall", "Filing-Cabinet-Open", "Wide-Filing-Cabinet", "Big-Filing-Cabinet",
                "Vending-Machine", "Water-Dispenser", "Coffee-Machine", "Printer", "Big-Office-Printer", "Printer-Furniture",
                "Bin", "Papers", "Folders", "Folders-2", "Books", "WC-Paper", "Wall-Note", "Wall-Note-2"
            };

            // Small / flat things that neither block movement nor sight.
            private static readonly HashSet<string> NonSolid = new HashSet<string>
            {
                "Chair", "Chair-2", "Small-Plant", "Boss-Chair", "Papers", "Folders", "Folders-2", "Books", "WC-Paper", "Bin",
                "Mirror", "Board", "Wall-Clock", "Wall-Graph", "Wall-Shelf", "Wall-Note", "Wall-Note-2"
            };

            private readonly System.Random rng;
            private int W, H, offX, offY;
            private bool[,] wall, occ, reserved;
            private Room[,] roomAt;
            private readonly List<Room> rooms = new List<Room>();
            private Room[,] grid;
            private int[] wx, wy;
            private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
            private Transform furnitureRoot, interactableRoot;
            private int obstacleLayer, furnitureLayer;
            private int solidCount, interactableCount, furnitureCount;

            public Builder(int seed) { rng = new System.Random(seed); }

            public string Run()
            {
                obstacleLayer = LayerMask.NameToLayer("Obstacle");
                if (obstacleLayer < 0) return "Layer 'Obstacle' is missing";
                furnitureLayer = EnsureLayer("Furniture");

                var scene = EditorSceneManager.GetActiveScene();
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (go.name == "Floor" || go.name == "Obstacles" || go.name == "Enemies" || go.name == "Maze" ||
                        go.name == "Furniture" || go.name == "Interactables")
                        Object.DestroyImmediate(go);
                }

                GenerateLayout();
                AssignRoomTypes();
                BuildTilemaps();

                furnitureRoot = new GameObject("Furniture").transform;
                interactableRoot = new GameObject("Interactables").transform;
                foreach (var r in rooms) Furnish(r);

                var enemies = SpawnEnemies();
                var spawn = PlacePlayerAndCamera();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return $"ok size={W}x{H} rooms={rooms.Count} furniture={furnitureCount} (solid {solidCount}) interactables={interactableCount} enemies={enemies} spawn={spawn}";
            }

            // ---------- layout ----------

            private void GenerateLayout()
            {
                int cols = ColW.Length, rws = RowH.Length;
                wx = new int[cols + 1]; wy = new int[rws + 1];
                for (int i = 0; i < cols; i++) wx[i + 1] = wx[i] + ColW[i] + 1;
                for (int i = 0; i < rws; i++) wy[i + 1] = wy[i] + RowH[i] + 1;
                W = wx[cols] + 1; H = wy[rws] + 1;
                offX = -W / 2; offY = -H / 2;

                wall = new bool[W, H]; occ = new bool[W, H]; reserved = new bool[W, H]; roomAt = new Room[W, H];
                for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) wall[x, y] = true;

                grid = new Room[cols, rws];
                for (int c = 0; c < cols; c++)
                    for (int r = 0; r < rws; r++)
                    {
                        var room = new Room { col = c, row = r, x0 = wx[c] + 1, x1 = wx[c + 1] - 1, y0 = wy[r] + 1, y1 = wy[r + 1] - 1 };
                        grid[c, r] = room; rooms.Add(room);
                        for (int x = room.x0; x <= room.x1; x++)
                            for (int y = room.y0; y <= room.y1; y++) { wall[x, y] = false; roomAt[x, y] = room; }
                    }

                // Spanning tree (randomised DFS) => every room reachable, one long winding route; plus a few loops.
                var visited = new bool[cols, rws];
                var edges = new HashSet<(int, int, bool)>();
                var stack = new Stack<Vector2Int>();
                stack.Push(new Vector2Int(0, 0)); visited[0, 0] = true;
                var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
                while (stack.Count > 0)
                {
                    var cur = stack.Peek();
                    var options = new List<Vector2Int>();
                    foreach (var d in dirs)
                    {
                        var n = cur + d;
                        if (n.x >= 0 && n.y >= 0 && n.x < cols && n.y < rws && !visited[n.x, n.y]) options.Add(n);
                    }
                    if (options.Count == 0) { stack.Pop(); continue; }
                    var next = options[rng.Next(options.Count)];
                    visited[next.x, next.y] = true;
                    edges.Add(EdgeOf(cur, next));
                    stack.Push(next);
                }

                var extras = new List<(int, int, bool)>();
                for (int c = 0; c < cols; c++)
                    for (int r = 0; r < rws; r++)
                    {
                        if (c + 1 < cols && !edges.Contains((c, r, true))) extras.Add((c, r, true));
                        if (r + 1 < rws && !edges.Contains((c, r, false))) extras.Add((c, r, false));
                    }
                Shuffle(extras);
                for (int i = 0; i < 6 && i < extras.Count; i++) edges.Add(extras[i]);

                foreach (var e in edges) CarveDoor(e.Item1, e.Item2, e.Item3);
            }

            private static (int, int, bool) EdgeOf(Vector2Int a, Vector2Int b)
            {
                if (b.x != a.x) return (Mathf.Min(a.x, b.x), a.y, true);
                return (a.x, Mathf.Min(a.y, b.y), false);
            }

            private void CarveDoor(int c, int r, bool horizontal)
            {
                if (horizontal)
                {
                    var left = grid[c, r]; var right = grid[c + 1, r];
                    int x = wx[c + 1];
                    int ys = rng.Next(left.y0 + 1, left.y1 - 1);
                    for (int dy = 0; dy < 2; dy++)
                    {
                        wall[x, ys + dy] = false;
                        left.doorCells.Add(new Vector2Int(x - 1, ys + dy));
                        right.doorCells.Add(new Vector2Int(x + 1, ys + dy));
                        reserved[x - 1, ys + dy] = reserved[x - 2, ys + dy] = reserved[x + 1, ys + dy] = reserved[x + 2, ys + dy] = true;
                    }
                }
                else
                {
                    var low = grid[c, r]; var high = grid[c, r + 1];
                    int y = wy[r + 1];
                    int xs = rng.Next(low.x0 + 1, low.x1 - 1);
                    for (int dx = 0; dx < 2; dx++)
                    {
                        wall[xs + dx, y] = false;
                        low.doorCells.Add(new Vector2Int(xs + dx, y - 1));
                        high.doorCells.Add(new Vector2Int(xs + dx, y + 1));
                        reserved[xs + dx, y - 1] = reserved[xs + dx, y - 2] = reserved[xs + dx, y + 1] = reserved[xs + dx, y + 2] = true;
                    }
                }
            }

            private void AssignRoomTypes()
            {
                var pool = new List<RoomType>();
                void Add(RoomType t, int n) { for (int i = 0; i < n; i++) pool.Add(t); }
                Add(RoomType.Office, 5); Add(RoomType.Boss, 1); Add(RoomType.Lounge, 2); Add(RoomType.Storage, 3);
                Add(RoomType.Bathroom, 2); Add(RoomType.Break, 2); Add(RoomType.Print, 2); Add(RoomType.Library, 2);
                Shuffle(pool);

                var lobby = grid[2, 1];
                lobby.type = RoomType.Lobby;
                int k = 0;
                foreach (var r in rooms) if (r != lobby) r.type = pool[k++ % pool.Count];
            }

            // ---------- tilemaps ----------

            private void BuildTilemaps()
            {
                var stone = Load<TileBase>("Assets/TileSet/Rule Tiles/Floors/Floor-Stone_01.asset");
                var wood = Load<TileBase>("Assets/TileSet/Rule Tiles/Floors/Floor-Wood_01.asset");
                var metal = Load<TileBase>("Assets/TileSet/Rule Tiles/Floors/Floor-Metal_01.asset");
                var wallTile = Load<TileBase>("Assets/TileSet/Rule Tiles/Walls/Wall-Concrete_01.asset");

                var maze = new GameObject("Maze");
                maze.AddComponent<Grid>();

                var floorGo = new GameObject("Floor"); floorGo.transform.SetParent(maze.transform, false);
                var floor = floorGo.AddComponent<Tilemap>();
                floorGo.AddComponent<TilemapRenderer>().sortingOrder = -10;

                var wallsGo = new GameObject("Walls"); wallsGo.transform.SetParent(maze.transform, false);
                wallsGo.layer = obstacleLayer;
                var walls = wallsGo.AddComponent<Tilemap>();
                wallsGo.AddComponent<TilemapRenderer>().sortingOrder = 0;
                var body = wallsGo.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Static;
                var tc = wallsGo.AddComponent<TilemapCollider2D>();
                var cc = wallsGo.AddComponent<CompositeCollider2D>();
                tc.compositeOperation = Collider2D.CompositeOperation.Merge;
                cc.geometryType = CompositeCollider2D.GeometryType.Polygons;

                var fp = new List<Vector3Int>(); var ft = new List<TileBase>();
                var wp = new List<Vector3Int>(); var wt = new List<TileBase>();
                for (int x = 0; x < W; x++)
                    for (int y = 0; y < H; y++)
                    {
                        var cell = new Vector3Int(x + offX, y + offY, 0);
                        if (wall[x, y]) { wp.Add(cell); wt.Add(wallTile); continue; }
                        fp.Add(cell);
                        var room = roomAt[x, y];
                        ft.Add(room == null ? stone : FloorFor(room.type, stone, wood, metal));
                    }
                floor.SetTiles(fp.ToArray(), ft.ToArray());
                walls.SetTiles(wp.ToArray(), wt.ToArray());
                walls.CompressBounds();
                tc.ProcessTilemapChanges();
                cc.GenerateGeometry();
            }

            private static TileBase FloorFor(RoomType t, TileBase stone, TileBase wood, TileBase metal)
            {
                switch (t)
                {
                    case RoomType.Office: case RoomType.Boss: case RoomType.Library: case RoomType.Lounge: return wood;
                    case RoomType.Bathroom: return metal;
                    default: return stone;
                }
            }

            // ---------- furniture ----------

            private void Furnish(Room r)
            {
                switch (r.type)
                {
                    case RoomType.Lobby:
                        Wall(r, "Big-Sofa", 1); Wall(r, "Small-Sofa", 2); Wall(r, "Big-Plant", 2);
                        Table(r, "Small-Table", "Books", null, 1); Wall(r, "Vending-Machine", 1); Wall(r, "Water-Dispenser", 1);
                        Decor(r, "Wall-Clock", 1); Decor(r, "Board", 1); Loot(r, "Papers", 1);
                        break;
                    case RoomType.Office:
                        for (int i = 0, n = rng.Next(2, 4); i < n; i++) Table(r, rng.Next(2) == 0 ? "Desk" : "Desk-2", Pick("Papers", "Folders", "Folders-2", "Books"), "Chair", 1);
                        Wall(r, "Filing-Cabinet-Small", 2); Wall(r, "Big-Plant", 1); Free(r, "Small-Plant", "wall", 1);
                        if (rng.Next(2) == 0) Wall(r, "Printer-Furniture", 1);
                        Free(r, "Bin", "wall", 1);
                        Decor(r, "Board", 1); Decor(r, "Wall-Clock", 1); Decor(r, "Wall-Graph", 1); Decor(r, "Wall-Note", 1);
                        Loot(r, Pick("Papers", "Folders"), 2);
                        break;
                    case RoomType.Boss:
                        Table(r, "Boss-Desk", Pick("Papers", "Folders-2"), "Boss-Chair", 1);
                        Wall(r, "Big-Sofa-2", 1); Wall(r, "Tall-Bookshelf", 2); Wall(r, "Big-Plant", 2); Wall(r, "Filing-Cabinet-Tall", 1);
                        Decor(r, "Mirror", 1); Decor(r, "Wall-Shelf", 1); Decor(r, "Wall-Note-2", 1);
                        break;
                    case RoomType.Lounge:
                        Wall(r, "Big-Sofa", 2); Free(r, "Small-Sofa", "free", 2);
                        Table(r, "Big-Round-Table", "Books", null, 1); Table(r, "Small-Table", null, null, 2);
                        Wall(r, "Big-Plant", 2); Free(r, "Small-Plant", "wall", 1);
                        Decor(r, "Wall-Shelf", 1); Decor(r, "Wall-Clock", 1); Loot(r, "Books", 1);
                        break;
                    case RoomType.Storage:
                        Wall(r, "Wide-Filing-Cabinet", 3); Wall(r, "Filing-Cabinet-Open", 1); Wall(r, "Filing-Cabinet-Tall", 2);
                        Wall(r, "Big-Filing-Cabinet", 1); Wall(r, "Tall-Bookshelf", 1); Free(r, "Bin", "wall", 1);
                        Loot(r, Pick("Folders", "Papers"), 2); Decor(r, "Wall-Note", 1);
                        break;
                    case RoomType.Bathroom:
                        Free(r, "Toilet-Closed", "wallN", 2); Free(r, "Toilet-Open", "wallN", 1); Free(r, "WC-Sink", "wallN", 2);
                        Free(r, "WC-Paper", "wall", 1); Free(r, "Bin", "wall", 1); Decor(r, "Mirror", 2);
                        break;
                    case RoomType.Break:
                        Wall(r, "Vending-Machine", 1); Wall(r, "Water-Dispenser", 1);
                        Table(r, "Small-Table", "Coffee-Machine", null, 1); Table(r, "Big-Round-Table", null, "Chair-2", 1);
                        Free(r, "Chair", "free", 2); Free(r, "Bin", "wall", 1); Wall(r, "Big-Plant", 1); Decor(r, "Wall-Clock", 1);
                        break;
                    case RoomType.Print:
                        Wall(r, "Big-Office-Printer", 2); Wall(r, "Printer-Furniture", 1);
                        Table(r, "Desk", "Printer", "Chair", 1); Wall(r, "Filing-Cabinet-Small", 2);
                        Loot(r, "Papers", 3); Loot(r, "Folders", 1); Free(r, "Bin", "wall", 1);
                        break;
                    case RoomType.Library:
                        Wall(r, "Tall-Bookshelf", 3); Free(r, "Bookshelf", "wall", 4);
                        Table(r, "Small-Table", "Books", "Chair", 1); Wall(r, "Big-Plant", 1); Free(r, "Small-Plant", "wall", 1);
                        Decor(r, "Wall-Note", 1); Decor(r, "Wall-Shelf", 1); Loot(r, "Books", 1);
                        break;
                }
            }

            private string Pick(params string[] names) => names[rng.Next(names.Length)];

            private void Wall(Room r, string name, int count) { for (int i = 0; i < count; i++) TryPlace(r, name, "wall", out _); }

            private void Free(Room r, string name, string mode, int count) { for (int i = 0; i < count; i++) TryPlace(r, name, mode, out _); }

            /// <summary>Table-like furniture with an optional item on top and an optional chair next to it.</summary>
            private void Table(Room r, string name, string top, string chair, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    if (!TryPlace(r, name, "free", out var p)) continue;
                    if (top != null)
                    {
                        var cell = new Vector2Int(p.pos.x + rng.Next(p.w), p.pos.y + rng.Next(p.h));
                        Spawn(top, CellCenter(cell.x, cell.y, 1, 1), 6, false);
                    }
                    if (chair != null) TryPlaceAt(r, chair, new Vector2Int(p.pos.x + rng.Next(p.w), p.pos.y - 1));
                }
            }

            /// <summary>Flat wall decoration drawn on the wall tiles above the room (never on a door gap).</summary>
            private void Decor(Room r, string name, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        int x = rng.Next(r.x0, r.x1 + 1);
                        int y = r.y1 + 1;
                        if (!wall[x, y] || !wall[x - 1, y] || !wall[x + 1, y]) continue;
                        Spawn(name, CellCenter(x, y, 1, 1), 1, false);
                        break;
                    }
                }
            }

            /// <summary>Small item lying on the floor. Walkable, so it does not occupy the cell.</summary>
            private void Loot(Room r, string name, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    for (int attempt = 0; attempt < 30; attempt++)
                    {
                        int x = rng.Next(r.x0 + 1, r.x1), y = rng.Next(r.y0 + 1, r.y1);
                        if (occ[x, y] || reserved[x, y]) continue;
                        Spawn(name, CellCenter(x, y, 1, 1), 3, false);
                        break;
                    }
                }
            }

            private bool TryPlace(Room r, string name, string mode, out Placed placed)
            {
                placed = null;
                var spr = GetSprite(name);
                int w = Mathf.RoundToInt(spr.rect.width / spr.pixelsPerUnit), h = Mathf.RoundToInt(spr.rect.height / spr.pixelsPerUnit);
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    int x, y;
                    string m = mode;
                    if (m == "wall") m = new[] { "wallN", "wallS", "wallW", "wallE" }[rng.Next(4)];
                    switch (m)
                    {
                        case "wallN": y = r.y1 - h + 1; x = rng.Next(r.x0, r.x1 - w + 2); break;
                        case "wallS": y = r.y0; x = rng.Next(r.x0, r.x1 - w + 2); break;
                        case "wallW": x = r.x0; y = rng.Next(r.y0, r.y1 - h + 2); break;
                        case "wallE": x = r.x1 - w + 1; y = rng.Next(r.y0, r.y1 - h + 2); break;
                        default: x = rng.Next(r.x0 + 1, Mathf.Max(r.x0 + 2, r.x1 - w + 1)); y = rng.Next(r.y0 + 1, Mathf.Max(r.y0 + 2, r.y1 - h + 1)); break;
                    }
                    if (TryOccupy(r, x, y, w, h))
                    {
                        placed = new Placed { pos = new Vector2Int(x, y), w = w, h = h };
                        Spawn(name, CellCenter(x, y, w, h), 5, true, w, h);
                        return true;
                    }
                }
                return false;
            }

            private bool TryPlaceAt(Room r, string name, Vector2Int cell)
            {
                if (cell.x < r.x0 || cell.x > r.x1 || cell.y < r.y0 || cell.y > r.y1) return false;
                if (!TryOccupy(r, cell.x, cell.y, 1, 1)) return false;
                Spawn(name, CellCenter(cell.x, cell.y, 1, 1), 5, true, 1, 1);
                return true;
            }

            /// <summary>Marks the footprint as occupied if it is free and the room stays fully connected.</summary>
            private bool TryOccupy(Room r, int x, int y, int w, int h)
            {
                if (x < r.x0 || y < r.y0 || x + w - 1 > r.x1 || y + h - 1 > r.y1) return false;
                for (int dx = 0; dx < w; dx++)
                    for (int dy = 0; dy < h; dy++)
                        if (occ[x + dx, y + dy] || reserved[x + dx, y + dy]) return false;

                SetOcc(x, y, w, h, true);
                if (!Connected(r)) { SetOcc(x, y, w, h, false); return false; }
                return true;
            }

            private void SetOcc(int x, int y, int w, int h, bool v)
            {
                for (int dx = 0; dx < w; dx++) for (int dy = 0; dy < h; dy++) occ[x + dx, y + dy] = v;
            }

            private bool Connected(Room r)
            {
                int free = 0;
                for (int x = r.x0; x <= r.x1; x++) for (int y = r.y0; y <= r.y1; y++) if (!occ[x, y]) free++;
                if (free == 0 || r.doorCells.Count == 0) return false;

                var seen = new HashSet<Vector2Int>();
                var q = new Queue<Vector2Int>();
                var start = r.doorCells.Find(c => c.x >= r.x0 && c.x <= r.x1 && c.y >= r.y0 && c.y <= r.y1);
                if (occ[start.x, start.y]) return false;
                q.Enqueue(start); seen.Add(start);
                var d4 = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    foreach (var d in d4)
                    {
                        var n = c + d;
                        if (n.x < r.x0 || n.x > r.x1 || n.y < r.y0 || n.y > r.y1 || occ[n.x, n.y] || !seen.Add(n)) continue;
                        q.Enqueue(n);
                    }
                }
                return seen.Count == free;
            }

            private Vector3 CellCenter(int x, int y, int w, int h) => new Vector3(offX + x + w * 0.5f, offY + y + h * 0.5f, 0f);

            private void Spawn(string name, Vector3 center, int order, bool solidAllowed, int w = 1, int h = 1)
            {
                bool interactable = Interactable.Contains(name);
                var go = new GameObject(name);
                go.transform.SetParent(interactable ? interactableRoot : furnitureRoot, false);
                go.transform.position = center;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GetSprite(name);
                sr.sortingOrder = order;

                if (solidAllowed && !NonSolid.Contains(name))
                {
                    // Furniture blocks movement but not sight (the vision rays only look at the Obstacle layer).
                    go.layer = furnitureLayer;
                    var box = go.AddComponent<BoxCollider2D>();
                    var fit = OpaqueBounds(name);
                    box.size = fit.size * 0.9f;
                    box.offset = fit.center;
                    solidCount++;
                }

                if (interactable)
                {
                    var target = go.AddComponent<VisionTarget>();
                    var so = new SerializedObject(target);
                    so.FindProperty("persistence").enumValueIndex = (int)VisionTarget.PersistenceMode.RememberOnceSeen;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    interactableCount++;
                }
                else furnitureCount++;
            }

            // ---------- actors ----------

            private int SpawnEnemies()
            {
                var prefab = Load<GameObject>("Assets/Prefabs/TestEnemy.prefab");
                var root = new GameObject("Enemies");
                var candidates = new List<Vector2Int>();
                foreach (var room in rooms)
                {
                    if (room.type == RoomType.Lobby) continue;
                    for (int x = room.x0; x <= room.x1; x++)
                        for (int y = room.y0; y <= room.y1; y++)
                            if (!occ[x, y] && !reserved[x, y]) candidates.Add(new Vector2Int(x, y));
                }
                Shuffle(candidates);
                var used = new List<Room>();
                int n = 0;
                foreach (var c in candidates)
                {
                    var room = roomAt[c.x, c.y];
                    if (used.Contains(room)) continue;
                    used.Add(room);
                    var e = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    e.transform.SetParent(root.transform);
                    e.transform.position = CellCenter(c.x, c.y, 1, 1);
                    if (++n == 8) break;
                }
                return n;
            }

            private Vector3 PlacePlayerAndCamera()
            {
                var lobby = grid[2, 1];
                var c = lobby.Center;
                float best = float.MaxValue;
                for (int x = lobby.x0; x <= lobby.x1; x++)
                    for (int y = lobby.y0; y <= lobby.y1; y++)
                    {
                        if (occ[x, y]) continue;
                        float d = (new Vector2(x, y) - (Vector2)lobby.Center).sqrMagnitude;
                        if (d < best) { best = d; c = new Vector2Int(x, y); }
                    }
                var pos = CellCenter(c.x, c.y, 1, 1);

                var player = GameObject.Find("Player");
                if (player != null)
                {
                    player.transform.position = pos;
                    var rb = player.GetComponent<Rigidbody2D>();
                    if (rb != null) { rb.position = pos; rb.linearVelocity = Vector2.zero; }
                }
                var cam = Camera.main;
                if (cam != null) cam.transform.position = new Vector3(pos.x, pos.y, cam.transform.position.z);
                return pos;
            }

            // ---------- helpers ----------

            private Sprite GetSprite(string name)
            {
                if (!sprites.TryGetValue(name, out var s))
                {
                    s = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + name + ".png");
                    if (s == null) throw new System.Exception("Sprite not found: " + name);
                    sprites[name] = s;
                }
                return s;
            }

            private readonly Dictionary<string, Bounds> opaque = new Dictionary<string, Bounds>();

            /// <summary>Bounds (in sprite-local units, pivot at the centre) of the non-transparent pixels.</summary>
            private Bounds OpaqueBounds(string name)
            {
                if (opaque.TryGetValue(name, out var cached)) return cached;
                var spr = GetSprite(name);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(System.IO.File.ReadAllBytes(SpriteDir + name + ".png"));
                var px = tex.GetPixels32();
                int minX = tex.width, minY = tex.height, maxX = -1, maxY = -1;
                for (int y = 0; y < tex.height; y++)
                    for (int x = 0; x < tex.width; x++)
                        if (px[y * tex.width + x].a > 16)
                        {
                            if (x < minX) minX = x; if (x > maxX) maxX = x;
                            if (y < minY) minY = y; if (y > maxY) maxY = y;
                        }
                float ppu = spr.pixelsPerUnit;
                var b = new Bounds();
                if (maxX < 0) b = new Bounds(Vector3.zero, new Vector3(spr.rect.width / ppu, spr.rect.height / ppu, 0));
                else
                {
                    float cx = (minX + maxX + 1) * 0.5f - tex.width * 0.5f, cy = (minY + maxY + 1) * 0.5f - tex.height * 0.5f;
                    b = new Bounds(new Vector3(cx / ppu, cy / ppu, 0), new Vector3((maxX - minX + 1) / ppu, (maxY - minY + 1) / ppu, 0));
                }
                Object.DestroyImmediate(tex);
                opaque[name] = b;
                return b;
            }

            private static int EnsureLayer(string layerName)
            {
                int existing = LayerMask.NameToLayer(layerName);
                if (existing >= 0) return existing;
                var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                var layers = tm.FindProperty("layers");
                for (int i = 9; i < layers.arraySize; i++)
                {
                    var p = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(p.stringValue)) { p.stringValue = layerName; tm.ApplyModifiedProperties(); return i; }
                }
                throw new System.Exception("No free layer slot for " + layerName);
            }

            private static T Load<T>(string path) where T : Object
            {
                var o = AssetDatabase.LoadAssetAtPath<T>(path);
                if (o == null) throw new System.Exception("Asset not found: " + path);
                return o;
            }

            private void Shuffle<T>(IList<T> list)
            {
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (list[i], list[j]) = (list[j], list[i]);
                }
            }
        }
    }
}


