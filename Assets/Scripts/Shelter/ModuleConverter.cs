using System;
using System.Collections.Generic;
using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.Quests;
using ProjectMayham.Trade;
using ProjectMayham.UI;
using UnityEngine;

namespace ProjectMayham.Shelter
{
    /// <summary>
    /// A module that turns one thing into another over a day (a day is a raid): what is loaded today is ready after
    /// the next raid. E opens its window: load the input out of the backpack and the stash, take what is ready into
    /// them. What it holds is kept in the save by the slot it stands on.
    /// </summary>
    public class ModuleConverter : ShelterInteractable, IShelterModule
    {
        [SerializeField] private ItemDefinition input;
        [SerializeField] private ItemDefinition output;
        [Tooltip("Pieces of the output one piece of the input gives.")]
        [SerializeField, Min(1)] private int outputPerInput = 5;
        [Tooltip("Pieces of the input it holds at a time.")]
        [SerializeField, Min(1)] private int capacity = 10;

        private string slotId;
        private ShelterModuleDefinition module;

        /// <summary>Raised when something was loaded or taken.</summary>
        public event Action Changed;

        public ItemDefinition Input => input;
        public ItemDefinition Output => output;
        public int OutputPerInput => Mathf.Max(1, outputPerInput);
        public int Capacity => Mathf.Max(1, capacity);
        public string Title => module != null ? module.DisplayName : name;
        public string Description => module != null ? module.Description : string.Empty;
        public override string Prompt => $"Открыть: {Title}";

        /// <summary>What it holds now; what was loaded on an earlier day has turned into the output.</summary>
        public ModuleStateData State => Ripen(StateOf(slotId), QuestLog.Day, OutputPerInput);

        public void Bind(string slot, ShelterModuleDefinition definition)
        {
            slotId = slot;
            module = definition;
        }

        public override void Interact(GameObject actor) => ConverterWindow.Open(actor, this);

        /// <summary>How many pieces of the input can go in now: no more than there is room for and than the stores hold.</summary>
        public int Loadable(IReadOnlyList<GridInventory> stores) => Mathf.Min(Room(State, Capacity), ModuleCost.Count(input, stores));

        /// <summary>
        /// Loads up to <paramref name="count"/> pieces of the input (as many as can go in, when that is fewer), out of
        /// the stores in their order. Returns how many went in.
        /// </summary>
        public int Load(IReadOnlyList<GridInventory> stores, int count)
        {
            count = Mathf.Min(count, Loadable(stores));
            if (count <= 0 || !ModuleCost.Take(new[] { new ItemAmount { item = input, count = count } }, stores)) return 0;

            Put(State, count, QuestLog.Day);
            Changed?.Invoke();
            return count;
        }

        /// <summary>Puts what is ready into the stores in their order; what finds no room stays. Returns how many were taken.</summary>
        public int Collect(IReadOnlyList<GridInventory> stores)
        {
            var state = State;
            int before = state.ready;
            foreach (var store in stores)
            {
                if (store == null || state.ready <= 0) continue;
                state.ready = store.Add(output, state.ready);
            }
            int taken = before - state.ready;
            if (taken > 0) Changed?.Invoke();
            return taken;
        }

        // ---- the rules, on the saved state ----

        /// <summary>The saved state of the module on a slot, made when there is none.</summary>
        public static ModuleStateData StateOf(string slot)
        {
            var data = GameSession.Data;
            data.modules ??= new List<ModuleStateData>();
            var state = data.modules.Find(m => m.slot == slot);
            if (state != null) return state;
            state = new ModuleStateData { slot = slot };
            data.modules.Add(state);
            return state;
        }

        /// <summary>What was loaded before <paramref name="today"/> becomes the output.</summary>
        public static ModuleStateData Ripen(ModuleStateData state, int today, int perInput)
        {
            if (state.loaded > 0 && today > state.day)
            {
                state.ready += state.loaded * perInput;
                state.loaded = 0;
            }
            return state;
        }

        /// <summary>How many more pieces of the input fit.</summary>
        public static int Room(ModuleStateData state, int capacity) => Mathf.Max(0, capacity - state.loaded);

        /// <summary>Loads pieces of the input today: they are ready after the next raid, with what was loaded today before.</summary>
        public static void Put(ModuleStateData state, int count, int today)
        {
            if (count <= 0) return;
            state.loaded += count;
            state.day = today;
        }
    }
}
