using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.Trade;
using ProjectMayham.UI;
using UnityEngine;

namespace ProjectMayham.Shelter
{
    /// <summary>
    /// An additional module of the shelter (a paper press now; a workbench, a coffee corner later): data plus a
    /// prefab that appears on a free <see cref="ShelterModuleSlot"/> once it is built. Building needs the module's
    /// blueprint (<see cref="Blueprints"/>) and takes its cost out of the backpack and the stash. The assets live in
    /// Resources/Modules, where <see cref="ModuleCatalog"/> finds them.
    /// </summary>
    [CreateAssetMenu(menuName = "Mayham/Shelter Module", fileName = "NewModule")]
    public class ShelterModuleDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Модуль";
        [TextArea] [SerializeField] private string description;
        [Tooltip("Spawned on the slot when the module is built. Put its own interaction on it (for example a ModuleConverter).")]
        [SerializeField] private GameObject prefab;
        [Tooltip("What building it takes, out of the backpack and the stash.")]
        [SerializeField] private List<ItemAmount> cost = new List<ItemAmount>();
        [Tooltip("The item that teaches the blueprint when it is brought home from a raid; raids hide one in a container " +
                 "now and then. Empty: the blueprint is only given for quests.")]
        [SerializeField] private ItemDefinition blueprintItem;

        public string Id => name;
        public string DisplayName => displayName;
        public string Description => description;
        public GameObject Prefab => prefab;
        public IReadOnlyList<ItemAmount> Cost => cost;
        public ItemDefinition BlueprintItem => blueprintItem;
    }

    /// <summary>
    /// A place in the shelter where a module can stand. E on a free one opens the list of the modules whose
    /// blueprints the player knows; building one puts its prefab here. Built modules are restored from the save.
    /// </summary>
    public class ShelterModuleSlot : ShelterInteractable
    {
        [Tooltip("Unique within the scene: the save file remembers which module stands on which slot.")]
        [SerializeField] private string slotId = "Slot1";
        [Tooltip("Modules that can be built here. Empty: any module there is.")]
        [SerializeField] private ShelterModuleDefinition[] available;

        private GameObject built;
        private ShelterModuleDefinition module;

        public string SlotId => slotId;
        public bool IsBuilt => built != null;
        /// <summary>The module standing here, or null.</summary>
        public ShelterModuleDefinition Module => module;
        /// <summary>The object of the module standing here, or null.</summary>
        public GameObject Built => built;
        public override string Prompt => "Построить модуль";

        /// <summary>The modules that can stand here: the listed ones, or every module when none is listed.</summary>
        public IEnumerable<ShelterModuleDefinition> Available =>
            available != null && available.Any(m => m != null) ? available.Where(m => m != null) : ModuleCatalog.All;

        private void Start()
        {
            // Restore the module that was built here in an earlier session.
            var saved = ModuleCatalog.Find(BuiltOn(slotId));
            if (saved != null && saved.Prefab != null) Spawn(saved);
        }

        public override void Interact(GameObject actor) => BuildWindow.Open(actor, this);

        /// <summary>Why the module cannot be built here now, or null when it can. <paramref name="stores"/> pay for it.</summary>
        public string CannotBuild(ShelterModuleDefinition candidate, IEnumerable<GridInventory> stores)
        {
            if (IsBuilt) return "Место занято";
            if (candidate == null || candidate.Prefab == null) return "Нельзя построить";
            if (!Blueprints.IsKnown(candidate)) return "Нет чертежа";
            if (IsBuiltAnywhere(candidate)) return "Уже построен";
            if (!ModuleCost.CanAfford(candidate.Cost, stores)) return "Не хватает ресурсов";
            return null;
        }

        /// <summary>Builds the module here, paying out of <paramref name="stores"/> in their order. False when it cannot.</summary>
        public bool TryBuild(ShelterModuleDefinition candidate, IReadOnlyList<GridInventory> stores)
        {
            if (CannotBuild(candidate, stores) != null) return false;

            ModuleCost.Take(candidate.Cost, stores);
            GameSession.Data.builtModules.Add($"{slotId}={candidate.Id}");
            Spawn(candidate);
            return true;
        }

        /// <summary>True when the module stands on some slot already: every module is built once.</summary>
        public static bool IsBuiltAnywhere(ShelterModuleDefinition candidate) =>
            candidate != null && GameSession.Data.builtModules.Any(m => m.EndsWith("=" + candidate.Id));

        // The id of the module the save puts on a slot, or null.
        private static string BuiltOn(string slot)
        {
            string saved = GameSession.Data.builtModules.Find(m => m.StartsWith(slot + "="));
            return saved?.Substring(slot.Length + 1);
        }

        private void Spawn(ShelterModuleDefinition definition)
        {
            module = definition;
            built = Instantiate(definition.Prefab, transform.position, Quaternion.identity, transform.parent);
            built.name = definition.Id;
            foreach (var part in built.GetComponentsInChildren<IShelterModule>()) part.Bind(slotId, definition);

            // The mark of a free place goes, and an occupied slot is no longer an interactable.
            if (TryGetComponent<SpriteRenderer>(out var mark)) mark.enabled = false;
            enabled = false;
        }
    }

    /// <summary>A part of a module's prefab that wants to know where it stands (its state is kept by slot).</summary>
    public interface IShelterModule
    {
        void Bind(string slotId, ShelterModuleDefinition module);
    }
}
