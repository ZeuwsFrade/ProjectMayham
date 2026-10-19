using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Items;
using UnityEngine;

namespace ProjectMayham.Shelter
{
    /// <summary>Every module of the shelter, from Resources/Modules, found by id or by its blueprint item.</summary>
    public static class ModuleCatalog
    {
        public const string ResourceFolder = "Modules";

        private static List<ShelterModuleDefinition> all;

        public static IReadOnlyList<ShelterModuleDefinition> All
        {
            get
            {
                if (all == null)
                {
                    all = Resources.LoadAll<ShelterModuleDefinition>(ResourceFolder)
                        .Where(m => m != null).OrderBy(m => m.Id, System.StringComparer.Ordinal).ToList();
                }
                return all;
            }
        }

        /// <summary>The module with this id, or null.</summary>
        public static ShelterModuleDefinition Find(string id) =>
            string.IsNullOrEmpty(id) ? null : All.FirstOrDefault(m => m.Id == id);

        /// <summary>The module this item is the blueprint of, or null.</summary>
        public static ShelterModuleDefinition ByBlueprintItem(ItemDefinition item) =>
            item == null ? null : All.FirstOrDefault(m => m.BlueprintItem == item);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() => all = null;

        /// <summary>For the self-tests: modules that exist only in memory instead of the assets. <see cref="Reset"/> undoes it.</summary>
        public static void Override(IEnumerable<ShelterModuleDefinition> modules) =>
            all = modules.Where(m => m != null).OrderBy(m => m.Id, System.StringComparer.Ordinal).ToList();
    }
}
