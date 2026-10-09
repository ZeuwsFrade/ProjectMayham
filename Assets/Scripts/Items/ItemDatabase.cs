using System.Collections.Generic;
using UnityEngine;

namespace ProjectMayham.Items
{
    /// <summary>
    /// Every item of the game, so save files can refer to items by id. Lives in Resources/ItemDatabase;
    /// refresh it with Mayham/Refresh Item Database after adding an item.
    /// </summary>
    [CreateAssetMenu(menuName = "Mayham/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        public const string ResourceName = "ItemDatabase";

        [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();

        private static ItemDatabase instance;
        private Dictionary<string, ItemDefinition> byId;

        public IReadOnlyList<ItemDefinition> Items => items;

        public static ItemDatabase Instance => instance != null ? instance : instance = Resources.Load<ItemDatabase>(ResourceName);

        /// <summary>The item with this id, or null.</summary>
        public static ItemDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var db = Instance;
            if (db == null)
            {
                Debug.LogError("ItemDatabase is missing: create Resources/ItemDatabase (Mayham/Refresh Item Database).");
                return null;
            }
            return db.Lookup(id);
        }

        private ItemDefinition Lookup(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, ItemDefinition>();
                foreach (var item in items)
                {
                    if (item != null) byId[item.Id] = item;
                }
            }
            return byId.TryGetValue(id, out var found) ? found : null;
        }

        public void SetItems(IEnumerable<ItemDefinition> all)
        {
            items = new List<ItemDefinition>(all);
            byId = null;
        }

        private void OnValidate() => byId = null;
    }
}
