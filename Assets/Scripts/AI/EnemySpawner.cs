using System.Collections;
using System.Collections.Generic;
using ProjectMayham.Player;
using ProjectMayham.World;
using UnityEngine;

namespace ProjectMayham.AI
{
    /// <summary>
    /// Fills a raid with packs of enemies. The level is made anew for every raid, so the places cannot be set in the
    /// scene: the spawner waits for the <see cref="NavGrid"/>, then picks free spots that are far enough from the
    /// player and can be walked to from where they start, and puts a pack at each.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Tooltip("What a pack is made of. Each enemy is one of these, picked at random.")]
        [SerializeField] private GameObject[] prefabs;
        [SerializeField, Min(0)] private int packs = 4;
        [Tooltip("Smallest and largest number of enemies in a pack.")]
        [SerializeField] private Vector2Int packSize = new Vector2Int(2, 5);
        [Tooltip("How far from each other the members of a pack stand.")]
        [SerializeField, Min(0.5f)] private float spread = 2.2f;
        [Tooltip("No pack starts nearer to the player than this, in world units.")]
        [SerializeField, Min(0f)] private float minPlayerDistance = 22f;
        [Tooltip("Where the enemies are put in the hierarchy.")]
        [SerializeField] private Transform parent;

        private readonly List<Vector2> route = new List<Vector2>();

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
                Instantiate(prefab, spot, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)), parent);
                made++;
            }
            return made;
        }

        private static float Distance(Vector2 a, Vector2 b)
        {
            var wrap = WrapWorld.Active;
            return wrap != null ? wrap.Delta(a, b).magnitude : Vector2.Distance(a, b);
        }
    }
}
