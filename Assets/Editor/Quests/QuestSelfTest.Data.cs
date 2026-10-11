using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Level;
using ProjectMayham.Quests;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    public static partial class QuestSelfTest
    {
        static partial void DataTests(Checks t)
        {
            var papers = Item("Papers");
            var folders = Item("Folders");
            t.Ok(papers != null && folders != null, "the Papers and Folders items exist");

            using (var world = new World())
            {
                var quest = world.Quest("B_quest", order: 2);
                world.Objective(quest, QuestObjectiveKind.Deliver, papers, 3);
                world.Objective(quest, QuestObjectiveKind.Deliver, folders, 1);
                world.Objective(quest, QuestObjectiveKind.FindSpecial, folders, 1, RoomKind.BossOffice);
                world.Objective(quest, QuestObjectiveKind.VisitRoom, null, 1, RoomKind.Parking);
                world.Objective(quest, QuestObjectiveKind.Deliver, papers, 2, RoomKind.Office, "Свои слова");

                var o = quest.Objectives;
                t.Eq(o.Count, 5, "five objectives");
                t.Eq(o[0].Describe(), "Бумаги ×3", "deliver, several");
                t.Eq(o[0].Subject, "Бумаги", "deliver, subject has no amount");
                t.Eq(o[1].Describe(), "Папки", "deliver, one piece has no amount");
                t.Eq(o[2].Describe(), "Папки (Офис босса)", "special item names its room");
                t.Eq(o[3].Describe(), "Побывать: Парковка", "visit");
                t.Eq(o[3].VisitMark, "visit:Parking", "the mark of a visit uses the enum name");
                t.Eq(QuestObjective.VisitMarkOf(RoomKind.BossOffice), "visit:BossOffice", "mark of a kind");
                t.Eq(o[4].Describe(), "Свои слова", "own wording replaces the generated one");
                t.Eq(o[4].Subject, "Свои слова", "own wording is the subject too");
                t.Ok(o[0].NeedsItem && o[2].NeedsItem && !o[3].NeedsItem, "which kinds need an item");
                t.Ok(quest.IsUsable(out _), "a complete quest is usable");

                // The catalog: sorted by order, then by name; found by id and by giver.
                var first = world.Quest("Z_first", order: 1);
                var alsoSecond = world.Quest("A_quest", order: 2);
                t.Eq(string.Join(",", QuestCatalog.All.Select(q => q.Id)), "Z_first,A_quest,B_quest", "order, then name");
                t.Ok(QuestCatalog.Find("A_quest") == alsoSecond && QuestCatalog.Find("nothing") == null, "find by id");
                t.Eq(QuestCatalog.ByGiver("TestGiver").Count, 3, "quests of the giver");
                t.Eq(QuestCatalog.ByGiver("Somebody").Count, 0, "nobody else has quests");
                t.Ok(QuestCatalog.Giver("TestGiver") == world.Giver && QuestCatalog.Giver("Somebody") == null, "giver by id");
                world.Set(first, "title", "Первое дело");
                t.Eq(first.Title, "Первое дело", "the title is what the writer typed");
                world.Set(first, "title", "  ");
                t.Eq(first.Title, "Z_first", "an empty title falls back to the name of the asset");

                // A quest that cannot be checked is left out instead of breaking the game.
                var broken = world.Quest("Broken", order: 0);
                world.Objective(broken, QuestObjectiveKind.Deliver, null, 1);
                t.Ok(!broken.IsUsable(out string reason) && !string.IsNullOrEmpty(reason), "an objective without its item makes the quest unusable");
                t.Ok(QuestCatalog.Find("Broken") == null, "an unusable quest is not in the catalog");
            }

            // The save data: the new list exists, survives JSON, and a save from before quests still loads.
            var data = new SaveData();
            t.Ok(data.quests != null && data.quests.Count == 0, "a new save has an empty quest list");
            data.quests.Add(new QuestProgressData { quest = "Q", state = 1 });
            data.quests[0].marks.Add("visit:Parking");
            var copy = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            t.Ok(copy.quests.Count == 1 && copy.quests[0].quest == "Q" && copy.quests[0].state == 1 && copy.quests[0].marks[0] == "visit:Parking",
                "quest progress survives the JSON round trip");
            var old = JsonUtility.FromJson<SaveData>("{\"version\":1,\"money\":40}");
            t.Ok(old != null && old.money == 40 && (old.quests == null || old.quests.Count == 0), "a save without quest data loads with no quests");
        }
    }
}
