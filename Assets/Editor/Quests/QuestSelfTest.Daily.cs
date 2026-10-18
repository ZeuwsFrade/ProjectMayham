using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Dialogue;
using ProjectMayham.Level;
using ProjectMayham.Npcs;
using ProjectMayham.Quests;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    public static partial class QuestSelfTest
    {
        private static void SetDaily(QuestDefinition quest, bool repeatable = false, int after = 1, bool canAbandon = false, int penalty = 0, int loyalty = 0)
        {
            var so = new SerializedObject(quest);
            so.FindProperty("repeatable").boolValue = repeatable;
            so.FindProperty("repeatAfterRaids").intValue = after;
            so.FindProperty("canAbandon").boolValue = canAbandon;
            so.FindProperty("abandonPenalty").intValue = penalty;
            so.FindProperty("rewardLoyalty").intValue = loyalty;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSlots(QuestGiverDefinition giver, int slots)
        {
            var so = new SerializedObject(giver);
            so.FindProperty("dailySlots").intValue = slots;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<QuestDefinition> Dailies() => QuestLog.Offers("TestGiver").Where(q => q.Repeatable).ToList();

        static partial void DailyTests(Checks t)
        {
            var papers = Item("Papers");

            // ---- loyalty for an answer: once ----
            using (var world = new World())
            {
                var dialogue = world.Make<DialogueDefinition>("Kind");
                NpcSamples.Fill(dialogue, new List<NpcSamples.Line>
                {
                    new NpcSamples.Line
                    {
                        Id = "start", Start = true, Text = "Привет.",
                        Answers =
                        {
                            new NpcSamples.Answer { Text = "Спасибо", Next = "start", Actions = { (DialogueActionKind.AddLoyalty, "", 3) } },
                            new NpcSamples.Answer { Text = "Грубость", Next = "start", Actions = { (DialogueActionKind.AddLoyalty, "", -2) } },
                        },
                    },
                });
                var npc = Npc(world, "TestGiver", dialogue);
                NpcCatalog.Override(new[] { npc });
                try
                {
                    var start = NpcConversation.Start(new ConversationContext { Npc = npc, Backpack = world.Inventory() });
                    var (again, _) = Choose(start, "Спасибо");
                    t.Eq(Loyalty.Points("TestGiver"), 3, "an answer gives its loyalty");
                    Choose(again, "Спасибо");
                    var later = NpcConversation.Start(new ConversationContext { Npc = npc, Backpack = world.Inventory() });
                    Choose(later, "Спасибо");
                    t.Eq(Loyalty.Points("TestGiver"), 3, "the same answer again, also in a later talk, gives nothing more");
                    Choose(later, "Грубость");
                    Choose(later, "Грубость");
                    t.Eq(Loyalty.Points("TestGiver"), 1, "a rude answer takes loyalty off, also once");
                }
                finally
                {
                    NpcCatalog.Reset();
                }
            }

            // ---- the dailies of a day ----
            using (var world = new World())
            {
                SetSlots(world.Giver, 2);
                var story = world.Quest("Story");
                world.Objective(story, QuestObjectiveKind.Deliver, papers, 1);
                var pool = new List<QuestDefinition>();
                foreach (string id in new[] { "DailyA", "DailyB", "DailyC" })
                {
                    var daily = world.Quest(id, 1);
                    world.Objective(daily, QuestObjectiveKind.Deliver, papers, 1);
                    SetDaily(daily, repeatable: true, loyalty: 4);
                    pool.Add(daily);
                }

                var today = Dailies();
                t.Eq(today.Count, 2, "a giver with two slots offers two of his three dailies");
                t.Ok(QuestLog.Offers("TestGiver").Contains(story), "story quests are offered as before");
                t.Ok(Dailies().SequenceEqual(today), "asked again, the dailies of the day are the same");

                var first = today[0];
                QuestLog.Accept(first);
                var left = Dailies();
                t.Ok(left.Count == 1 && left[0] == today[1], "taking one leaves the other, and no third one comes the same day");
                var backpack = world.Inventory();
                backpack.Add(papers, 1);
                t.Eq(QuestLog.TryTurnIn(first, backpack), TurnInResult.Done, "a daily is turned in");
                t.Eq(QuestLog.StatusOf(first), QuestStatus.TurnedIn, "and waits");
                t.Ok(!QuestLog.Offers("TestGiver").Contains(first), "it is not offered again the same day");

                QuestLog.CountRaid();
                t.Eq(QuestLog.Day, 1, "a raid is a new day");
                var tomorrow = Dailies();
                t.Ok(tomorrow.Count == 2 && tomorrow.All(pool.Contains), "the next day two dailies of the pool are offered");
            }

            // ---- waiting, slots and taking a daily again ----
            using (var world = new World())
            {
                SetSlots(world.Giver, 5);
                var daily = world.Quest("Daily");
                world.Objective(daily, QuestObjectiveKind.Deliver, papers, 1);
                SetDaily(daily, repeatable: true, after: 2);
                var follow = world.Quest("AfterDaily", 1, daily);
                world.Objective(follow, QuestObjectiveKind.Deliver, papers, 1);

                var backpack = world.Inventory();
                QuestLog.Accept(daily);
                backpack.Add(papers, 1);
                QuestLog.TryTurnIn(daily, backpack);
                QuestLog.CountRaid();
                t.Eq(QuestLog.StatusOf(daily), QuestStatus.TurnedIn, "a daily that waits two raids is not back after one");
                QuestLog.CountRaid();
                t.Eq(QuestLog.StatusOf(daily), QuestStatus.Available, "and is back after two");
                t.Ok(QuestLog.Accept(daily), "it can be taken again");
                t.Eq(world.Data.quests.Count(r => r.quest == "Daily"), 1, "in its old entry");
                t.Eq(QuestLog.StatusOf(follow), QuestStatus.Available, "a quest that requires the daily stays open while it is taken again");
                backpack.Add(papers, 1);
                QuestLog.TryTurnIn(daily, backpack);
                t.Eq(world.Data.quests.First(r => r.quest == "Daily").done, 2, "turned in twice");
            }

            using (var world = new World())
            {
                SetSlots(world.Giver, 1);
                foreach (string id in new[] { "DailyX", "DailyY" })
                {
                    var daily = world.Quest(id);
                    world.Objective(daily, QuestObjectiveKind.Deliver, papers, 1);
                    SetDaily(daily, repeatable: true);
                }
                var taken = Dailies().Single();
                QuestLog.Accept(taken);
                QuestLog.CountRaid();
                t.Eq(Dailies().Count, 0, "a daily taken and not done keeps its slot the next day");
                var backpack = world.Inventory();
                backpack.Add(papers, 1);
                QuestLog.TryTurnIn(taken, backpack);
                QuestLog.CountRaid();
                t.Eq(Dailies().Count, 1, "once it is done the slot is free again");
            }

            // ---- refusing ----
            using (var world = new World())
            {
                var story = world.Quest("Story");
                world.Objective(story, QuestObjectiveKind.Deliver, papers, 1);
                var refusable = world.Quest("Refusable", 1);
                world.Objective(refusable, QuestObjectiveKind.VisitRoom, null, 1, RoomKind.Parking);
                SetDaily(refusable, canAbandon: true, loyalty: 6);
                Loyalty.Add("TestGiver", 10);

                t.Ok(!QuestLog.Abandon(refusable), "a quest that is not taken cannot be refused");
                QuestLog.Accept(story);
                t.Ok(!QuestLog.CanAbandon(story) && !QuestLog.Abandon(story), "a story quest cannot be refused");
                t.Eq(QuestLog.StatusOf(story), QuestStatus.Active, "and stays taken");

                QuestLog.Accept(refusable);
                QuestLog.CommitVisits(new[] { RoomKind.Parking });
                t.Ok(QuestLog.Check(refusable, world.Inventory()).Ready, "the place was reached");
                t.Ok(QuestLog.Abandon(refusable), "a quest marked so can be refused");
                t.Eq(Loyalty.Points("TestGiver"), 4, "the giver takes off as much loyalty as the quest gives");
                t.Eq(QuestLog.StatusOf(refusable), QuestStatus.Locked, "it is not offered the same day");
                QuestLog.CountRaid();
                t.Eq(QuestLog.StatusOf(refusable), QuestStatus.Available, "but the next day");
                QuestLog.Accept(refusable);
                t.Ok(!QuestLog.Check(refusable, world.Inventory()).Ready, "taken again it starts from scratch");

                SetDaily(refusable, canAbandon: true, penalty: 2, loyalty: 6);
                QuestLog.Abandon(refusable);
                t.Eq(Loyalty.Points("TestGiver"), 2, "a penalty of its own is taken off instead");

                var rescue = world.Quest("Rescue", 2);
                world.Objective(rescue, QuestObjectiveKind.Rescue, null, 1);
                SetDaily(rescue, repeatable: true, canAbandon: true);
                t.Ok(!rescue.Repeatable, "a rescue is never a daily");
                QuestLog.Accept(rescue);
                t.Ok(!QuestLog.CanAbandon(rescue), "and cannot be refused");
            }

            // ---- refusing in the talk ----
            using (var world = new World())
            {
                var story = world.Quest("Story");
                world.Objective(story, QuestObjectiveKind.Deliver, papers, 1);
                var refusable = world.Quest("Refusable", 1);
                world.Objective(refusable, QuestObjectiveKind.Deliver, papers, 2);
                SetDaily(refusable, canAbandon: true, loyalty: 3);
                QuestLog.Accept(story);
                QuestLog.Accept(refusable);
                var back = new DialogueNode { Text = "root" };
                var work = QuestDialogue.TakenChoice("TestGiver", world.Inventory(), back);
                work.Action();
                var menu = work.Next;

                var (storyPage, _) = Choose(menu, "Напомни: Story");
                while (storyPage != null && storyPage.Choices.Any(c => c.Text == "Дальше")) storyPage = Choose(storyPage, "Дальше").next;
                t.Ok(storyPage != null && Labels(storyPage) == "Понятно", "a story quest is only reminded of");

                var (page, _) = Choose(menu, "Напомни: Refusable");
                while (page != null && page.Choices.Any(c => c.Text == "Дальше")) page = Choose(page, "Дальше").next;
                t.Ok(page != null && Labels(page) == "Понятно|Отказаться", "a quest that may be refused has 'refuse' under its reminder");
                var (ask, _) = Choose(page, "Отказаться");
                t.Ok(ask != null && ask.Text.Contains("Отказаться от «Refusable»?") && Labels(ask) == "Да, отказываюсь|Нет, сделаю",
                    "the player is asked to confirm, and the cost is not named");
                var (kept, _) = Choose(ask, "Нет, сделаю");
                t.Ok(kept != null && QuestLog.StatusOf(refusable) == QuestStatus.Active, "'no' keeps the quest");
                var (said, _) = Choose(ask, "Да, отказываюсь");
                t.Ok(said != null && said.Text.Contains("Найду кого-нибудь другого") && QuestLog.StatusOf(refusable) == QuestStatus.Locked,
                    "'yes' refuses it, and the giver says so");
            }

            // ---- the save ----
            var data = new SaveData { raids = 5 };
            data.dailies.Add(new DailyOfferData { giver = "G", day = 5, quests = new List<string> { "A" } });
            data.quests.Add(new QuestProgressData { quest = "A", state = 3, again = 6, done = 2 });
            var copy = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            t.Ok(copy.raids == 5 && copy.dailies.Count == 1 && copy.dailies[0].day == 5 && copy.dailies[0].quests.SequenceEqual(new[] { "A" })
                && copy.quests[0].state == 3 && copy.quests[0].again == 6 && copy.quests[0].done == 2, "days, dailies and refusals survive the save file");
            var old = JsonUtility.FromJson<SaveData>("{\"version\":4,\"quests\":[{\"quest\":\"Q\",\"state\":2}]}");
            t.Ok(old.raids == 0 && old.dailies != null && old.quests[0].again == 0 && old.quests[0].done == 0, "an older save reads with no days and no dailies");

            // ---- the validator ----
            using (var world = new World())
            {
                var facts = TestFacts(papers);
                var rescue = world.Quest("Rescue");
                world.Objective(rescue, QuestObjectiveKind.Rescue, null, 1);
                world.Reward(rescue, 5);
                SetDaily(rescue, repeatable: true, canAbandon: true);
                var daily = world.Quest("Daily", 1);
                world.Objective(daily, QuestObjectiveKind.Deliver, papers, 1);
                world.Reward(daily, 5);
                SetDaily(daily, repeatable: true);
                SetSlots(world.Giver, 0);
                string errors = Errors(world, facts);
                t.Ok(errors.Contains("отказаться нельзя") && errors.Contains("не может быть дейликом"), "a rescue that may be refused or repeated is an error");
                t.Ok(Warnings(world, facts).Contains("Daily Slots"), "dailies of a giver with no daily slots are a warning");
            }
        }
    }
}
