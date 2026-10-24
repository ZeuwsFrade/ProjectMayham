using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.Quests;
using ProjectMayham.Shelter;
using ProjectMayham.Trade;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    public static partial class QuestSelfTest
    {
        // Shelter modules: blueprints (for quests, brought home, hidden in raids), paying for a module, building it,
        // the paper press, and the assets of the press and of Tyler's quest that gives it.
        static partial void ModuleTests(Checks t)
        {
            var papers = Item("Papers");
            var folders = Item("Folders");
            var mop = Item("Mop");

            using (var world = new World())
            {
                // A module found in raids and one given only for quests.
                var found = world.Make<ShelterModuleDefinition>("TestFound");
                var plan = world.Make<ItemDefinition>("TestPlan");
                var planSo = new SerializedObject(plan);
                planSo.FindProperty("displayName").stringValue = "Чертёж";
                planSo.FindProperty("holdable").boolValue = true;
                planSo.ApplyModifiedPropertiesWithoutUndo();
                var prefab = new GameObject("TestModulePrefab") { hideFlags = HideFlags.HideAndDontSave };
                Module(found, "Найденный", prefab, plan, (folders, 2), (mop, 1));
                var given = world.Make<ShelterModuleDefinition>("TestGiven");
                Module(given, "Данный", prefab, null, (papers, 3));
                ModuleCatalog.Override(new[] { found, given });
                try
                {
                    BlueprintChecks(t, world, found, given, plan);
                    CostChecks(t, world, folders, mop);
                    BuildChecks(t, world, found, folders, mop);
                    LoadChecks(t, world);
                }
                finally
                {
                    ModuleCatalog.Reset();
                    Object.DestroyImmediate(prefab);
                }
            }

            ConverterChecks(t);
            AssetChecks(t);
        }

        private static void Module(ShelterModuleDefinition module, string title, GameObject prefab, ItemDefinition plan,
            params (ItemDefinition item, int count)[] cost)
        {
            var so = new SerializedObject(module);
            so.FindProperty("displayName").stringValue = title;
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.FindProperty("blueprintItem").objectReferenceValue = plan;
            var list = so.FindProperty("cost");
            list.arraySize = cost.Length;
            for (int i = 0; i < cost.Length; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = cost[i].item;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("count").intValue = cost[i].count;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BlueprintChecks(Checks t, World world, ShelterModuleDefinition found, ShelterModuleDefinition given, ItemDefinition plan)
        {
            t.Ok(!Blueprints.IsKnown(given), "no blueprint is known at first");

            // For a quest.
            var quest = world.Quest("Blueprint");
            world.Objective(quest, QuestObjectiveKind.Deliver, Item("Papers"), 1);
            var so = new SerializedObject(quest);
            so.FindProperty("rewardBlueprint").objectReferenceValue = given;
            so.ApplyModifiedPropertiesWithoutUndo();
            t.Ok(QuestText.Reward(quest).Contains("чертёж «Данный»"), "the reward names the blueprint");
            QuestLog.Accept(quest);
            var backpack = world.Inventory();
            backpack.Add(Item("Papers"), 1);
            t.Eq(QuestLog.TryTurnIn(quest, backpack), TurnInResult.Done, "the quest is handed in");
            t.Ok(Blueprints.IsKnown(given), "and its blueprint is known");
            Blueprints.Learn(given);
            t.Eq(world.Data.blueprints.Count, 1, "learned once");

            // Hidden in raids: only a module with a blueprint item, unknown, not owned already, and with the chance.
            t.Ok(Blueprints.PickForRaid(new System.Random(1), 1f) == found, "a raid hides the blueprint of a module found in raids");
            t.Ok(Blueprints.PickForRaid(new System.Random(1), 0f) == null, "with no chance, none");
            int hits = 0;
            for (int i = 0; i < 1000; i++) if (Blueprints.PickForRaid(new System.Random(i), 0.5f) != null) hits++;
            t.Ok(hits > 420 && hits < 580, $"with half a chance, about every other raid ({hits} of 1000)");
            world.Data.stash.stacks.Add(new StackData { item = plan.Id, count = 1 });
            t.Ok(Blueprints.PickForRaid(new System.Random(1), 1f) == null, "not while the player owns the blueprint item");
            world.Data.stash.stacks.Clear();

            // Brought home: learned, and the item goes.
            var carried = world.Inventory();
            carried.Add(plan, 1);
            var learned = Blueprints.LearnCarried(carried);
            t.Ok(learned.Count == 1 && learned[0] == found, "a blueprint brought home is learned");
            t.Ok(Blueprints.IsKnown(found) && carried.CountOf(plan) == 0, "and its item is gone");
            t.Ok(Blueprints.PickForRaid(new System.Random(1), 1f) == null, "a known blueprint is not hidden in raids any more");
            carried.HoldNew(plan);
            t.Ok(Blueprints.LearnCarried(carried).Count == 0 && carried.HandsEmpty, "a second copy, in the hands, is only taken away");
        }

        private static void CostChecks(Checks t, World world, ItemDefinition folders, ItemDefinition mop)
        {
            var backpack = world.Inventory();
            var stash = world.Inventory();
            backpack.Add(folders, 1);
            stash.Add(folders, 4);
            backpack.HoldNew(mop);
            var stores = new List<GridInventory> { backpack, stash };
            var cost = new List<ItemAmount> { new ItemAmount { item = folders, count = 3 }, new ItemAmount { item = mop, count = 1 } };

            t.Eq(ModuleCost.Count(folders, stores), 5, "the backpack and the stash are counted together");
            t.Eq(ModuleCost.Count(mop, stores), 1, "with what the hands hold");
            t.Ok(ModuleCost.CanAfford(cost, stores), "three folders and a mop are there");
            t.Ok(ModuleCost.Take(cost, stores), "they are paid");
            t.Ok(backpack.CountOf(folders) == 0 && stash.CountOf(folders) == 2, "the backpack's folder first, then the stash's");
            t.Ok(backpack.HandsEmpty, "the mop out of the hands");
            t.Ok(!ModuleCost.CanAfford(cost, stores) && !ModuleCost.Take(cost, stores), "not twice");
            t.Eq(stash.CountOf(folders), 2, "and a refused payment takes nothing");
        }

        private static void BuildChecks(Checks t, World world, ShelterModuleDefinition found, ItemDefinition folders, ItemDefinition mop)
        {
            var go = new GameObject("self-test slot") { hideFlags = HideFlags.HideAndDontSave };
            var other = new GameObject("self-test slot 2") { hideFlags = HideFlags.HideAndDontSave };
            ShelterModuleSlot slot = null;
            try
            {
                slot = go.AddComponent<ShelterModuleSlot>();
                var second = other.AddComponent<ShelterModuleSlot>();
                SetSlot(slot, "TestSlotA");
                SetSlot(second, "TestSlotB");
                var stores = new List<GridInventory> { world.Inventory() };

                t.Ok(slot.Available.Contains(found), "a slot without a list takes any module");
                world.Data.blueprints.Remove(found.Id);
                t.Eq(slot.CannotBuild(found, stores), "Нет чертежа", "without the blueprint it is not built");
                Blueprints.Learn(found);
                t.Eq(slot.CannotBuild(found, stores), "Не хватает ресурсов", "nor without what it takes");
                stores[0].Add(folders, 2);
                stores[0].Add(mop, 1);
                t.Ok(slot.CannotBuild(found, stores) == null, "with both it can be built");
                t.Ok(slot.TryBuild(found, stores), "it is built");
                t.Ok(slot.IsBuilt && slot.Module == found && !slot.enabled, "it stands on the slot, which is busy now");
                t.Ok(world.Data.builtModules.Contains("TestSlotA=TestFound"), "the save remembers it");
                t.Ok(stores[0].CountOf(folders) == 0 && stores[0].CountOf(mop) == 0, "what it took is gone");
                stores[0].Add(folders, 2);
                stores[0].Add(mop, 1);
                t.Eq(second.CannotBuild(found, stores), "Уже построен", "a module is built once");
                t.Eq(slot.CannotBuild(found, stores), "Место занято", "a busy slot takes nothing");
            }
            finally
            {
                // The module that was put on the slot.
                if (slot != null && slot.Built != null) Object.DestroyImmediate(slot.Built);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(other);
            }
        }

        private static void SetSlot(ShelterModuleSlot slot, string id)
        {
            var so = new SerializedObject(slot);
            so.FindProperty("slotId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Loading the press: as many as asked for, never more than there is room for or than the player has.
        private static void LoadChecks(Checks t, World world)
        {
            var books = Item("Books");
            var go = new GameObject("self-test press") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var press = go.AddComponent<ModuleConverter>();
                var so = new SerializedObject(press);
                so.FindProperty("input").objectReferenceValue = books;
                so.FindProperty("output").objectReferenceValue = Item("Papers");
                so.FindProperty("capacity").intValue = 10;
                so.ApplyModifiedPropertiesWithoutUndo();
                press.Bind("TestPressSlot", null);

                var backpack = world.Inventory();
                backpack.Add(books, 4);
                var stash = world.Inventory();
                stash.Add(books, 5);
                stash.Add(books, 5);
                var stores = new List<GridInventory> { backpack, stash };

                t.Eq(press.Loadable(stores), 10, "ten books fit, fourteen are there");
                t.Eq(press.Load(stores, 3), 3, "three are loaded when three are asked for");
                t.Ok(press.State.loaded == 3 && backpack.CountOf(books) == 1 && stash.CountOf(books) == 10, "out of the backpack first");
                t.Eq(press.Loadable(stores), 7, "room for seven more");
                t.Eq(press.Load(stores, 50), 7, "asking for more loads what fits");
                t.Ok(press.State.loaded == 10 && ModuleCost.Count(books, stores) == 4, "and takes only that");
                t.Eq(press.Load(stores, 1), 0, "a full press takes nothing");
                t.Eq(press.Load(stores, 0), 0, "nor does asking for none");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void ConverterChecks(Checks t)
        {
            var state = new ModuleStateData { slot = "Test" };
            ModuleConverter.Put(state, 3, 7);
            t.Eq(ModuleConverter.Room(state, 10), 7, "three books in, room for seven more");
            ModuleConverter.Ripen(state, 7, 5);
            t.Ok(state.loaded == 3 && state.ready == 0, "on the same day nothing is ready");
            ModuleConverter.Put(state, 2, 7);
            ModuleConverter.Ripen(state, 8, 5);
            t.Ok(state.loaded == 0 && state.ready == 25, "after a raid five books are twenty-five papers");
            ModuleConverter.Put(state, 1, 8);
            ModuleConverter.Ripen(state, 8, 5);
            t.Ok(state.loaded == 1 && state.ready == 25, "what is loaded later waits for the next raid");
        }

        private static void AssetChecks(Checks t)
        {
            ModuleCatalog.Reset();
            var press = ModuleCatalog.Find("PaperPress");
            t.Ok(press != null, "the paper press is in Resources/Modules (Mayham > Shelter > Set Up Modules)");
            if (press == null) return;

            var converter = press.Prefab != null ? press.Prefab.GetComponent<ModuleConverter>() : null;
            t.Ok(converter != null, "its prefab turns things into others");
            if (converter != null)
            {
                t.Ok(converter.Input == Item("Books") && converter.Output == Item("Papers"), "books into papers");
                t.Eq(converter.OutputPerInput, 5, "five papers a book");
            }
            t.Ok(press.Cost.Count > 0 && press.Cost.All(c => c.item != null), "building it takes something");
            t.Ok(press.BlueprintItem == null, "its blueprint is not found in raids: it is given for a quest");

            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/Resources/Quests/Tyler/Tyler_Press.asset");
            t.Ok(quest != null && quest.RewardBlueprint == press, "Tyler gives the blueprint of the press for a quest");
        }
    }
}
