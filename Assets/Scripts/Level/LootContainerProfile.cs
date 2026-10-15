using System.Collections.Generic;
using ProjectMayham.Items;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// Settings of one kind of loot container (a bin, a cabinet, a crate): how it is called, how big its grid is,
    /// how long it takes to find a thing in it and what a raid puts into it. Shared by every piece of furniture
    /// of the kind, so the numbers are tuned in one place.
    /// </summary>
    [CreateAssetMenu(menuName = "Mayham/Loot Container Profile", fileName = "Container_New")]
    public class LootContainerProfile : ScriptableObject
    {
        [Tooltip("Name in the prompt and in the window: \"Обыскать: Шкаф\".")]
        [SerializeField] private string title = "Контейнер";
        [Tooltip("Columns and rows of the container's grid.")]
        [SerializeField] private Vector2Int gridSize = new Vector2Int(3, 3);
        [Tooltip("Seconds of searching it takes to find one thing.")]
        [SerializeField, Min(0.05f)] private float secondsPerItem = 1f;
        [Tooltip("Seconds of the first look into a container, before it shows what is in it or that it is empty.")]
        [SerializeField, Min(0f)] private float lookSeconds = 0.8f;

        [Header("Contents")]
        [SerializeField] private LootTable loot;
        [Tooltip("Share of the containers of this kind that hold nothing.")]
        [SerializeField, Range(0f, 1f)] private float emptyChance = 0.7f;
        [SerializeField, Min(1)] private int minStacks = 1;
        [SerializeField, Min(1)] private int maxStacks = 2;
        [Tooltip("Chance that a container with loot also gets one thing from the loot table of the room it stands in.")]
        [SerializeField, Range(0f, 1f)] private float roomLootChance = 0.3f;

        public string Title => title;
        public Vector2Int GridSize => new Vector2Int(Mathf.Max(1, gridSize.x), Mathf.Max(1, gridSize.y));
        public float SecondsPerItem => Mathf.Max(0.05f, secondsPerItem);
        public float LookSeconds => Mathf.Max(0f, lookSeconds);
        public LootTable Loot => loot;
        public float EmptyChance => emptyChance;

        /// <summary>True when one piece of the item fits into the grid, turned if it has to be.</summary>
        public bool Fits(ItemDefinition item)
        {
            if (item == null) return false;
            var grid = GridSize;
            var size = item.GridSize;
            if (size.x <= grid.x && size.y <= grid.y) return true;
            return item.CanRotate && size.y <= grid.x && size.x <= grid.y;
        }

        /// <summary>
        /// Decides what a container of this kind holds in one raid: nothing, or a few stacks from its own table and
        /// now and then one from the table of the room. The same generator state gives the same answer.
        /// </summary>
        public List<(ItemDefinition item, int count)> Roll(System.Random rng, LootTable roomLoot = null)
        {
            var result = new List<(ItemDefinition item, int count)>();
            if (rng.NextDouble() < emptyChance) return result;

            int min = Mathf.Max(1, minStacks);
            int stacks = rng.Next(min, Mathf.Max(min, maxStacks) + 1);
            for (int i = 0; i < stacks; i++) AddRoll(result, loot, rng);
            if (roomLoot != null && rng.NextDouble() < roomLootChance) AddRoll(result, roomLoot, rng);
            return result;
        }

        private static void AddRoll(List<(ItemDefinition item, int count)> result, LootTable table, System.Random rng)
        {
            if (table == null) return;
            var entry = table.Roll(rng);
            if (entry == null || entry.item == null) return;
            result.Add((entry.item, table.RollCount(entry, rng)));
        }
    }
}
