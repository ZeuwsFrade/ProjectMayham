using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Combat;
using ProjectMayham.Core;
using ProjectMayham.Level;
using ProjectMayham.Localization;
using ProjectMayham.Player;
using ProjectMayham.UI;
using ProjectMayham.World;
using UnityEngine;

namespace ProjectMayham.AI
{
    public partial class EnemySpawner
    {
        [Header("Raid pressure")]
        [SerializeField] private Vector3Int initialByZone = new Vector3Int(4, 16, 32);
        [SerializeField, Min(1f)] private float reinforcementInterval = 300f;
        [SerializeField, Min(1.1f)] private float waveGrowth = 1.5f;
        [SerializeField, Min(1)] private int populationCap = 170;

        private readonly List<Health> raidEnemies = new List<Health>();
        private GameObject[] common;
        private GameObject[] heavies;
        private Camera raidCamera;
        private Vector2[][] spawnPoints;

        public int Wave { get; private set; }
        public Vector3Int LastWaveSpawned { get; private set; }

        private IEnumerator PopulateRaid(NavGrid grid, RaidGenerator raid, PlayerStats player)
        {
            while (SceneLoader.IsLoading && player != null) yield return null;
            if (!MaySpawn(player)) yield break;

            common = prefabs?.Where(prefab => prefab != null).ToArray() ?? System.Array.Empty<GameObject>();
            heavies = loners?.Where(loner => loner != null && loner.prefab != null).Select(loner => loner.prefab).ToArray()
                ?? System.Array.Empty<GameObject>();
            raidCamera = Camera.main;
            if (common.Length == 0) yield break;
            var zones = raid.CorridorSpawnPoints().ToLookup(point => raid.ZoneAt(point));
            spawnPoints = new[] { zones[RaidZone.Entry].ToArray(), zones[RaidZone.Risk].ToArray(), zones[RaidZone.Deep].ToArray() };

            float elapsed = 0f;
            var initial = Allocation(0);
            using (var placement = SpawnWave(grid, raid, player, initial, true))
            {
                while (MaySpawn(player) && placement.MoveNext())
                {
                    yield return null;
                    elapsed += ActiveSeconds();
                }
            }
            while (MaySpawn(player))
            {
                yield return null;
                elapsed += ActiveSeconds();
                if (elapsed < (Wave + 1) * reinforcementInterval || Time.timeScale <= 0f || !MaySpawn(player)) continue;

                Wave++;
                using var placement = SpawnWave(grid, raid, player, Allocation(Wave), false);
                while (MaySpawn(player) && placement.MoveNext())
                {
                    yield return null;
                    elapsed += ActiveSeconds();
                }
                if (MaySpawn(player) && LastWaveSpawned != Vector3Int.zero)
                    ScreenNote.Show(GameText.Format("Волна {0} · в лабиринт пришло подкрепление", Wave), 4f);
            }
        }

        private static float ActiveSeconds() => Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;

        private static bool MaySpawn(PlayerStats player) => player != null && ((IDamageable)player).IsAlive && !SceneLoader.IsLoading;

        private Vector3Int Allocation(int wave)
        {
            raidEnemies.RemoveAll(enemy => enemy == null || !enemy.IsAlive);
            int available = Mathf.Max(0, populationCap - raidEnemies.Count);
            var population = Vector3Int.Max(initialByZone, Vector3Int.zero);
            int total = population.x + population.y + population.z;
            if (total == 0 || available == 0) return Vector3Int.zero;
            float desired = WaveBudget(total, wave, waveGrowth, available);
            int entry = Mathf.FloorToInt(desired * population.x / total);
            int risk = Mathf.FloorToInt(desired * (population.x + population.y) / total);
            return new Vector3Int(entry, risk - entry, (int)desired - risk);
        }

        public static int WaveBudget(int initial, int wave, float growth, int available)
        {
            if (wave == 0) return Mathf.Min(initial, available);
            float increase = initial * (growth - 1f) * Mathf.Pow(growth, wave - 1);
            return increase >= available ? available : Mathf.CeilToInt(increase);
        }

        private IEnumerator<object> SpawnWave(NavGrid grid, RaidGenerator raid, PlayerStats player, Vector3Int targets, bool initial)
        {
            LastWaveSpawned = Vector3Int.zero;
            if (initial && targets.z > 0 && loners != null)
            {
                var guard = loners.FirstOrDefault(loner => loner != null && loner.prefab != null && loner.haunt == Haunt.Doorways);
                if (guard != null)
                {
                    foreach (var room in raid.Rooms.Where(room => room.Zone == RaidZone.Deep).Take(targets.z / 8))
                    {
                        while (Time.timeScale <= 0f) yield return null;
                        var posts = Posts(raid, room, grid);
                        if (posts.Count == 0 || !SafeSpawn(posts[0], player.transform.position, raid)) continue;
                        var actor = SpawnRaidEnemy(guard.prefab, posts[0]);
                        if (actor.TryGetComponent<EnemyAI>(out var ai)) ai.SetPatrolRoute(posts);
                        LastWaveSpawned += new Vector3Int(0, 0, 1);
                        yield return null;
                    }
                }
            }

            for (int zoneIndex = 0; zoneIndex < 3; zoneIndex++)
            {
                var zone = (RaidZone)zoneIndex;
                var points = spawnPoints[zoneIndex];
                if (points.Length == 0) continue;
                int groupSize = zone == RaidZone.Entry ? 1 : zone == RaidZone.Risk ? 3 : 4;
                int budget = targets[zoneIndex];
                for (int attempt = 0; attempt < budget * 12 && LastWaveSpawned[zoneIndex] < budget; attempt++)
                {
                    yield return null;
                    while (Time.timeScale <= 0f) yield return null;
                    if (!MaySpawn(player)) yield break;

                    Vector2 position = player.transform.position;
                    if (!TryRaidCenter(grid, raid, points, position, initial, out var center)) continue;

                    int wanted = Mathf.Min(groupSize, budget - LastWaveSpawned[zoneIndex]);
                    for (int member = 0; member < wanted; member++)
                    {
                        var point = center;
                        if (member > 0 && !TryCompanion(grid, raid, zone, center, position, out point)) continue;
                        var pool = heavies.Length > 0 && member == wanted - 1 &&
                            (zone == RaidZone.Deep && wanted > 1 || zone == RaidZone.Risk && wanted > 1 && Random.value < 0.45f)
                            ? heavies : common;
                        SpawnRaidEnemy(pool[Random.Range(0, pool.Length)], point);
                        var tally = LastWaveSpawned;
                        tally[zoneIndex]++;
                        LastWaveSpawned = tally;
                    }
                }
            }
            Debug.Log($"Raid wave {Wave}: {LastWaveSpawned} spawned; requested {targets}.", this);
        }

        private bool TryRaidCenter(NavGrid grid, RaidGenerator raid, Vector2[] points, Vector2 player, bool initial, out Vector2 center)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                center = points[Random.Range(0, points.Length)];
                if (initial && Distance(center, raid.SpawnPoint) < minPlayerDistance) continue;
                if (grid.IsWalkable(center) && SafeSpawn(center, player, raid)) return true;
            }
            center = default;
            return false;
        }

        private bool TryCompanion(NavGrid grid, RaidGenerator raid, RaidZone zone, Vector2 center, Vector2 player, out Vector2 point)
        {
            for (int attempt = 0; attempt < 8; attempt++)
                if (grid.TryRandomPoint(center, spread, out point) && raid.ZoneAt(point) == zone &&
                    SafeSpawn(point, player, raid) && !Physics2D.Linecast(center, point, grid.ObstacleMask)) return true;
            point = default;
            return false;
        }

        private bool SafeSpawn(Vector2 point, Vector2 player, RaidGenerator raid)
        {
            if (Distance(player, point) < minPlayerDistance) return false;
            if (raid.Exit != null && Distance(raid.Exit.transform.position, point) < 8f) return false;
            if (raidCamera != null)
            {
                var wrap = WrapWorld.Active;
                var image = wrap != null ? wrap.NearestImage(point, player) : point;
                var view = raidCamera.WorldToViewportPoint(image);
                if (view.z > 0f && view.x > -0.08f && view.x < 1.08f && view.y > -0.08f && view.y < 1.08f) return false;
            }
            foreach (var enemy in raidEnemies)
                if (enemy != null && enemy.IsAlive && Distance(enemy.transform.position, point) < 1.1f) return false;
            return true;
        }

        private GameObject SpawnRaidEnemy(GameObject prefab, Vector2 point)
        {
            var enemy = Spawn(prefab, point);
            if (enemy.TryGetComponent<Health>(out var health)) raidEnemies.Add(health);
            return enemy;
        }
    }
}
