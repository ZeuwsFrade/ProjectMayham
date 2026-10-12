using System.Linq;
using ProjectMayham.Dialogue;
using ProjectMayham.Quests;

namespace ProjectMayham.EditorTools
{
    public static partial class QuestSelfTest
    {
        // What the window does when an answer is clicked: the action first, then the node the answer leads to.
        private static DialogueNode Pick(DialogueChoice choice)
        {
            choice.Action?.Invoke();
            return choice.Next;
        }

        private static string Labels(DialogueNode node) => string.Join("|", node.Choices.Select(c => c.Text));

        static partial void DialogueTests(Checks t)
        {
            var papers = Item("Papers");

            // Review focus 4: an NPC nobody wrote quests for.
            using (var world = new World())
            {
                var back = new DialogueNode { Text = "root" };
                var ask = QuestDialogue.WorkChoice("Somebody", world.Inventory(), back);
                t.Eq(ask.Text, QuestGiverDefinition.DefaultAskChoice, "unknown npc: the default question");
                var node = Pick(ask);
                t.Ok(node != null && node.Text == QuestGiverDefinition.DefaultNoWorkText && node.Choices.Count == 1,
                    "unknown npc: no work, one answer");
                t.Ok(Pick(node.Choices[0]) == back, "unknown npc: the answer leads back");
            }

            // A giver whose "ready" line is long: it is said first, and the progress keeps a page of its own with the answers.
            using (var world = new World())
            {
                world.Set(world.Giver, "readyText", "Первая строка.\nВторая строка.\nТретья строка.");
                var quest = world.Quest("Готово");
                world.Objective(quest, QuestObjectiveKind.Deliver, papers, 1);
                var backpack = world.Inventory();
                backpack.Add(papers, 1);
                QuestLog.Accept(quest);
                var menu = Pick(QuestDialogue.WorkChoice("TestGiver", backpack, new DialogueNode()));
                var ready = Pick(menu.Choices[0]);
                t.Ok(ready.Text == "Первая строка.\nВторая строка.\nТретья строка." && Labels(ready) == "Дальше", "a long ready line comes first, on its own");
                ready = Pick(ready.Choices[0]);
                t.Ok(ready != null && ready.Text.Contains("Бумаги — 1/1") && !ready.Text.Contains("строка") && Labels(ready) == "Вот, держи|Позже",
                    "then the progress alone, with the two answers");
                if (ready != null && ready.Choices.Count > 0) Pick(ready.Choices[0]);
                t.Eq(QuestLog.StatusOf(quest), QuestStatus.TurnedIn, "and the quest is handed in from there");
            }

            using (var world = new World())
            {
                var first = world.Quest("Первое", 1);
                world.Objective(first, QuestObjectiveKind.Deliver, papers, 3);
                world.Reward(first, 30);
                world.Set(first, "offerText", "Страница один.\n\nСтраница два.");
                world.Quest("Второе", 2, first);
                world.Quest("Побочное", 3);
                world.Quest("Третье", 4);
                var backpack = world.Inventory();
                var back = new DialogueNode { Text = "root" };

                var menu = Pick(QuestDialogue.WorkChoice("TestGiver", backpack, back));
                t.Eq(menu.Text, world.Giver.MenuText, "the list starts with the giver's line");
                t.Eq(Labels(menu), "Новое дело: Первое|Новое дело: Побочное|Что ещё?", "two topics and 'more'");
                var page2 = Pick(menu.Choices[2]);
                t.Eq(Labels(page2), "Новое дело: Третье|Назад", "the last page ends with 'back'");
                t.Ok(Pick(page2.Choices[1]) == back, "'back' leads to where the talk came from");

                // The offer: the writer's pages, then the summary with the two answers.
                var offer = Pick(menu.Choices[0]);
                t.Ok(offer.Text == "Страница один." && Labels(offer) == "Дальше", "first page of the offer");
                offer = Pick(offer.Choices[0]);
                t.Ok(offer.Text == "Страница два." && Labels(offer) == "Дальше", "second page of the offer");
                var summary = Pick(offer.Choices[0]);
                t.Ok(summary.Text.Contains("Нужно: Бумаги ×3") && summary.Text.Contains("Награда: 30 $"),
                    "the summary names the objectives and the reward");
                t.Eq(Labels(summary), "Берусь|Не сейчас", "answers to an offer");

                // "Not now" changes nothing.
                var again = Pick(summary.Choices[1]);
                t.Eq(QuestLog.StatusOf(first), QuestStatus.Available, "declined: still on offer");
                t.Eq(Labels(again), "Новое дело: Первое|Новое дело: Побочное|Что ещё?", "declined: back in the list");

                // Accepting.
                var accepted = Pick(summary.Choices[0]);
                t.Eq(QuestLog.StatusOf(first), QuestStatus.Active, "accepted");
                t.Ok(accepted.Text == world.Giver.AcceptedText && Labels(accepted) == "Понятно", "the giver confirms");
                menu = Pick(accepted.Choices[0]);
                t.Eq(Labels(menu), "Новое дело: Побочное|Новое дело: Третье|Что ещё?", "new offers come before quests in progress");
                var last = Pick(menu.Choices[2]);
                t.Eq(Labels(last), "Напомни: Первое|Назад", "the quest in progress is last");

                // The reminder: the general line, then the progress.
                var reminder = Pick(last.Choices[0]);
                t.Ok(reminder.Text == world.Giver.ReminderText && Labels(reminder) == "Дальше", "the general reminder");
                reminder = Pick(reminder.Choices[0]);
                t.Ok(reminder.Text.Contains("Бумаги — 0/3") && Labels(reminder) == "Понятно", "then the progress");

                // Ready: the quest moves to the top and can be handed in.
                backpack.Add(papers, 3);
                menu = Pick(reminder.Choices[0]);
                t.Ok(Labels(menu).StartsWith("Сдать: Первое|"), "a ready quest is first");
                var ready = Pick(menu.Choices[0]);
                t.Ok(ready.Text.StartsWith(world.Giver.ReadyText) && ready.Text.Contains("Бумаги — 3/3"), "the ready line with the progress");
                t.Eq(Labels(ready), "Вот, держи|Позже", "answers when ready");
                int money = world.Data.money;
                var thanks = Pick(ready.Choices[0]);
                t.Eq(QuestLog.StatusOf(first), QuestStatus.TurnedIn, "handed in");
                t.Ok(thanks.Text == "Thanks for Первое" && Labels(thanks) == "Дальше", "the thanks");
                var received = Pick(thanks.Choices[0]);
                t.Ok(received.Text.Contains("Получено: 30 $") && Labels(received) == "Понятно" && world.Data.money == money + 30,
                    "what was received");
                menu = Pick(received.Choices[0]);
                t.Eq(Labels(menu), "Новое дело: Второе|Новое дело: Побочное|Что ещё?", "the quest that was unlocked is offered");
                t.Eq(string.Join(",", QuestService.GetAvailable("TestGiver")), "Второе,Побочное,Третье", "the old service lists the offers");
            }
        }
    }
}
