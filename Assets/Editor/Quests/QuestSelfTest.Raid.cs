using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Items;
using ProjectMayham.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectMayham.EditorTools
{
    public static partial class QuestSelfTest
    {
        [MenuItem("Mayham/Quests/Check Raid Demands (open raid scene)")]
        public static void RunRaidCheckMenu() => Debug.Log(RunRaidCheck());

        /// <summary>
        /// Generates 30 raids in the open scene without demands, with empty ones and with "an item in the boss
        /// office and a visit to the parking", and checks what comes out. Needs a scene with a RaidGenerator
        /// (TileTest) outside Play mode. The preview is cleared afterwards and the scene is not saved.
        /// </summary>
        public static string RunRaidCheck()
        {
            if (EditorApplication.isPlaying) return "FAIL: leave Play mode first";
            var generator = Object.FindFirstObjectByType<RaidGenerator>();
            if (generator == null) return "FAIL: the open scene has no RaidGenerator (open Assets/Scenes/Tests/TileTest.unity)";

            var item = Item("Papers");
            var library = AssetDatabase.LoadAssetAtPath<RoomLibrary>(RoomFactory.LibraryPath);
            int slots = generator.GetComponentsInChildren<RoomSlot>().Length;
            var t = new Checks { Group = "raid" };
            try
            {
                t.Ok(library.CanPlace(RoomKind.Parking, RoomSize.Large), "a parking can stand in a large slot");
                t.Ok(!library.CanPlace(RoomKind.Parking, RoomSize.Small), "and not in a small one");

                for (int n = 1; n <= 30; n++)
                {
                    int seed = n * 7919 + 13;
                    generator.Generate(seed);
                    string plain = Describe(generator);
                    t.Eq(generator.QuestPickups.Count, 0, $"seed {seed}: no quest pickups without demands");
                    t.Ok(generator.Rooms.All(r => !r.Forced), $"seed {seed}: no room is forced without demands");
                    generator.Generate(seed, new RaidDemands());
                    t.Eq(Describe(generator), plain, $"seed {seed}: empty demands change nothing");

                    var demands = new RaidDemands();
                    demands.AddItem(item, 1, RoomKind.BossOffice);
                    demands.AddVisit(RoomKind.Parking);
                    generator.Generate(seed, demands);
                    string forced = Describe(generator);
                    var office = generator.Rooms.FirstOrDefault(r => r.Forced && r.Kind == RoomKind.BossOffice);
                    t.Ok(office != null, $"seed {seed}: a boss office is forced");
                    t.Ok(generator.Rooms.Any(r => r.Forced && r.Kind == RoomKind.Parking), $"seed {seed}: a parking is forced");
                    t.Eq(generator.Rooms.Count(r => r.Forced), 2, $"seed {seed}: two rooms are forced");
                    t.Eq(generator.Rooms.Count, slots, $"seed {seed}: every slot has a room");
                    t.Eq(generator.MissingKinds.Count, 0, $"seed {seed}: nothing is missing");
                    t.Eq(generator.QuestPickups.Count, 1, $"seed {seed}: one quest pickup");
                    if (office != null && generator.QuestPickups.Count == 1)
                    {
                        var pickup = generator.QuestPickups[0].GetComponent<ItemPickup>();
                        t.Ok(pickup != null && pickup.Item == item && pickup.Count == 1, $"seed {seed}: it is the item that was asked for");
                        t.Ok(generator.QuestPickups[0].transform.IsChildOf(office.Instance.transform), $"seed {seed}: it lies in the forced boss office");
                    }
                    t.Eq(CutOffTiles(generator), 0, $"seed {seed}: every walkable tile is reachable");

                    generator.Generate(seed, demands);
                    t.Eq(Describe(generator), forced, $"seed {seed}: same seed and demands, same raid");
                }
            }
            finally
            {
                generator.Clear();
            }

            return t.Failures.Count == 0
                ? $"PASS: {t.Count} checks"
                : $"FAIL: {t.Failures.Count} of {t.Count} checks\n - " + string.Join("\n - ", t.Failures.Take(20));
        }

        private static string Describe(RaidGenerator generator) =>
            string.Join(", ", generator.Rooms.Select(r => $"{r.Slot.name}={r.Prefab.name}:{r.LootCount}"))
            + " | holes " + string.Join(";", generator.Holes.Select(h => $"{h.transform.position.x},{h.transform.position.y}"));

        // Walkable tiles that cannot be reached from the tile at world (0, 0), where the player starts.
        // Barricades and holes do not count as obstacles: the player gets past them.
        private static int CutOffTiles(RaidGenerator generator)
        {
            var maze = generator.transform;
            var floor = maze.Find("Floor").GetComponent<Tilemap>();
            var walls = maze.Find("Walls").GetComponent<Tilemap>();
            var windowsRoot = maze.Find("Windows");
            var windows = windowsRoot != null ? windowsRoot.GetComponent<Tilemap>() : null;
            var so = new SerializedObject(generator);
            Vector2Int origin = so.FindProperty("gridOrigin").vector2IntValue;
            int tiles = so.FindProperty("cells").intValue * RoomSizes.CellTiles;

            var solid = new bool[tiles, tiles];
            for (int x = 0; x < tiles; x++)
            {
                for (int y = 0; y < tiles; y++)
                {
                    var cell = new Vector3Int(origin.x + x, origin.y + y, 0);
                    solid[x, y] = !floor.HasTile(cell) || walls.HasTile(cell) || (windows != null && windows.HasTile(cell));
                }
            }

            int furniture = LayerMask.NameToLayer("Furniture");
            foreach (var box in maze.GetComponentsInChildren<BoxCollider2D>())
            {
                if (box.isTrigger || box.gameObject.layer != furniture) continue;
                if (box.GetComponentInParent<Barricade>() != null || box.GetComponentInParent<WallHole>() != null) continue;

                Vector2 center = box.transform.TransformPoint(box.offset);
                Vector2 half = Vector2.Scale(box.size, box.transform.lossyScale) * 0.5f;
                float hx = Mathf.Abs(half.x), hy = Mathf.Abs(half.y);
                for (int x = Mathf.FloorToInt(center.x - hx + 0.05f); x <= Mathf.CeilToInt(center.x + hx - 0.05f) - 1; x++)
                {
                    for (int y = Mathf.FloorToInt(center.y - hy + 0.05f); y <= Mathf.CeilToInt(center.y + hy - 0.05f) - 1; y++)
                    {
                        int tx = x - origin.x, ty = y - origin.y;
                        if (tx >= 0 && ty >= 0 && tx < tiles && ty < tiles) solid[tx, ty] = true;
                    }
                }
            }

            int walkable = 0;
            foreach (bool blocked in solid)
            {
                if (!blocked) walkable++;
            }

            var start = new Vector2Int(-origin.x, -origin.y);
            if (start.x < 0 || start.y < 0 || start.x >= tiles || start.y >= tiles || solid[start.x, start.y]) return walkable;

            var seen = new bool[tiles, tiles];
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            seen[start.x, start.y] = true;
            int reached = 0;
            var steps = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                var tile = queue.Dequeue();
                reached++;
                foreach (var step in steps)
                {
                    int nx = ((tile.x + step.x) % tiles + tiles) % tiles, ny = ((tile.y + step.y) % tiles + tiles) % tiles;
                    if (solid[nx, ny] || seen[nx, ny]) continue;
                    seen[nx, ny] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return walkable - reached;
        }
    }
}
