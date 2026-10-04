using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Items;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectMayham.Level
{
    /// <summary>
    /// Fills the room slots of the corridor template with rooms and scatters loot. Runs when the scene starts, before
    /// <see cref="World.WrapWorld"/> copies the tilemaps around the seam, so every raid has a different set of rooms in
    /// the same places. The same seed always gives the same raid.
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

        [Header("Level")]
        [SerializeField] private RoomLibrary library;
        [SerializeField] private Tilemap floorMap;
        [SerializeField] private Tilemap wallMap;

        [Header("Corridor loot")]
        [SerializeField] private LootTable corridorLoot;
        [SerializeField, Min(0)] private int corridorLootCount = 10;
        [Tooltip("No loot within a few tiles of these points (player spawn, exit).")]
        [SerializeField] private Transform[] keepClear;

        [Header("Seed")]
        [Tooltip("Off: every raid gets a new random seed (printed to the console).")]
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int fixedSeed = 1;

        // Cells painted into the maze tilemaps, so a preview made in the editor can be taken back.
        [SerializeField, HideInInspector] private List<Vector3Int> paintedFloor = new List<Vector3Int>();
        [SerializeField, HideInInspector] private List<Vector3Int> paintedWalls = new List<Vector3Int>();

        private readonly List<PlacedRoom> rooms = new List<PlacedRoom>();

        public int Seed { get; private set; }
        public IReadOnlyList<PlacedRoom> Rooms => rooms;

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
            ScatterCorridorLoot(root.transform, slots, rng);

            string summary = string.Join(", ", rooms.Select(r => $"{r.Slot.name}={RoomSizes.Title(r.Kind)}"));
            Debug.Log($"Raid seed {seed}: {summary}");
        }

        /// <summary>Removes the rooms, props and loot of the current raid and restores the empty template.</summary>
        public void Clear()
        {
            foreach (var cell in paintedFloor) floorMap.SetTile(cell, null);
            foreach (var cell in paintedWalls) wallMap.SetTile(cell, null);
            paintedFloor.Clear();
            paintedWalls.Clear();
            rooms.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name != RootName) continue;
                Remove(child.gameObject);
            }
        }

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

            CopyTiles(instance.Floor, floorMap, origin, paintedFloor);
            CopyTiles(instance.Walls, wallMap, origin, paintedWalls);
            Remove(instance.Tiles.gameObject);

            RemoveDecorOnDoors(instance, slot);

            var placed = new PlacedRoom { Slot = slot, Kind = info.kind, Prefab = prefab, Instance = instance.gameObject };
            placed.LootCount = SpawnRoomLoot(instance, info, root, rng);
            return placed;
        }

        private static void CopyTiles(Tilemap source, Tilemap target, Vector2Int origin, List<Vector3Int> painted)
        {
            if (source == null) return;
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

        private static void RemoveDecorOnDoors(RoomTemplate room, RoomSlot slot)
        {
            if (room.Decor == null) return;
            for (int i = room.Decor.childCount - 1; i >= 0; i--)
            {
                var decor = room.Decor.GetChild(i);
                var cell = new Vector2Int(Mathf.FloorToInt(decor.position.x), Mathf.FloorToInt(decor.position.y));
                if (!slot.DoorTiles.Contains(cell)) continue;
                Remove(decor.gameObject);
            }
        }

        private int SpawnRoomLoot(RoomTemplate room, RoomLibrary.KindInfo info, Transform root, System.Random rng)
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

        private void ScatterCorridorLoot(Transform root, List<RoomSlot> slots, System.Random rng)
        {
            if (corridorLoot == null || corridorLootCount <= 0) return;

            var candidates = new List<Vector3Int>();
            foreach (var cell in floorMap.cellBounds.allPositionsWithin)
            {
                if (!floorMap.HasTile(cell) || wallMap.HasTile(cell)) continue;
                if (slots.Any(s => InsideSlot(s, cell))) continue;
                var center = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                if (keepClear != null && keepClear.Any(t => t != null && Vector2.Distance(t.position, center) < 3f)) continue;
                candidates.Add(cell);
            }

            for (int i = 0; i < corridorLootCount && candidates.Count > 0; i++)
            {
                int index = rng.Next(candidates.Count);
                var cell = candidates[index];
                candidates.RemoveAt(index);
                SpawnLoot(corridorLoot, new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f), root, rng);
            }
        }

        private static bool InsideSlot(RoomSlot slot, Vector3Int cell)
        {
            var origin = slot.InteriorOrigin;
            var size = slot.Interior;
            return cell.x >= origin.x && cell.x < origin.x + size.x && cell.y >= origin.y && cell.y < origin.y + size.y;
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
