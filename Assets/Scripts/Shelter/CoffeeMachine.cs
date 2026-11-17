using ProjectMayham.Localization;
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
    /// <summary>One coffee per raid, using one water and one of a portion's three bean charges.</summary>
    public class CoffeeMachine : ShelterInteractable, IShelterModule
    {
        public const int UsesPerPortion = 3;
        public const int WaterCapacity = 3;
        [SerializeField] private ItemDefinition water;
        [SerializeField] private ItemDefinition beans;
        [SerializeField] private ItemDefinition coffee;
        private string slotId;
        private ShelterModuleDefinition definition;
        public event Action Changed;
        public ItemDefinition Water => water;
        public ItemDefinition Beans => beans;
        public ItemDefinition Coffee => coffee;
        public string Title => definition != null ? definition.DisplayName : "Кофемашина";
        public override string Prompt => GameText.Format("Открыть: {0}", Title);
        public ModuleStateData State => Advance(ModuleConverter.StateOf(slotId), QuestLog.Day);

        public void Bind(string slot, ShelterModuleDefinition module)
        {
            slotId = slot;
            definition = module;
        }

        public override void Interact(GameObject actor) => CoffeeMachineWindow.Open(actor, this);

        public int LoadableWater(IReadOnlyList<GridInventory> stores) =>
            Mathf.Min(Mathf.Max(0, WaterCapacity - State.loaded), ModuleCost.Count(water, stores));

        public int LoadWater(IReadOnlyList<GridInventory> stores, int count)
        {
            var state = State;
            count = Mathf.Clamp(count, 0, LoadableWater(stores));
            if (count == 0 || !ModuleCost.Take(new[] { new ItemAmount { item = water, count = count } }, stores)) return 0;
            state.loaded += count;
            Changed?.Invoke();
            return count;
        }

        public bool CanLoadBeans(IReadOnlyList<GridInventory> stores) => State.beanUses == 0 && ModuleCost.Count(beans, stores) > 0;

        public bool LoadBeans(IReadOnlyList<GridInventory> stores)
        {
            var state = State;
            if (state.beanUses != 0 || !ModuleCost.Take(new[] { new ItemAmount { item = beans, count = 1 } }, stores)) return false;
            state.beanUses = UsesPerPortion;
            Changed?.Invoke();
            return true;
        }

        public int Collect(IReadOnlyList<GridInventory> stores)
        {
            var state = State;
            int before = state.ready;
            foreach (var store in stores)
                if (store != null && state.ready > 0) state.ready = store.Add(coffee, state.ready);
            int taken = before - state.ready;
            if (taken > 0) Changed?.Invoke();
            return taken;
        }

        /// <summary>Elapsed raids are settled once. Missing ingredients never accrue production debt.</summary>
        public static ModuleStateData Advance(ModuleStateData state, int today)
        {
            if (today <= state.day) return state;
            int made = Mathf.Min(today - state.day, Mathf.Min(state.loaded, state.beanUses));
            state.loaded -= made;
            state.beanUses -= made;
            state.ready += made;
            state.day = today;
            return state;
        }
    }
}
