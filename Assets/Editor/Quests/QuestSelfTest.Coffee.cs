using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Quests;
using ProjectMayham.Shelter;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    public static partial class QuestSelfTest
    {
        static partial void CoffeeTests(Checks t)
        {
            var module = AssetDatabase.LoadAssetAtPath<ShelterModuleDefinition>(CoffeeMachineSetup.ModulePath);
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(CoffeeMachineSetup.QuestPath);
            var beans = AssetDatabase.LoadAssetAtPath<ItemDefinition>(CoffeeMachineSetup.BeansPath);
            t.Ok(module != null && module.Prefab != null, "coffee module has a prefab");
            t.Ok(quest != null && quest.Giver.Id == "Intern" && quest.RewardBlueprint == module, "Intern rewards the coffee blueprint");
            t.Ok(beans != null && beans.Icon != null && beans.WorldPrefab.GetComponent<ItemPickup>().Item == beans, "new beans have their own sprite and pickup");
            if (module == null || quest == null || beans == null) return;
            t.Ok(beans.WorldPrefab.GetComponent<ProjectMayham.World.WrapEntity>() != null, "loose beans follow the wrapped raid world");
            t.Ok(module.BlueprintItem == null, "the blueprint cannot bypass Intern by spawning in raid loot");
            t.Ok(ItemDatabase.Find(beans.Id) == beans, "beans survive inventory save/load by their database id");
            t.Ok(!quest.Repeatable && quest.Requires.Any(q => q.Id == "Intern_00_Rescue"), "quest follows rescue and cannot be farmed");
            foreach (string loot in new[] { "BreakRoom", "Storage", "Cabinet" })
                t.Ok(AssetDatabase.LoadAssetAtPath<LootTable>($"Assets/Data/Level/Loot/Loot_{loot}.asset").Entries.Any(e => e.item == beans && e.weight > 0), "beans obtainable in " + loot);

            using (var world = new World())
            {
                QuestCatalog.Reset();
                ModuleCatalog.Reset();
                t.Eq(QuestLog.StatusOf(quest), QuestStatus.Locked, "coffee quest locked before rescue");
                world.Data.quests.Add(new QuestProgressData { quest = "Intern_00_Rescue", state = 2 });
                t.Eq(QuestLog.StatusOf(quest), QuestStatus.Available, "coffee quest available after rescue");
                QuestLog.Accept(quest);
                var pack = world.Inventory();
                var stash = world.Inventory();
                var stores = new List<GridInventory> { pack, stash };
                t.Ok(!Blueprints.IsKnown(module), "blueprint starts unknown");
                t.Eq(QuestLog.TryTurnIn(quest, pack), TurnInResult.NotReady, "missing quest materials do not grant blueprint");
                foreach (var objective in quest.Objectives) pack.Add(objective.Item, objective.Count);
                t.Eq(QuestLog.TryTurnIn(quest, pack), TurnInResult.Done, "quest materials can be delivered");
                t.Ok(Blueprints.IsKnown(module) && pack.CountOf(beans) == 1, "blueprint learned with starter beans");
                t.Eq(QuestLog.TryTurnIn(quest, pack), TurnInResult.NotActive, "no duplicate reward");

                var first = new GameObject("coffee test slot") { hideFlags = HideFlags.HideAndDontSave };
                var second = new GameObject("coffee test second slot") { hideFlags = HideFlags.HideAndDontSave };
                ShelterModuleSlot slot = null, restored = null;
                try
                {
                    slot = first.AddComponent<ShelterModuleSlot>();
                    restored = second.AddComponent<ShelterModuleSlot>();
                    SetSlot(slot, "CoffeeTest");
                    SetSlot(restored, "CoffeeTest");
                    t.Ok(slot.Available.Contains(module), "module appears on unrestricted shelter slots");
                    world.Data.blueprints.Remove(module.Id);
                    t.Eq(slot.CannotBuild(module, stores), "Нет чертежа", "building is blocked without blueprint");
                    Blueprints.Learn(module);
                    t.Eq(slot.CannotBuild(module, stores), "Не хватает ресурсов", "building requires resources");
                    foreach (var cost in module.Cost) stash.Add(cost.item, cost.count);
                    t.Ok(slot.TryBuild(module, stores), "coffee machine builds on a slot");
                    t.Ok(module.Cost.All(c => ModuleCost.Count(c.item, stores) == 0), "construction consumes materials");
                    var machine = slot.Built.GetComponent<CoffeeMachine>();
                    t.Ok(machine != null && machine.Water != null && machine.Coffee != null, "production inputs are wired");
                    t.Eq(restored.CannotBuild(module, stores), "Уже построен", "second machine is blocked");
                    stash.Add(machine.Water, 4);
                    t.Eq(machine.LoadWater(stores, 99), 3, "water tank holds three");
                    t.Ok(machine.LoadBeans(stores) && machine.State.beanUses == 3, "one portion gives three uses");
                    t.Ok(!machine.LoadBeans(stores), "cannot overwrite remaining bean uses");
                    t.Ok(machine.State.ready == 0 && machine.State.loaded == 3, "no coffee on loading day");
                    QuestLog.CountRaid();
                    t.Ok(machine.State.ready == 1 && machine.State.loaded == 2 && machine.State.beanUses == 2, "first raid consumes one water and one charge");
                    t.Eq(machine.State.ready, 1, "re-reading state does not brew again");
                    var saved = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(world.Data));
                    DataFieldForCoffee(saved);
                    second.SendMessage("Start", SendMessageOptions.RequireReceiver);
                    var reloaded = restored.Built.GetComponent<CoffeeMachine>();
                    t.Ok(reloaded.State.ready == 1 && reloaded.State.loaded == 2 && reloaded.State.beanUses == 2, "slot and production restore from save");
                    QuestLog.CountRaid();
                    t.Ok(reloaded.State.ready == 2 && reloaded.State.beanUses == 1, "second raid uses second charge");
                    QuestLog.CountRaid();
                    t.Ok(reloaded.State.ready == 3 && reloaded.State.loaded == 0 && reloaded.State.beanUses == 0, "third raid exhausts the portion");
                    QuestLog.CountRaid();
                    t.Eq(reloaded.State.ready, 3, "empty machine produces nothing");
                    var full = world.Inventory();
                    for (int i = 0; i < 40; i++) full.Add(Item("Mop"), 1);
                    t.Eq(reloaded.Collect(new[] { full }), 0, "full inventory cannot collect coffee");
                    t.Eq(reloaded.State.ready, 3, "uncollected coffee stays in machine");
                    t.Eq(reloaded.Collect(stores), 3, "ready coffee can be collected");
                    t.Eq(ModuleCost.Count(reloaded.Coffee, stores), 3, "three real consumables received");
                }
                finally
                {
                    if (slot != null && slot.Built != null) Object.DestroyImmediate(slot.Built);
                    if (restored != null && restored.Built != null) Object.DestroyImmediate(restored.Built);
                    Object.DestroyImmediate(first);
                    Object.DestroyImmediate(second);
                    ModuleCatalog.Reset();
                }
            }
            var state = new ModuleStateData { loaded = 1, beanUses = 3, day = 5 };
            CoffeeMachine.Advance(state, 8);
            t.Ok(state.ready == 1 && state.beanUses == 2 && state.loaded == 0, "missing water preserves bean charges across skipped raids");
            state.loaded = 3;
            CoffeeMachine.Advance(state, 8);
            t.Eq(state.ready, 1, "refill does not retroactively brew missed days");
            CoffeeMachine.Advance(state, 9);
            t.Ok(state.ready == 2 && state.beanUses == 1, "production resumes on next raid");
            var dryBeans = new ModuleStateData { loaded = 3, day = 1 };
            CoffeeMachine.Advance(dryBeans, 5);
            t.Ok(dryBeans.loaded == 3 && dryBeans.ready == 0, "without beans water is preserved");
            dryBeans.beanUses = 3;
            CoffeeMachine.Advance(dryBeans, 20);
            t.Ok(dryBeans.ready == 3 && dryBeans.beanUses == 0 && dryBeans.loaded == 0, "long absence bounded by loaded ingredients");
        }

        private static void DataFieldForCoffee(SaveData data) => typeof(GameSession)
            .GetField("data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, data);
    }
}
