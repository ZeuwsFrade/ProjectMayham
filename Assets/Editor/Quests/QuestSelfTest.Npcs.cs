using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Dialogue;
using ProjectMayham.Items;
using ProjectMayham.Npcs;
using ProjectMayham.Quests;
using ProjectMayham.Trade;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    public static partial class QuestSelfTest
    {
        // Picks the answer with this text; returns the node it leads to and whether the window would close.
        private static (DialogueNode next, bool closes) Choose(DialogueNode node, string text)
        {
            var choice = node.Choices.FirstOrDefault(c => c.Text == text);
            if (choice == null) return (null, false);
            choice.Action?.Invoke();
            return (choice.Next, choice.Closes);
        }

        private static NpcDefinition Npc(World world, string id, DialogueDefinition dialogue, TraderDefinition trader = null)
        {
            var npc = world.Make<NpcDefinition>(id);
            var so = new SerializedObject(npc);
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("dialogue").objectReferenceValue = dialogue;
            so.FindProperty("trader").objectReferenceValue = trader;
            so.ApplyModifiedPropertiesWithoutUndo();
            return npc;
        }

        // A person item of 6x2 and 15 kg, and the NPC "TestGiver" (the giver of the test quests) carried as it.
        private static (ItemDefinition person, NpcDefinition npc) LostGiver(World world, bool lostInMaze = true)
        {
            var person = world.Make<ItemDefinition>("TestPerson");
            var so = new SerializedObject(person);
            so.FindProperty("displayName").stringValue = "Человек";
            so.FindProperty("gridSize").vector2IntValue = new Vector2Int(6, 2);
            so.FindProperty("weight").floatValue = 15f;
            so.FindProperty("holdable").boolValue = true;
            so.FindProperty("isPerson").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            var npc = world.Make<NpcDefinition>("TestGiver");
            var nso = new SerializedObject(npc);
            nso.FindProperty("lostInMaze").boolValue = lostInMaze;
            nso.FindProperty("carryItem").objectReferenceValue = person;
            nso.ApplyModifiedPropertiesWithoutUndo();
            return (person, npc);
        }

        static partial void NpcTests(Checks t)
        {
            var papers = Item("Papers");
            var mug = Item("Mug");

            // ---- loyalty ----
            using (var world = new World())
            {
                var levels = new[] { 10, 25, 50 };
                t.Eq(Loyalty.LevelFor(levels, 0), 1, "no points is level 1");
                t.Eq(Loyalty.LevelFor(levels, 9), 1, "nine points are still level 1");
                t.Eq(Loyalty.LevelFor(levels, 10), 2, "ten points reach level 2");
                t.Eq(Loyalty.LevelFor(levels, 49), 3, "49 points are level 3");
                t.Eq(Loyalty.LevelFor(levels, 500), 4, "and there is no level above 4");

                t.Eq(Loyalty.Points("TestGiver"), 0, "an NPC nobody worked for has no points");
                Loyalty.Add("TestGiver", 12);
                t.Eq(Loyalty.Level("TestGiver"), 2, "points add up to a level (default thresholds without an NPC asset)");
                Loyalty.Add("TestGiver", -100);
                t.Eq(Loyalty.Points("TestGiver"), 0, "points never go below zero");

                // A quest that asks for loyalty, and one that gives it.
                var trusted = world.Quest("Trusted");
                var so = new SerializedObject(trusted);
                so.FindProperty("minLoyalty").intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
                world.Objective(trusted, QuestObjectiveKind.Deliver, papers, 1);
                t.Eq(QuestLog.StatusOf(trusted), QuestStatus.Locked, "a quest for loyalty 2 is locked at level 1");

                var earner = world.Quest("Earner");
                world.Objective(earner, QuestObjectiveKind.Deliver, papers, 1);
                var reward = new SerializedObject(earner);
                reward.FindProperty("rewardLoyalty").intValue = 10;
                reward.ApplyModifiedPropertiesWithoutUndo();
                t.Eq(QuestText.Reward(earner), string.Empty, "loyalty is hidden: a quest that gives only loyalty shows no reward");

                var backpack = world.Inventory();
                backpack.Add(papers, 1);
                QuestLog.Accept(earner);
                t.Eq(QuestLog.TryTurnIn(earner, backpack), TurnInResult.Done, "the quest is handed in");
                t.Eq(Loyalty.Points("TestGiver"), 10, "and its giver trusts the player more");
                t.Eq(QuestLog.StatusOf(trusted), QuestStatus.Available, "which opens the quest for loyalty 2");
            }

            // ---- a quest line that forks on an answer ----
            using (var world = new World())
            {
                var help = world.Quest("Help");
                var report = world.Quest("Report");
                var so = new SerializedObject(help);
                so.FindProperty("requiredChoices").arraySize = 1;
                so.FindProperty("requiredChoices").GetArrayElementAtIndex(0).stringValue = "helped";
                so.ApplyModifiedPropertiesWithoutUndo();
                so = new SerializedObject(report);
                so.FindProperty("excludingChoices").arraySize = 1;
                so.FindProperty("excludingChoices").GetArrayElementAtIndex(0).stringValue = "helped";
                so.ApplyModifiedPropertiesWithoutUndo();

                t.Eq(QuestLog.StatusOf(help), QuestStatus.Locked, "a branch that needs an answer is locked before it");
                t.Eq(QuestLog.StatusOf(report), QuestStatus.Available, "the other branch is open");
                PlayerChoices.Remember("helped");
                t.Ok(PlayerChoices.Has("helped"), "the answer is remembered");
                t.Eq(QuestLog.StatusOf(help), QuestStatus.Available, "the first branch opens");
                t.Eq(QuestLog.StatusOf(report), QuestStatus.Locked, "the other closes for good");
                t.Ok(world.Data.choices.Count == 1, "an answer remembered twice is written once");
                PlayerChoices.Remember("helped");
                t.Eq(world.Data.choices.Count, 1, "still once");
            }

            // ---- a conversation from a dialogue asset ----
            using (var world = new World())
            {
                var dialogue = world.Make<DialogueDefinition>("TestDialogue");
                var quest = world.Quest("Job");
                world.Objective(quest, QuestObjectiveKind.Deliver, papers, 1);
                NpcSamples.Fill(dialogue, new List<NpcSamples.Line>
                {
                    new NpcSamples.Line
                    {
                        Id = "friend", Start = true, MinLoyalty = 2, Text = "Привет, друг.",
                        Answers = { new NpcSamples.Answer { Text = "Пока" } },
                    },
                    new NpcSamples.Line
                    {
                        Id = "start", Start = true, Text = "Привет.",
                        Answers =
                        {
                            new NpcSamples.Answer { Text = "Помогу", Next = "thanks", Actions = { (DialogueActionKind.RememberChoice, "helped", 0), (DialogueActionKind.AddLoyalty, "", 3) } },
                            new NpcSamples.Answer { Text = "Секрет", Next = "start", Conditions = { (DialogueConditionKind.ChoiceMade, 1, "helped") } },
                            new NpcSamples.Answer { Text = "Работа", Actions = { (DialogueActionKind.OpenWork, "", 0) } },
                            new NpcSamples.Answer { Text = "Пока" },
                        },
                    },
                    new NpcSamples.Line
                    {
                        Id = "thanks", Text = "Раз.\nДва.\nТри.\nЧетыре.",
                        Answers = { new NpcSamples.Answer { Text = "Назад", Next = "start" } },
                    },
                    new NpcSamples.Line
                    {
                        Id = "many", Text = "Выбирай.",
                        Answers =
                        {
                            new NpcSamples.Answer { Text = "1" }, new NpcSamples.Answer { Text = "2" }, new NpcSamples.Answer { Text = "3" },
                            new NpcSamples.Answer { Text = "4" }, new NpcSamples.Answer { Text = "5" },
                        },
                    },
                });
                var npc = Npc(world, "TestGiver", dialogue);
                NpcCatalog.Override(new[] { npc });
                try
                {
                    var context = new ConversationContext { Npc = npc, Backpack = world.Inventory() };
                    var start = NpcConversation.Start(context);
                    t.Eq(start.Text, "Привет.", "at loyalty 1 the conversation starts with the plain greeting");
                    t.Eq(Labels(start), "Помогу|Работа|Пока", "an answer whose condition does not hold does not show");

                    var (thanks, closes) = Choose(start, "Помогу");
                    t.Ok(!closes && thanks != null, "an answer leads to its line");
                    t.Ok(PlayerChoices.Has("helped"), "and remembers the choice");
                    t.Eq(Loyalty.Points("TestGiver"), 3, "and adds the loyalty it gives");
                    t.Eq(thanks.Text, "Раз.\nДва.\nТри.", "a line of four rows is paged");
                    t.Eq(Labels(thanks), "Дальше", "with 'next' on the first page");
                    var (second, _) = Choose(thanks, "Дальше");
                    t.Eq(second.Text, "Четыре.", "the second page has the rest");
                    var (back, _) = Choose(second, "Назад");
                    t.Eq(Labels(back), "Помогу|Секрет|Работа|Пока", "an answer that needs the remembered choice shows now");

                    var (work, _) = Choose(back, "Работа");
                    t.Ok(work != null && work.Choices.Any(c => c.Text == "Новое дело: Job"), "'work' opens the list of quests of the NPC");
                    var (bye, gone) = Choose(back, "Пока");
                    t.Ok(gone && bye == null, "an answer without a next line ends the conversation");

                    Loyalty.Add("TestGiver", 20);
                    t.Eq(NpcConversation.Start(context).Text, "Привет, друг.", "at loyalty 2 the friendlier start comes first");

                    var many = new ConversationContext { Npc = npc, Backpack = context.Backpack };
                    var line = dialogue.Find("many");
                    t.Ok(line != null, "a line is found by its name");
                    var manyNode = typeof(NpcConversation).GetMethod("LineNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                        .Invoke(null, new object[] { many, line }) as DialogueNode;
                    t.Eq(Labels(manyNode), "1|2|3|4|5", "five answers are all shown: the window scrolls");
                }
                finally
                {
                    NpcCatalog.Reset();
                }
            }

            // ---- trade by loyalty ----
            using (var world = new World())
            {
                var trader = world.Make<TraderDefinition>("TestGiver");
                var so = new SerializedObject(trader);
                var stock = so.FindProperty("stock");
                stock.arraySize = 2;
                stock.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = papers;
                stock.GetArrayElementAtIndex(0).FindPropertyRelative("count").intValue = 10;
                stock.GetArrayElementAtIndex(0).FindPropertyRelative("minLoyalty").intValue = 1;
                stock.GetArrayElementAtIndex(1).FindPropertyRelative("item").objectReferenceValue = mug;
                stock.GetArrayElementAtIndex(1).FindPropertyRelative("count").intValue = 1;
                stock.GetArrayElementAtIndex(1).FindPropertyRelative("minLoyalty").intValue = 2;
                so.FindProperty("discountPerLevel").floatValue = 0.1f;
                so.ApplyModifiedPropertiesWithoutUndo();

                var book = new TraderBook(trader);
                try
                {
                    t.Ok(book.Offered.Any(w => w.Item == papers) && book.Offered.All(w => w.Item != mug), "a ware of loyalty 2 is not offered at level 1");
                    world.Data.money = 1000;
                    t.Ok(!book.TryBuy(world.Inventory(), mug, 1, out string error) && error.Length > 0, "and cannot be bought");
                    int full = book.BuyPrice(papers);
                    Loyalty.Add("TestGiver", 25);
                    t.Eq(Loyalty.Level("TestGiver"), 3, "25 points are level 3");
                    t.Ok(book.Offered.Any(w => w.Item == mug), "at level 2 and above it is offered");
                    t.Ok(book.BuyPrice(papers) <= full, "the price does not grow with loyalty");
                    t.Eq(book.BuyPrice(mug), Mathf.Max(1, Mathf.CeilToInt(mug.Value * trader.SellMarkup * 0.8f - 0.0001f)), "at level 3 the price is 20% off");
                }
                finally
                {
                    book.Dispose();
                }
            }

            // ---- a lost NPC: carried, rescued, never sold ----
            using (var world = new World())
            {
                var person = world.Make<ItemDefinition>("TestPerson");
                var so = new SerializedObject(person);
                so.FindProperty("displayName").stringValue = "Человек";
                so.FindProperty("gridSize").vector2IntValue = new Vector2Int(6, 2);
                so.FindProperty("weight").floatValue = 15f;
                so.FindProperty("holdable").boolValue = true;
                so.FindProperty("isPerson").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                var books = Item("Books");

                var roomy = world.Inventory();
                t.Ok(LostNpc.TryCarry(roomy, person) == null, "a person goes into a backpack with room for 6x2");
                t.Eq(roomy.CountOf(person), 1, "and lies in the grid");
                t.Ok(Mathf.Approximately(roomy.TotalWeight, 15f), "weighing 15 kg");

                // Two papers in the fourth column block every 6x2 place of an 8x5 grid.
                var full = world.Inventory();
                full.PlaceItem(papers, 1, new Vector2Int(3, 1), false);
                full.PlaceItem(papers, 1, new Vector2Int(3, 3), false);
                t.Ok(!full.CanFit(person, 1), "a backpack without a free 6x2 has no room for a person");
                t.Ok(LostNpc.TryCarry(full, person) == null, "then the person is carried in the hands");
                t.Ok(full.HeldItem == person, "the hands hold them");
                string second = LostNpc.TryCarry(full, person);
                t.Ok(!string.IsNullOrEmpty(second) && second.Contains("тяжело"), "a second person of 15 kg is too heavy");
                var light = world.Make<ItemDefinition>("LightPerson");
                var lso = new SerializedObject(light);
                lso.FindProperty("gridSize").vector2IntValue = new Vector2Int(6, 2);
                lso.FindProperty("weight").floatValue = 1f;
                lso.FindProperty("holdable").boolValue = true;
                lso.FindProperty("isPerson").boolValue = true;
                lso.ApplyModifiedPropertiesWithoutUndo();
                string busy = LostNpc.TryCarry(full, light);
                t.Ok(!string.IsNullOrEmpty(busy) && busy.Contains("6×2"), "with busy hands and no room there is nowhere to take one");
                t.Ok(full.HeldItem == person && full.CountOf(light) == 0, "and nothing changes");

                var heavy = world.Inventory();
                heavy.Add(books, 6);
                t.Ok(heavy.TotalWeight > 10f, "more than ten kg of books");
                string tooHeavy = LostNpc.TryCarry(heavy, person);
                t.Ok(!string.IsNullOrEmpty(tooHeavy) && tooHeavy.Contains("тяжело"), "a person over the weight limit is refused");
                t.Eq(heavy.CountOf(person), 0, "and is not taken");

                var npc = world.Make<NpcDefinition>("TestLost");
                var nso = new SerializedObject(npc);
                nso.FindProperty("lostInMaze").boolValue = true;
                nso.FindProperty("carryItem").objectReferenceValue = person;
                nso.ApplyModifiedPropertiesWithoutUndo();
                NpcCatalog.Override(new[] { npc });
                try
                {
                    var carried = NpcRescue.TakeCarried(full);
                    t.Ok(carried.Count == 1 && carried[0] == npc, "home, the carried person is found as their NPC");
                    t.Ok(full.HeldItem == null, "and taken out of the hands");
                    var inGrid = NpcRescue.TakeCarried(roomy);
                    t.Ok(inGrid.Count == 1 && roomy.CountOf(person) == 0, "or out of the grid");
                    t.Ok(!NpcRescue.IsRescued("TestLost"), "not rescued before it is written down");
                    NpcRescue.Rescue("TestLost");
                    NpcRescue.Rescue("TestLost");
                    t.Ok(NpcRescue.IsRescued("TestLost") && world.Data.rescued.Count == 1, "rescued once and for good");
                    t.Ok(NpcRescue.IsInShelter("TestLost") && world.Data.shelterNpcs.Count == 1, "a rescued NPC is in the shelter, written down once");
                    NpcRescue.SetInShelter("TestLost", false);
                    t.Ok(!NpcRescue.IsInShelter("TestLost") && NpcRescue.IsRescued("TestLost"), "an NPC that left is away but still rescued");
                    var reloaded = JsonUtility.FromJson<ProjectMayham.Core.SaveData>(JsonUtility.ToJson(world.Data));
                    t.Ok(reloaded.shelterNpcs.Count == 1 && !reloaded.shelterNpcs[0].present, "their absence survives the save file");
                    NpcRescue.SetInShelter("Nobody", true);
                    t.Eq(world.Data.shelterNpcs.Count, 1, "a stranger is not written down");
                    NpcRescue.Rescue("TestLost");
                    t.Ok(NpcRescue.IsInShelter("TestLost") && world.Data.rescued.Count == 1, "bringing them home again puts them back");
                    var legacy = new ProjectMayham.Core.SaveData { version = 3 };
                    legacy.rescued.Add("Old");
                    legacy.Upgrade();
                    t.Ok(legacy.shelterNpcs.Count == 1 && legacy.shelterNpcs[0].present, "an old save: everyone rescued is in the shelter");

                    var trader = world.Make<TraderDefinition>("TestTrader");
                    var book = new TraderBook(trader);
                    try
                    {
                        var seller = world.Inventory();
                        seller.Add(person, 1);
                        t.Ok(!book.TrySell(seller, person, 1, out string error) && error.Contains("Людей"), "a person cannot be sold");
                        t.Eq(seller.CountOf(person), 1, "and stays with the player");
                    }
                    finally
                    {
                        book.Dispose();
                    }

                    // In the raid and at the base, and going along.
                    var dialogue = world.Make<DialogueDefinition>("LostTalk");
                    NpcSamples.Fill(dialogue, new List<NpcSamples.Line>
                    {
                        new NpcSamples.Line
                        {
                            Id = "raid", Start = true, Conditions = { (DialogueConditionKind.InRaid, 1, "") }, Text = "В лабиринте.",
                            Answers = { new NpcSamples.Answer { Text = "Пойдём", Next = "raid", Actions = { (DialogueActionKind.TakeAlong, "", 0) } } },
                        },
                        new NpcSamples.Line { Id = "base", Start = true, Conditions = { (DialogueConditionKind.AtBase, 1, "") }, Text = "На базе." },
                    });
                    nso = new SerializedObject(npc);
                    nso.FindProperty("dialogue").objectReferenceValue = dialogue;
                    nso.ApplyModifiedPropertiesWithoutUndo();
                    string answer = "Не пойду.";
                    var raid = new ConversationContext { Npc = npc, Backpack = world.Inventory(), InRaid = true, TakeAlong = () => answer };
                    var start = NpcConversation.Start(raid);
                    t.Eq(start.Text, "В лабиринте.", "in the raid the raid line starts");
                    t.Eq(NpcConversation.Start(new ConversationContext { Npc = npc, Backpack = raid.Backpack }).Text, "На базе.", "at the base the base line");
                    var (refused, _) = Choose(start, "Пойдём");
                    t.Ok(refused != null && refused.Text == "Не пойду." && Labels(refused) == "Ясно", "a refusal is said, with an answer to go back");
                    answer = null;
                    var (gone, closes) = Choose(start, "Пойдём");
                    t.Ok(gone == null && closes, "going along ends the conversation");

                    var unready = world.Make<NpcDefinition>("Unready");
                    var uso = new SerializedObject(unready);
                    uso.FindProperty("lostInMaze").boolValue = true;
                    uso.ApplyModifiedPropertiesWithoutUndo();
                    string errors = string.Join(" || ", DialogueValidator.Check(new DialogueDefinition[0], new[] { unready }, new QuestDefinition[0])
                        .Where(p => p.IsError).Select(p => p.Message));
                    t.Ok(errors.Contains("LostNpc") && errors.Contains("вещи-человека"), "a lost NPC without a prefab and a person item is an error");
                    var plain = world.Make<NpcDefinition>("Plain");
                    var pso = new SerializedObject(plain);
                    pso.FindProperty("dialogue").objectReferenceValue = dialogue;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    errors = string.Join(" || ", DialogueValidator.Check(new[] { dialogue }, new[] { plain }, new QuestDefinition[0])
                        .Where(p => p.IsError).Select(p => p.Message));
                    t.Ok(errors.Contains("взять с собой"), "taking along an NPC who is not lost is an error");
                }
                finally
                {
                    NpcCatalog.Reset();
                }
            }

            // ---- the rescue is the first quest of a lost NPC ----
            using (var world = new World())
            {
                var (person, npc) = LostGiver(world);
                NpcCatalog.Override(new[] { npc });
                try
                {
                    t.Eq(npc.TakeAlongLevel, 1, "a lost NPC goes along from the first meeting");
                    var rescue = world.Quest("Rescue");
                    world.Objective(rescue, QuestObjectiveKind.Rescue, person, 1);
                    world.Reward(rescue, 0, mug, 1);
                    var rso = new SerializedObject(rescue);
                    rso.FindProperty("rewardLoyalty").intValue = 10;
                    rso.ApplyModifiedPropertiesWithoutUndo();
                    var next = world.Quest("Next", 1, rescue);
                    world.Objective(next, QuestObjectiveKind.Deliver, papers, 1);

                    t.Eq(QuestText.Objectives(rescue), "Довести до убежища: Человек", "the objective names who is led home");
                    t.Eq(QuestLog.StatusOf(rescue), QuestStatus.Available, "the rescue is on offer");
                    NpcRescue.StartRescue("TestGiver");
                    t.Eq(QuestLog.StatusOf(rescue), QuestStatus.Active, "going along takes it");
                    var backpack = world.Inventory();
                    backpack.Add(person, 1);
                    t.Eq(QuestText.Checklist(QuestLog.Check(rescue, backpack)), "Довести до убежища: Человек — ещё нет", "carried is not home yet");
                    t.Eq(QuestLog.TryTurnIn(rescue, backpack), TurnInResult.NotReady, "and cannot be handed in in the raid");
                    t.Eq(QuestLog.BuildRaidDemands().Visits.Count, 0, "the rescue asks nothing of the raid generator");

                    foreach (var home in NpcRescue.TakeCarried(backpack)) NpcRescue.Rescue(home.Id);
                    var results = NpcRescue.FinishRescue("TestGiver", backpack);
                    t.Ok(results.Count == 1 && results[0].result == TurnInResult.Done, "home, the rescue is handed in at once");
                    t.Eq(QuestLog.StatusOf(rescue), QuestStatus.TurnedIn, "and is done");
                    t.Eq(backpack.CountOf(mug), 1, "the reward is in the backpack");
                    t.Eq(Loyalty.Points("TestGiver"), 10, "with the loyalty it gives");
                    t.Eq(QuestLog.StatusOf(next), QuestStatus.Available, "the quests after it open");
                    var outcome = QuestDialogue.Outcome(rescue, TurnInResult.Done);
                    t.Ok(outcome.Text == "Thanks for Rescue" && Labels(outcome) == "Дальше", "the NPC thanks the player");
                    var (received, _) = Choose(outcome, "Дальше");
                    t.Ok(received != null && received.Text.Contains("Получено: Кружка") && received.Choices.Count == 1 && received.Choices[0].Closes,
                        "then says what was received, and the answer closes the window");
                    t.Eq(NpcRescue.FinishRescue("TestGiver", backpack).Count, 0, "a second arrival hands in nothing");
                }
                finally
                {
                    NpcCatalog.Reset();
                }
            }

            // ---- carried home without the quest taken, and no room for the reward ----
            using (var world = new World())
            {
                var (person, npc) = LostGiver(world);
                NpcCatalog.Override(new[] { npc });
                try
                {
                    var wardrobe = world.Make<ItemDefinition>("Wardrobe");
                    var wso = new SerializedObject(wardrobe);
                    wso.FindProperty("gridSize").vector2IntValue = new Vector2Int(8, 5);
                    wso.FindProperty("weight").floatValue = 1f;
                    wso.ApplyModifiedPropertiesWithoutUndo();
                    var rescue = world.Quest("Rescue");
                    world.Objective(rescue, QuestObjectiveKind.Rescue, person, 1);
                    world.Reward(rescue, 0, wardrobe, 1);

                    var backpack = world.Inventory();
                    backpack.Add(papers, 1);
                    NpcRescue.Rescue("TestGiver");
                    var results = NpcRescue.FinishRescue("TestGiver", backpack);
                    t.Ok(results.Count == 1 && results[0].result == TurnInResult.NoRoom, "a reward without room is not handed in");
                    t.Eq(QuestLog.StatusOf(rescue), QuestStatus.Active, "the quest is taken on arrival even if it never was");
                    t.Ok(QuestLog.Check(rescue, backpack).Ready, "and stays ready for a talk");
                    var noRoom = QuestDialogue.Outcome(rescue, TurnInResult.NoRoom);
                    t.Ok(noRoom.Text.Length > 0 && noRoom.Choices.Count == 1 && noRoom.Choices[0].Closes, "the NPC says there is no room");
                    backpack.RemoveItems(papers, 1);
                    t.Eq(QuestLog.TryTurnIn(rescue, backpack), TurnInResult.Done, "with room made it is handed in");

                    string Problems(bool errors, IReadOnlyList<NpcDefinition> npcs, IReadOnlyList<QuestDefinition> quests) =>
                        string.Join(" || ", DialogueValidator.Check(new DialogueDefinition[0], npcs, quests).Where(p => p.IsError == errors).Select(p => p.Message));
                    t.Ok(!Problems(true, new[] { npc }, new[] { rescue }).Contains("Довести"), "a rescue quest of a lost NPC is fine");
                    t.Ok(Problems(false, new[] { npc }, new QuestDefinition[0]).Contains("Довести до убежища"), "a lost NPC without a rescue quest is a warning");
                    var (_, home) = LostGiver(world, false);
                    t.Ok(Problems(true, new[] { home }, new[] { rescue }).Contains("не потерян"), "a rescue quest of an NPC who is not lost is an error");
                }
                finally
                {
                    NpcCatalog.Reset();
                }
            }

            // ---- hiding from enemies ----
            {
                var hiding = new NpcHiding();
                t.Ok(!hiding.Hidden, "a lost NPC sits in the open");
                t.Ok(hiding.Step(true, 0.25f, 8f) && hiding.Hidden, "an enemy near: they hide at once");
                t.Ok(!hiding.Step(false, 5f, 8f) && hiding.Hidden, "five quiet seconds are not enough");
                t.Ok(!hiding.Step(true, 0.25f, 8f) && hiding.Hidden, "the enemy comes back: still hidden");
                t.Ok(!hiding.Step(false, 5f, 8f) && hiding.Hidden, "and the quiet starts over");
                t.Ok(hiding.Step(false, 3f, 8f) && !hiding.Hidden, "eight quiet seconds in a row: back where they sat");
                t.Ok(!hiding.Step(false, 1f, 8f) && !hiding.Hidden, "and they stay");
            }

            // ---- the rescue is not offered in the list of work, only by going along ----
            using (var world = new World())
            {
                var (person, lost) = LostGiver(world);
                var mugQuest = world.Quest("Mug");
                world.Objective(mugQuest, QuestObjectiveKind.Deliver, mug, 1);
                var rescue = world.Quest("Rescue", 1, mugQuest);
                world.Objective(rescue, QuestObjectiveKind.Rescue, person, 1);

                var dialogue = world.Make<DialogueDefinition>("LostTalk");
                NpcSamples.Fill(dialogue, new List<NpcSamples.Line>
                {
                    new NpcSamples.Line
                    {
                        Id = "raid", Start = true, Text = "В лабиринте.",
                        Answers =
                        {
                            new NpcSamples.Answer { Text = "Работа", Actions = { (DialogueActionKind.OpenWork, "", 0) } },
                            new NpcSamples.Answer { Text = "Пойдём", Actions = { (DialogueActionKind.TakeAlong, "", 0) }, QuestConditions = { (rescue, QuestStage.Available) } },
                            new NpcSamples.Answer { Text = "Пойдём!", Actions = { (DialogueActionKind.TakeAlong, "", 0) }, QuestConditions = { (rescue, QuestStage.Active) } },
                            new NpcSamples.Answer { Text = "Пока" },
                        },
                    },
                });
                var so = new SerializedObject(lost);
                so.FindProperty("dialogue").objectReferenceValue = dialogue;
                so.ApplyModifiedPropertiesWithoutUndo();
                NpcCatalog.Override(new[] { lost });
                try
                {
                    var backpack = world.Inventory();
                    var context = new ConversationContext { Npc = lost, Backpack = backpack, InRaid = true, TakeAlong = () => null };
                    t.Eq(Labels(NpcConversation.Start(context)), "Работа|Пока", "before the mug there is no going along");
                    var (menu, _) = Choose(NpcConversation.Start(context), "Работа");
                    t.Eq(Labels(menu), "Новое дело: Mug|Назад", "the mug is offered");

                    QuestLog.Accept(mugQuest);
                    backpack.Add(mug, 1);
                    QuestLog.TryTurnIn(mugQuest, backpack);
                    t.Eq(QuestLog.StatusOf(rescue), QuestStatus.Available, "after the mug the rescue is open");
                    var start = NpcConversation.Start(context);
                    t.Eq(Labels(start), "Пойдём|Пока", "and he can be taken along; with nothing new to offer, 'work' is not asked");
                    t.Ok(!QuestDialogue.HasOffers(lost.Id), "the rescue is not in the list of work");

                    NpcRescue.StartRescue(lost.Id);
                    t.Eq(Labels(NpcConversation.Start(context)), "Пойдём!|Пока", "taken already (after a death): he can be taken along again");
                }
                finally
                {
                    NpcCatalog.Reset();
                }
            }

            // ---- the dialogue validator ----
            using (var world = new World())
            {
                var broken = world.Make<DialogueDefinition>("Broken");
                NpcSamples.Fill(broken, new List<NpcSamples.Line>
                {
                    new NpcSamples.Line { Id = "a", Start = true, Text = "А", Answers = { new NpcSamples.Answer { Text = "Туда", Next = "nowhere" } } },
                    new NpcSamples.Line { Id = "a", Text = "Ещё А", Answers = { new NpcSamples.Answer { Text = "Да", Conditions = { (DialogueConditionKind.ChoiceMade, 1, "typo") } } } },
                    new NpcSamples.Line { Id = "lonely", Text = "Никто сюда не ведёт", Answers = { new NpcSamples.Answer { Text = "Ок" } } },
                });
                var problems = DialogueValidator.Check(new[] { broken }, new NpcDefinition[0], new QuestDefinition[0]);
                string errors = string.Join(" || ", problems.Where(p => p.IsError).Select(p => p.Message));
                string warnings = string.Join(" || ", problems.Where(p => !p.IsError).Select(p => p.Message));
                t.Ok(errors.Contains("«nowhere» нет"), "an answer leading to a missing line is an error");
                t.Ok(errors.Contains("встречается дважды"), "two lines with one name are an error");
                t.Ok(warnings.Contains("«lonely»"), "a line nobody leads to is a warning");
                t.Ok(warnings.Contains("«typo»"), "a choice no answer remembers is a warning");
            }
        }
    }
}
