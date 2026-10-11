using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Quests;

namespace ProjectMayham.EditorTools
{
    public static partial class QuestSelfTest
    {
        static partial void LogTests(Checks t)
        {
            var papers = Item("Papers");
            var mug = Item("Mug");
            var mop = Item("Mop");

            // States, prerequisites, a plain turn-in.
            using (var world = new World())
            {
                var first = world.Quest("First", 1);
                world.Objective(first, QuestObjectiveKind.Deliver, papers, 3);
                world.Reward(first, 30, mug, 1);
                var second = world.Quest("Second", 2, first);
                var backpack = world.Inventory();

                t.Eq(QuestLog.StatusOf(first), QuestStatus.Available, "no prerequisites: available");
                t.Eq(QuestLog.StatusOf(second), QuestStatus.Locked, "prerequisite not turned in: locked");
                t.Eq(string.Join(",", QuestLog.Offers("TestGiver").Select(q => q.Id)), "First", "only the available quest is offered");
                t.Ok(QuestLog.Accept(first), "accept");
                t.Ok(!QuestLog.Accept(first), "accepting twice changes nothing");
                t.Ok(!QuestLog.Accept(second), "a locked quest cannot be taken");
                t.Eq(world.Data.quests.Count, 1, "one entry in the save");
                t.Eq(QuestLog.StatusOf(first), QuestStatus.Active, "active after accepting");
                t.Eq(QuestLog.ActiveOf("TestGiver").Count, 1, "one active quest");

                backpack.Add(papers, 2);
                var progress = QuestLog.Check(first, backpack);
                t.Ok(!progress.Ready && progress.Lines[0].Have == 2 && progress.Lines[0].Need == 3, "2 of 3 is not ready");
                t.Eq(QuestLog.TryTurnIn(first, backpack), TurnInResult.NotReady, "turn-in is refused while something is missing");
                t.Eq(backpack.CountOf(papers), 2, "a refused turn-in takes nothing");

                backpack.Add(papers, 3);
                int money = world.Data.money;
                t.Eq(QuestLog.TryTurnIn(first, backpack), TurnInResult.Done, "turn-in");
                t.Eq(backpack.CountOf(papers), 2, "exactly what was asked is taken");
                t.Eq(backpack.CountOf(mug), 1, "the reward item is in the backpack");
                t.Eq(world.Data.money, money + 30, "the reward money is paid");
                t.Eq(QuestLog.StatusOf(first), QuestStatus.TurnedIn, "turned in");
                t.Eq(QuestLog.TryTurnIn(first, backpack), TurnInResult.NotActive, "a quest is turned in once");
                t.Eq(QuestLog.StatusOf(second), QuestStatus.Available, "the next quest of the chain opens");
            }

            // The item in the hands counts and is taken.
            using (var world = new World())
            {
                var quest = world.Quest("Mugs");
                world.Objective(quest, QuestObjectiveKind.Deliver, mug, 2);
                var backpack = world.Inventory();
                backpack.Add(mug, 2);
                t.Ok(backpack.Hold(backpack.Stacks[0]), "a mug goes into the hands");
                t.Eq(backpack.CountOf(mug), 1, "CountOf leaves the hands out");
                t.Eq(QuestLog.Carried(backpack, mug), 2, "Carried counts the hands");
                QuestLog.Accept(quest);
                t.Ok(QuestLog.Check(quest, backpack).Ready, "ready with one mug in the hands");
                t.Eq(QuestLog.TryTurnIn(quest, backpack), TurnInResult.Done, "turn-in takes the mug from the hands");
                t.Ok(backpack.HeldItem == null && backpack.CountOf(mug) == 0, "nothing is left");
            }

            // A reward that does not fit: nothing changes, the hands included.
            using (var world = new World())
            {
                var quest = world.Quest("Heavy");
                world.Objective(quest, QuestObjectiveKind.Deliver, mug, 2);
                world.Reward(quest, 50, mop, 20);   // 20 mops weigh 50 kg, the backpack takes 25
                var backpack = world.Inventory();
                backpack.Add(mug, 2);
                backpack.Hold(backpack.Stacks[0]);
                QuestLog.Accept(quest);
                int money = world.Data.money;
                t.Eq(QuestLog.TryTurnIn(quest, backpack), TurnInResult.NoRoom, "the reward does not fit");
                t.Eq(QuestLog.Carried(backpack, mug), 2, "the mugs are back");
                t.Ok(backpack.HeldItem == mug, "the mug is back in the hands");
                t.Eq(backpack.CountOf(mop), 0, "no part of the reward stays");
                t.Eq(world.Data.money, money, "no money is paid");
                t.Eq(QuestLog.StatusOf(quest), QuestStatus.Active, "the quest stays active");
            }

            // A reward line whose amount was left at 0 in the Inspector still gives one piece, as it says.
            using (var world = new World())
            {
                var quest = world.Quest("ZeroAmount");
                world.Reward(quest, 0, mug, 0);
                var backpack = world.Inventory();
                QuestLog.Accept(quest);
                t.Eq(QuestText.Reward(quest), "Кружка", "a reward line with amount 0 is announced as one piece");
                t.Eq(QuestLog.TryTurnIn(quest, backpack), TurnInResult.Done, "turn-in with a reward amount of 0");
                t.Eq(backpack.CountOf(mug), 1, "and one piece is really given");
            }

            // Places: marked only for active quests, once, and kept.
            using (var world = new World())
            {
                var visit = world.Quest("Visit");
                world.Objective(visit, QuestObjectiveKind.VisitRoom, null, 1, RoomKind.Parking);
                var other = world.Quest("Other");
                world.Objective(other, QuestObjectiveKind.VisitRoom, null, 1, RoomKind.Parking);
                var backpack = world.Inventory();
                QuestLog.Accept(visit);
                t.Ok(!QuestLog.Check(visit, backpack).Ready, "not visited yet");
                QuestLog.CommitVisits(new[] { RoomKind.Office });
                t.Ok(!QuestLog.Check(visit, backpack).Ready, "another kind of room does not count");
                QuestLog.CommitVisits(new[] { RoomKind.Parking, RoomKind.Parking });
                QuestLog.CommitVisits(new[] { RoomKind.Parking });
                t.Eq(world.Data.quests[0].marks.Count, 1, "the mark is written once");
                t.Ok(QuestLog.Check(visit, backpack).Ready, "visited");
                QuestLog.Accept(other);
                t.Ok(!QuestLog.Check(other, backpack).Ready, "a place reached before the quest was taken does not count");
                t.Eq(QuestLog.TryTurnIn(visit, backpack), TurnInResult.Done, "a visit quest is turned in without items");
            }

            // What the raid has to contain.
            using (var world = new World())
            {
                var special = world.Make<ItemDefinition>("TestSpecial");
                var find = world.Quest("Find", 1);
                world.Objective(find, QuestObjectiveKind.FindSpecial, special, 1, RoomKind.BossOffice);
                var visit = world.Quest("Visit", 2);
                world.Objective(visit, QuestObjectiveKind.VisitRoom, null, 1, RoomKind.Parking);
                var deliver = world.Quest("Deliver", 3);
                world.Objective(deliver, QuestObjectiveKind.Deliver, papers, 3);

                t.Ok(QuestLog.BuildRaidDemands().IsEmpty, "nothing is asked for while no quest is active");
                QuestLog.Accept(visit);
                QuestLog.Accept(find);
                QuestLog.Accept(deliver);
                var demands = QuestLog.BuildRaidDemands();
                t.Eq(string.Join(",", demands.Rooms()), "Parking,BossOffice", "rooms in the order the quests were taken");
                t.Ok(demands.Items.Count == 1 && demands.Items[0].Item == special && demands.Items[0].Count == 1
                     && demands.Items[0].Room == RoomKind.BossOffice, "the special item is asked for");
                t.Ok(demands.WantsVisit(RoomKind.Parking) && !demands.WantsVisit(RoomKind.BossOffice), "the place to visit");

                world.Data.stash.stacks.Add(new StackData { item = "TestSpecial", count = 1 });
                t.Eq(QuestLog.BuildRaidDemands().Items.Count, 0, "an item lying in the stash is not placed again");
                world.Data.stash.stacks.Clear();
                world.Data.player.held = "TestSpecial";
                t.Eq(QuestLog.BuildRaidDemands().Items.Count, 0, "nor one carried in the hands");
                world.Data.player.held = null;

                QuestLog.CommitVisits(new[] { RoomKind.Parking });
                t.Ok(!QuestLog.BuildRaidDemands().WantsVisit(RoomKind.Parking), "a place already brought home is not asked for again");
            }

            // Review focus 1: a save from before quests, and entries that mean nothing.
            using (var world = new World())
            {
                var plain = world.Quest("Plain");
                world.Data.quests = null;
                t.Eq(QuestLog.StatusOf(plain), QuestStatus.Available, "old save: the quest is offered");
                t.Ok(QuestLog.BuildRaidDemands().IsEmpty, "old save: nothing is asked of the raid");
                t.Ok(QuestLog.Accept(plain) && world.Data.quests != null && world.Data.quests.Count == 1, "old save: accepting creates the list");

                world.Data.quests.Add(new QuestProgressData { quest = "Deleted quest", state = 1 });
                world.Data.quests.Add(new QuestProgressData { quest = "Plain", state = 2 });
                world.Data.quests.Add(new QuestProgressData { quest = "Plain", state = 7, marks = null });
                world.Data.quests.Add(null);
                t.Eq(QuestLog.StatusOf(plain), QuestStatus.Active, "the first entry of a quest wins");
                QuestLog.CommitVisits(new[] { RoomKind.Office });
                t.Ok(QuestLog.BuildRaidDemands().IsEmpty, "odd entries ask nothing of the raid");
                t.Eq(world.Data.quests.Count, 5, "entries the game does not understand are kept");
            }

            // Review focus 2: prerequisites that form a cycle must not hang the game.
            using (var world = new World())
            {
                var a = world.Quest("A");
                var b = world.Quest("B", 0, a);
                var so = new UnityEditor.SerializedObject(a);
                var list = so.FindProperty("requires");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = b;
                so.ApplyModifiedPropertiesWithoutUndo();
                t.Eq(QuestLog.StatusOf(a), QuestStatus.Locked, "cycle: locked, not a hang");
                t.Eq(QuestLog.StatusOf(b), QuestStatus.Locked, "cycle: the other one too");
                t.Eq(QuestLog.Offers("TestGiver").Count, 0, "cycle: nothing is offered");
            }

            // The demands themselves.
            var merged = new RaidDemands();
            merged.AddItem(papers, 1, RoomKind.Office);
            merged.AddItem(papers, 3, RoomKind.Storage);
            merged.AddItem(null, 2, RoomKind.Toilet);
            merged.AddVisit(RoomKind.Office);
            merged.AddVisit(RoomKind.Office);
            t.Ok(merged.Items.Count == 1 && merged.Items[0].Count == 3 && merged.Items[0].Room == RoomKind.Office,
                "the same item asked twice: one demand, the larger amount, the first room");
            t.Eq(string.Join(",", merged.Rooms()), "Office", "every kind of room once");
            t.Ok(new RaidDemands().IsEmpty && !merged.IsEmpty, "empty and not empty");
        }
    }
}
