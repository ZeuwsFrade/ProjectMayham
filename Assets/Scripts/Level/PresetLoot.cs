using System;
using ProjectMayham.Items;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// Puts given things into a <see cref="LootContainer"/> when the scene starts, still to be found by searching,
    /// instead of what a raid rolls. For a level made by hand (the tutorial), where the player has to find a
    /// particular thing in a particular place.
    /// </summary>
    [RequireComponent(typeof(LootContainer))]
    public class PresetLoot : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public ItemDefinition item;
            [Min(1)] public int count;
        }

        [SerializeField] private Entry[] contents = Array.Empty<Entry>();

        private void Start()
        {
            var container = GetComponent<LootContainer>();
            container.Inventory.Clear();
            foreach (var entry in contents)
            {
                if (entry.item == null) continue;
                if (!container.Hide(entry.item, Mathf.Max(1, entry.count)))
                    Debug.LogWarning($"{name}: {entry.item.name} ×{entry.count} does not fit into the container.", this);
            }
        }
    }
}
