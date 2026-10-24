using System;
using System.Collections;
using System.Collections.Generic;
using ProjectMayham.Level;
using ProjectMayham.Player;
using ProjectMayham.World;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectMayham.AI
{
    /// <summary>
    /// Fills a raid with enemies: packs of the common kind, and lone ones of the kinds that live by themselves (rams in
    /// the corridors, Justice at the doors of a room). The level is made anew for every raid, so the places cannot be
    /// set in the scene: the spawner waits for the <see cref="NavGrid"/>, then picks free spots that are far enough from
    /// the player and can be walked to from where they start.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        /// <summary>Where a lone enemy lives.</summary>
        public enum Haunt
        {
            /// <summary>Corridor floor, mostly near the offices.</summary>
            Corridor,
            /// <summary>Inside a room, walking between its doors.</summary>
            Doorways
        }

        /// <summary>A kind of enemy that is put alone, not in a pack.</summary>
        [Serializable]
        public class Loner
        {
            public GameObject prefab;
            [Tooltip("Smallest and largest number of them in a raid.")]
            public Vector2Int count = new Vector2Int(1, 2);
            public Haunt haunt;
        }

        // A corridor spot this near to the door of an office counts as "between the offices".
        private const float NearOffice = 14f;
        // How much more often a corridor loner stands near an office than anywhere else.
        private const float OfficeShare = 0.75f;
        // A guard stands this far inside the room from a door, out of the doorway itself.
        private const float DoorInset = 1.6f;
        // With one door only, its second point is this far into the room.
        private const float SingleDoorDepth = 6f;

        [Tooltip("What a pack is made of. Each enemy is one of these, picked at random.")]
        [SerializeField] private GameObject[] prefabs;
        [SerializeField, Min(0)] private int packs = 4;
        [Tooltip("Smallest and largest number of enemies in a pack.")]
        [SerializeField] private Vector2Int packSize = new Vector2Int(2, 5);
        [Tooltip("How far from each other the members of a pack stand.")]
        [SerializeField, Min(0.5f)] private float spread = 2.2f;
        [Tooltip("Kinds that are put one by one.")]
        [SerializeField] private Loner[] loners;
        [Tooltip("No enemy starts nearer to the player than this, in world units.")]
        [SerializeField, Min(0f)] private float minPlayerDistance = 22f;
        [Tooltip("Where the enemies are put in the hierarchy.")]
        [SerializeField] private Transform parent;

        private readonly List<Vector2> route = new List<Vector2>();
        private readonly HashSet<RaidGenerator.PlacedRoom> guarded = new HashSet<RaidGenerator.PlacedRoom>();
        private List<Vector2> officeDoors;

        public IReadOnlyList<Loner> Loners => loners;

        private IEnumerator Start()
        {
            var grid = NavGrid.Active;
            while (grid == null || !grid.IsReady)
            {
                grid = NavGrid.Active;
                yield return null;
            }
            // One more frame: the rooms of the raid and their furniture have their colliders by now.
            yield return null;

            var player = FindFirstObjectByType<PlayerStats>();
            Vector2 start = player != null ? (Vector2)player.transform.position : Vector2.zero;
            var raid = FindFirstObjectByType<RaidGenerator>();

            int placed = 0;
            for (int attempt = 0; attempt < packs * 40 && placed < packs; attempt++)
            {
                // A search a frame: placing the packs does not take the budget the enemies already walking need.
                yield return null;
                if (!TryPackCenter(grid, start, out var center)) continue;

                int count = Random.Range(packSize.x, packSize.y + 1);
                if (SpawnPack(center, count) > 0) placed++;
            }
            Debug.Log($"EnemySpawner: {placed} of {packs} packs placed.");

            if (loners == null) yield break;
            foreach (var loner in loners)
            {
                if (loner == null || loner.prefab == null) continue;
                int wanted = Random.Range(loner.count.x, loner.count.y + 1), made = 0;
                for (int attempt = 0; attempt < wanted * 40 && made < wanted; attempt++)
                {
                    yield return null;
                    if (TryPlaceLoner(loner, grid, raid, start)) made++;
                }
                Debug.Log($"EnemySpawner: {made} of {wanted} {loner.prefab.name} placed ({loner.haunt}).");
            }
        }

        private bool TryPackCenter(NavGrid grid, Vector2 playerPosition, out Vector2 center)
        {
            if (!grid.TryRandomPoint(out center)) return false;
            if (Distance(playerPosition, center) < minPlayerDistance) return false;
            // A pack in a sealed room would never find anyone.
            return grid.HasSearchBudget && grid.TryFindPath(playerPosition, center, route);
        }

        /// <summary>Puts a pack of up to <paramref name="count"/> enemies around a point. Returns how many were made.</summary>
        public int SpawnPack(Vector2 center, int count)
        {
            if (prefabs == null || prefabs.Length == 0) return 0;

            var grid = NavGrid.Active;
            int made = 0;
            for (int i = 0; i < count; i++)
            {
                Vector2 spot = center;
                if (grid != null && grid.IsReady && !grid.TryRandomPoint(center, spread, out spot)) continue;

                var prefab = prefabs[Random.Range(0, prefabs.Length)];
                if (prefab == null) continue;
                Spawn(prefab, spot);
                made++;
            }
            return made;
        }

        /// <summary>Puts one enemy on free ground near a point. Null when there is none.</summary>
        public GameObject SpawnOne(GameObject prefab, Vector2 near)
        {
            if (prefab == null) return null;
            var grid = NavGrid.Active;
            Vector2 spot = near;
            if (grid != null && grid.IsReady && !grid.TryRandomPoint(near, 1.5f, out spot)) return null;
            return Spawn(prefab, spot);
        }

        private GameObject Spawn(GameObject prefab, Vector2 spot) =>
            Instantiate(prefab, spot, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)), parent);

        // ---- loners ----

        private bool TryPlaceLoner(Loner loner, NavGrid grid, RaidGenerator raid, Vector2 playerPosition)
        {
            // Without rooms (a test arena) every kind just stands somewhere far off.
            if (loner.haunt == Haunt.Doorways && raid != null && raid.Rooms.Count > 0)
                return TryPlaceGuard(loner.prefab, grid, raid, playerPosition);
            return TryPlaceInCorridor(loner.prefab, grid, raid, playerPosition);
        }

        private bool TryPlaceInCorridor(GameObject prefab, NavGrid grid, RaidGenerator raid, Vector2 playerPosition)
        {
            bool nearOffice = raid != null && Random.value < OfficeShare && OfficeDoors(raid).Count > 0;
            for (int i = 0; i < 12; i++)
            {
                if (!grid.TryRandomPoint(out var spot)) continue;
                if (Distance(playerPosition, spot) < minPlayerDistance) continue;
                if (raid != null && raid.IsInsideRoom(spot)) continue;
                if (nearOffice && NearestOfficeDoor(raid, spot) > NearOffice) continue;
                if (!grid.HasSearchBudget || !grid.TryFindPath(playerPosition, spot, route)) return false;

                Spawn(prefab, spot);
                return true;
            }
            return false;
        }

        private bool TryPlaceGuard(GameObject prefab, NavGrid grid, RaidGenerator raid, Vector2 playerPosition)
        {
            var room = PickRoom(raid, playerPosition);
            if (room == null) return false;

            var posts = Posts(raid, room, grid);
            if (posts.Count == 0) return false;
            if (!grid.HasSearchBudget || !grid.TryFindPath(playerPosition, posts[0], route)) return false;

            guarded.Add(room);
            var enemy = Spawn(prefab, posts[0]);
            var ai = enemy.GetComponent<EnemyAI>();
            if (ai != null) ai.SetPatrolRoute(posts);
            return true;
        }

        // Offices and the parking lot are where Justice keeps watch most of all; other rooms now and then.
        private RaidGenerator.PlacedRoom PickRoom(RaidGenerator raid, Vector2 playerPosition)
        {
            RaidGenerator.PlacedRoom best = null;
            float total = 0f;
            foreach (var room in raid.Rooms)
            {
                if (room == null || room.Slot == null || guarded.Contains(room)) continue;
                if (Distance(playerPosition, RoomCenter(room)) < minPlayerDistance) continue;

                float weight = room.Kind == RoomKind.Office || room.Kind == RoomKind.Parking ? 3f : 1f;
                total += weight;
                if (Random.value * total < weight) best = room;
            }
            return best;
        }

        /// <summary>The points a guard walks between: just inside each door, or one door and a point deeper in.</summary>
        private static List<Vector2> Posts(RaidGenerator raid, RaidGenerator.PlacedRoom room, NavGrid grid)
        {
            var posts = new List<Vector2>();
            Vector2 inward = Vector2.zero;
            foreach (var door in raid.DoorsOf(room))
            {
                Vector2 post = door.Position + door.Inward * DoorInset;
                if (!grid.IsWalkable(post)) continue;
                posts.Add(post);
                inward = door.Inward;
            }
            if (posts.Count == 1 && grid.TryRandomPoint(posts[0] + inward * SingleDoorDepth, 2f, out var deeper)) posts.Add(deeper);

            // Round the room from door to door, not back and forth across it.
            Vector2 center = RoomCenter(room);
            posts.Sort((a, b) => Mathf.Atan2(a.y - center.y, a.x - center.x).CompareTo(Mathf.Atan2(b.y - center.y, b.x - center.x)));
            return posts;
        }

        private List<Vector2> OfficeDoors(RaidGenerator raid)
        {
            if (officeDoors != null) return officeDoors;
            officeDoors = new List<Vector2>();
            foreach (var room in raid.Rooms)
            {
                if (room == null || (room.Kind != RoomKind.Office && room.Kind != RoomKind.BossOffice)) continue;
                foreach (var door in raid.DoorsOf(room)) officeDoors.Add(door.Position);
            }
            return officeDoors;
        }

        private float NearestOfficeDoor(RaidGenerator raid, Vector2 point)
        {
            float nearest = float.MaxValue;
            foreach (var door in OfficeDoors(raid)) nearest = Mathf.Min(nearest, Distance(point, door));
            return nearest;
        }

        private static Vector2 RoomCenter(RaidGenerator.PlacedRoom room) =>
            room.Slot.InteriorOrigin + (Vector2)room.Slot.Interior * 0.5f;

        private static float Distance(Vector2 a, Vector2 b)
        {
            var wrap = WrapWorld.Active;
            return wrap != null ? wrap.Delta(a, b).magnitude : Vector2.Distance(a, b);
        }
    }
}
