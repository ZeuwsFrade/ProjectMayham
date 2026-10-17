using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMayham.Npcs
{
    /// <summary>Every NPC of the project, from Resources/Npcs, found by id.</summary>
    public static class NpcCatalog
    {
        public const string ResourceFolder = "Npcs";

        private static List<NpcDefinition> all;

        public static IReadOnlyList<NpcDefinition> All
        {
            get
            {
                if (all == null) all = Resources.LoadAll<NpcDefinition>(ResourceFolder).Where(n => n != null).ToList();
                return all;
            }
        }

        /// <summary>The NPC with this id, or null.</summary>
        public static NpcDefinition Find(string id) =>
            string.IsNullOrEmpty(id) ? null : All.FirstOrDefault(n => n.Id == id);

        /// <summary>The NPC who is carried as this item, or null.</summary>
        public static NpcDefinition ByCarryItem(Items.ItemDefinition item) =>
            item == null ? null : All.FirstOrDefault(n => n.CarryItem == item);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() => all = null;

        /// <summary>For the self-tests: NPCs that exist only in memory instead of the assets. <see cref="Reset"/> undoes it.</summary>
        public static void Override(IEnumerable<NpcDefinition> npcs) => all = npcs.Where(n => n != null).ToList();
    }
}
