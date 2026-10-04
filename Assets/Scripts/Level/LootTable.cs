using System;
using System.Collections.Generic;
using ProjectMayham.Items;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>Weighted list of items that can lie in a room of one kind (or in the corridors).</summary>
    [CreateAssetMenu(menuName = "Mayham/Loot Table", fileName = "NewLootTable")]
    public class LootTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public ItemDefinition item;
            [Min(0f)] public float weight = 1f;
            [Min(1)] public int minCount = 1;
            [Min(1)] public int maxCount = 1;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>Picks an entry by weight, or null when the table is empty.</summary>
        public Entry Roll(System.Random rng)
        {
            float total = 0f;
            foreach (var entry in entries)
            {
                if (entry.item != null && entry.weight > 0f) total += entry.weight;
            }
            if (total <= 0f) return null;

            double pick = rng.NextDouble() * total;
            foreach (var entry in entries)
            {
                if (entry.item == null || entry.weight <= 0f) continue;
                pick -= entry.weight;
                if (pick <= 0.0) return entry;
            }
            return entries[entries.Count - 1];
        }

        public int RollCount(Entry entry, System.Random rng)
        {
            int min = Mathf.Max(1, entry.minCount);
            int max = Mathf.Max(min, entry.maxCount);
            return rng.Next(min, max + 1);
        }
    }
}
