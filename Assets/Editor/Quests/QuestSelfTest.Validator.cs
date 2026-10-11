using System.Linq;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Quests;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    public static partial class QuestSelfTest
    {
        private static string Errors(World world, QuestValidator.Facts facts) =>
            string.Join(" || ", QuestValidator.Check(world.Quests, facts).Where(p => p.IsError).Select(p => p.Message));

        private static string Warnings(World world, QuestValidator.Facts facts) =>
            string.Join(" || ", QuestValidator.Check(world.Quests, facts).Where(p => !p.IsError).Select(p => p.Message));

        private static void SetItem(ItemDefinition item, int value, GameObject prefab)
        {
            var so = new SerializedObject(item);
            so.FindProperty("value").intValue = value;
            so.FindProperty("worldPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // What the validator knows about the project, made by hand: the test quests "lie" where quests belong.
        private static QuestValidator.Facts TestFacts(params ItemDefinition[] known)
        {
            var facts = new QuestValidator.Facts
            {
                PathOf = _ => "Assets/Resources/Quests/Test/x.asset",
                Library = AssetDatabase.LoadAssetAtPath<RoomLibrary>(RoomFactory.LibraryPath)
            };
            foreach (var item in known) facts.DatabaseItems.Add(item);
            facts.NpcIds.Add("TestGiver");
            return facts;
        }

        static partial void ValidatorTests(Checks t)
        {
            var papers = Item("Papers");
            var mug = Item("Mug");

            // Sound quests: nothing to complain about.
            using (var world = new World())
            {
                var special = world.Make<ItemDefinition>("Special");
                SetItem(special, 0, papers.WorldPrefab);
                var facts = TestFacts(papers, mug, special);
                facts.LootItems.Add(papers);

                var good = world.Quest("Good");
                world.Objective(good, QuestObjectiveKind.Deliver, papers, 3);
                world.Objective(good, QuestObjectiveKind.VisitRoom, null, 1, RoomKind.Parking);
                world.Reward(good, 10, mug, 1);
                var find = world.Quest("Find", 1, good);
                world.Objective(find, QuestObjectiveKind.FindSpecial, special, 1, RoomKind.BossOffice);
                world.Reward(find, 10);
                t.Eq(Errors(world, facts), string.Empty, "sound quests have no errors");

                // The special item, one mistake at a time.
                SetItem(special, 5, papers.WorldPrefab);
                t.Ok(Errors(world, facts).Contains("цену 5"), "a special item with a price is an error");
                SetItem(special, 0, papers.WorldPrefab);
                facts.LootItems.Add(special);
                t.Ok(Errors(world, facts).Contains("в таблице лута"), "a special item in a loot table is an error");
                facts.LootItems.Remove(special);
                facts.TraderItems.Add(special);
                t.Ok(Errors(world, facts).Contains("у торговца"), "a special item a trader deals in is an error");
                facts.TraderItems.Remove(special);
                SetItem(special, 0, null);
                t.Ok(Errors(world, facts).Contains("нет префаба"), "a special item without a prefab is an error");
                SetItem(special, 0, new GameObject("bare") { hideFlags = HideFlags.HideAndDontSave });
                t.Ok(Errors(world, facts).Contains("ItemPickup"), "a prefab without ItemPickup is an error");
                Object.DestroyImmediate(special.WorldPrefab);
                SetItem(special, 0, papers.WorldPrefab);
                facts.DatabaseItems.Remove(special);
                t.Ok(Errors(world, facts).Contains("нет в базе предметов"), "an item missing from the database is an error");
                facts.DatabaseItems.Add(special);
                t.Eq(Errors(world, facts), string.Empty, "and all is well again");

                // A deliver item that cannot be found anywhere is only a warning.
                facts.LootItems.Clear();
                t.Ok(Errors(world, facts).Length == 0 && Warnings(world, facts).Contains("негде взять"), "an item found nowhere is a warning");
            }

            // The quest itself.
            using (var world = new World())
            {
                var facts = TestFacts(papers, mug);
                facts.LootItems.Add(papers);

                var noGiver = world.Quest("NoGiver");
                var so = new SerializedObject(noGiver);
                so.FindProperty("giver").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                t.Ok(Errors(world, facts).Contains("не указан выдающий"), "no giver is an error");

                var twice = world.Quest("Twice");
                world.Objective(twice, QuestObjectiveKind.Deliver, papers, 1);
                world.Objective(twice, QuestObjectiveKind.Deliver, papers, 2);
                t.Ok(Errors(world, facts).Contains("в двух целях"), "the same item in two objectives is an error");

                var noItem = world.Quest("NoItem");
                world.Objective(noItem, QuestObjectiveKind.Deliver, null, 1);
                t.Ok(Errors(world, facts).Contains("не указан предмет"), "an objective without its item is an error");

                var emptyReward = world.Quest("EmptyReward");
                var reward = new SerializedObject(emptyReward);
                reward.FindProperty("rewardItems").arraySize = 1;
                reward.ApplyModifiedPropertiesWithoutUndo();
                t.Ok(Errors(world, facts).Contains("в награде пустой предмет"), "an empty reward line is an error");

                var same = world.Quest("Twice");
                t.Ok(same != twice && Errors(world, facts).Contains("с таким же именем"), "two quests with one name is an error");

                facts.PathOf = _ => "Assets/Data/Somewhere/x.asset";
                t.Ok(Errors(world, facts).Contains("вне Assets/Resources/Quests"), "a quest outside Resources/Quests is an error");
            }

            // Prerequisites.
            using (var world = new World())
            {
                var facts = TestFacts(papers);
                var a = world.Quest("A");
                var b = world.Quest("B", 0, a);
                var c = world.Quest("C", 0, b);
                t.Eq(Errors(world, facts), string.Empty, "a chain is fine");

                var so = new SerializedObject(a);
                var list = so.FindProperty("requires");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = c;
                so.ApplyModifiedPropertiesWithoutUndo();
                string errors = Errors(world, facts);
                t.Ok(errors.Contains("A: условия образуют цикл") && errors.Contains("B: условия образуют цикл") && errors.Contains("C: условия образуют цикл"),
                    "every quest of a cycle is reported");

                list.GetArrayElementAtIndex(0).objectReferenceValue = a;
                so.ApplyModifiedPropertiesWithoutUndo();
                t.Ok(Errors(world, facts).Contains("требует сам себя"), "a quest that requires itself is an error");

                list.GetArrayElementAtIndex(0).objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                t.Ok(Errors(world, facts).Contains("удалённый квест"), "an empty prerequisite is an error");
            }

            // Texts.
            using (var world = new World())
            {
                var facts = TestFacts(papers);
                var quest = world.Quest("Long");
                world.Set(quest, "offerText", "Коротко.\n\n" + new string('я', QuestText.PageLimit + 1));
                string warnings = Warnings(world, facts);
                t.Ok(warnings.Contains("предложение: страниц длиннее"), "a page that is too long is a warning");
                t.Ok(warnings.Contains("нет целей") && warnings.Contains("нет награды"), "no objectives and no reward are warnings");
                world.Set(quest, "completedText", "Раз.\nДва.\nТри.\nЧетыре.");
                t.Ok(Warnings(world, facts).Contains("благодарность: страниц длиннее 3 строк"), "a page of more than three lines is a warning");
                t.Eq(Errors(world, facts), string.Empty, "warnings are not errors");
            }

            // The giver: an NPC of that name has to exist, and its own lines have to fit too.
            using (var world = new World())
            {
                var facts = TestFacts(papers);
                facts.LootItems.Add(papers);
                var quest = world.Quest("OfGiver");
                world.Objective(quest, QuestObjectiveKind.Deliver, papers, 1);
                world.Reward(quest, 5);
                t.Ok(!Warnings(world, facts).Contains("TestGiver"), "a giver that is an NPC and speaks briefly raises nothing");

                facts.NpcIds.Clear();
                facts.NpcIds.Add("Tyler");
                t.Ok(Warnings(world, facts).Contains("TestGiver: такого NPC нет"), "a giver whose name matches no NPC is a warning");
                facts.NpcIds.Add("TestGiver");

                world.Set(world.Giver, "menuText", "Раз.\nДва.\nТри.\nЧетыре.");
                t.Ok(Warnings(world, facts).Contains("TestGiver: фраза над списком дел не помещается"), "a list line that does not fit is a warning");
                world.Set(world.Giver, "menuText", "Есть пара дел.");
                world.Set(world.Giver, "readyText", new string('я', QuestText.PageLimit + 1));
                t.Ok(Warnings(world, facts).Contains("TestGiver: фраза «всё готово»: страниц длиннее"), "a long line of the giver is a warning");
                t.Eq(Errors(world, facts), string.Empty, "none of this is an error");
            }
        }
    }
}
