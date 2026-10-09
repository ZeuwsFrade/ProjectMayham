using ProjectMayham.Core;
using ProjectMayham.Trade;
using ProjectMayham.UI;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMayham.Shelter
{
    /// <summary>
    /// STUB of the additional shelter modules (workbench, coffee corner, ...). A module is data
    /// (<see cref="ShelterModuleDefinition"/>) plus a prefab that appears on a free <see cref="ShelterModuleSlot"/>
    /// once it is built. Building is not wired to any UI yet, and there are no modules.
    /// </summary>
    [CreateAssetMenu(menuName = "Mayham/Shelter Module", fileName = "NewModule")]
    public class ShelterModuleDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Модуль";
        [TextArea] [SerializeField] private string description;
        [Tooltip("Spawned on the slot when the module is built. Put its own interaction on it.")]
        [SerializeField] private GameObject prefab;
        [Tooltip("Components that building it takes (not checked yet).")]
        [SerializeField] private List<ItemAmount> cost = new List<ItemAmount>();

        public string Id => name;
        public string DisplayName => displayName;
        public string Description => description;
        public GameObject Prefab => prefab;
        public IReadOnlyList<ItemAmount> Cost => cost;
    }

    /// <summary>A place in the shelter where a module can stand. Empty slots say so when used; built modules are restored from the save.</summary>
    public class ShelterModuleSlot : ShelterInteractable
    {
        [Tooltip("Unique within the scene: the save file remembers which module stands on which slot.")]
        [SerializeField] private string slotId = "Slot1";
        [Tooltip("Modules that can be built here. Used when building is implemented.")]
        [SerializeField] private ShelterModuleDefinition[] available;

        private GameObject built;

        public bool IsBuilt => built != null;
        public override string Prompt => "Осмотреть: свободное место для модуля";

        private void Start()
        {
            // Restore the module that was built here in an earlier session.
            string saved = GameSession.Data.builtModules.Find(m => m.StartsWith(slotId + "="));
            if (saved == null) return;

            string id = saved.Substring(slotId.Length + 1);
            foreach (var module in available ?? System.Array.Empty<ShelterModuleDefinition>())
            {
                if (module != null && module.Id == id) { Spawn(module); return; }
            }
        }

        public override void Interact(GameObject actor)
        {
            ConfirmDialog.Notice("Свободное место",
                "Здесь можно будет установить дополнительный модуль убежища.\n(Система модулей ещё не реализована.)");
        }

        /// <summary>Puts a module on the slot and remembers it. Not called by any UI yet.</summary>
        public bool TryBuild(ShelterModuleDefinition module)
        {
            if (IsBuilt || module == null || module.Prefab == null) return false;

            GameSession.Data.builtModules.Add($"{slotId}={module.Id}");
            Spawn(module);
            return true;
        }

        private void Spawn(ShelterModuleDefinition module)
        {
            built = Instantiate(module.Prefab, transform.position, Quaternion.identity, transform.parent);
            enabled = false; // an occupied slot is no longer an interactable
        }
    }
}
