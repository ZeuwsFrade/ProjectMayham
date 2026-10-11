using System;
using System.Collections.Generic;
using ProjectMayham.Dialogue;
using ProjectMayham.Items;
using ProjectMayham.Shelter;
using UnityEngine;

namespace ProjectMayham.Quests
{
    /// <summary>
    /// The talk about work with a quest giver, built from the nodes of <see cref="DialogueWindow"/>: a list of
    /// quests (ready to hand in, new, in progress), the offer of a quest, a reminder of what is still missing and
    /// the turn-in. Stays within what the window fits: three answers and about three lines of text per node.
    /// </summary>
    public static class QuestDialogue
    {
        public const int TopicsPerPage = 2;

        // What the player can answer; the same for every NPC.
        private const string AnswerAccept = "Берусь";
        private const string AnswerDecline = "Не сейчас";
        private const string AnswerTurnIn = "Вот, держи";
        private const string AnswerLater = "Позже";
        private const string AnswerOk = "Понятно";
        private const string AnswerNext = "Дальше";
        private const string AnswerMore = "Что ещё?";
        private const string AnswerBack = "Назад";

        private enum TopicKind { Ready, Offer, Reminder }

        private struct Topic
        {
            public TopicKind Kind;
            public QuestDefinition Quest;
        }

        // What one conversation is about.
        private class Talk
        {
            public string NpcId;
            public QuestGiverDefinition Giver;
            public GridInventory Backpack;
            public DialogueNode Back;
        }

        /// <summary>The answer that opens the talk about work with this NPC; add it to the NPC's first node.</summary>
        public static DialogueChoice WorkChoice(string npcId, GridInventory backpack, DialogueNode back)
        {
            var talk = new Talk { NpcId = npcId, Giver = QuestCatalog.Giver(npcId), Backpack = backpack, Back = back };
            string ask = talk.Giver != null ? talk.Giver.AskChoice : QuestGiverDefinition.DefaultAskChoice;
            return Go(ask, () => Menu(talk, 0));
        }

        // The window runs the action of an answer before it reads where the answer leads, so the next node can be
        // built at the moment of the click: what it says is then always up to date, also after trading.
        private static DialogueChoice Go(string text, Func<DialogueNode> build)
        {
            var choice = new DialogueChoice { Text = text };
            choice.Action = () => choice.Next = build();
            return choice;
        }

        private static List<Topic> Topics(Talk talk)
        {
            var topics = new List<Topic>();
            var waiting = new List<Topic>();
            foreach (var quest in QuestLog.ActiveOf(talk.NpcId))
            {
                if (QuestLog.Check(quest, talk.Backpack).Ready) topics.Add(new Topic { Kind = TopicKind.Ready, Quest = quest });
                else waiting.Add(new Topic { Kind = TopicKind.Reminder, Quest = quest });
            }
            foreach (var quest in QuestLog.Offers(talk.NpcId)) topics.Add(new Topic { Kind = TopicKind.Offer, Quest = quest });
            topics.AddRange(waiting);
            return topics;
        }

        private static DialogueNode Menu(Talk talk, int page)
        {
            var topics = Topics(talk);
            var node = new DialogueNode();
            if (topics.Count == 0)
            {
                node.Text = talk.Giver != null ? talk.Giver.NoWorkText : QuestGiverDefinition.DefaultNoWorkText;
                node.Choices.Add(new DialogueChoice { Text = AnswerOk, Next = talk.Back });
                return node;
            }

            // There are quests, so their giver is known even when the catalog was asked too early.
            talk.Giver ??= topics[0].Quest.Giver;
            int pages = (topics.Count + TopicsPerPage - 1) / TopicsPerPage;
            page = Mathf.Clamp(page, 0, pages - 1);
            node.Text = talk.Giver.MenuText;

            int end = Mathf.Min(topics.Count, (page + 1) * TopicsPerPage);
            for (int i = page * TopicsPerPage; i < end; i++)
            {
                var quest = topics[i].Quest;
                switch (topics[i].Kind)
                {
                    case TopicKind.Ready:
                        node.Choices.Add(Go($"Сдать: {quest.Title}", () => ReadyNode(talk, quest)));
                        break;
                    case TopicKind.Offer:
                        node.Choices.Add(Go($"Новое дело: {quest.Title}", () => OfferNode(talk, quest, 0)));
                        break;
                    default:
                        node.Choices.Add(Go($"Напомни: {quest.Title}", () => ReminderNode(talk, quest)));
                        break;
                }
            }

            if (page < pages - 1) node.Choices.Add(Go(AnswerMore, () => Menu(talk, page + 1)));
            else node.Choices.Add(new DialogueChoice { Text = AnswerBack, Next = talk.Back });
            return node;
        }

        // The writer's pages one by one, then the page the game writes: what is asked and what is paid.
        private static DialogueNode OfferNode(Talk talk, QuestDefinition quest, int page)
        {
            var pages = QuestText.Pages(quest.OfferText);
            var node = new DialogueNode();
            if (page < pages.Count)
            {
                node.Text = pages[page];
                node.Choices.Add(Go(AnswerNext, () => OfferNode(talk, quest, page + 1)));
                return node;
            }

            string summary = QuestText.OfferSummary(quest);
            node.Text = summary.Length > 0 ? QuestText.Small(summary) : quest.Title;
            node.Choices.Add(Go(AnswerAccept, () =>
            {
                QuestLog.Accept(quest);
                return Say(talk.Giver.AcceptedText, () => Menu(talk, 0));
            }));
            node.Choices.Add(Go(AnswerDecline, () => Menu(talk, 0)));
            return node;
        }

        private static DialogueNode ReminderNode(Talk talk, QuestDefinition quest)
        {
            string text = string.IsNullOrWhiteSpace(quest.ReminderText) ? talk.Giver.ReminderText : quest.ReminderText;
            var progress = QuestLog.Check(quest, talk.Backpack, Stash());
            return Say(text, () => Menu(talk, 0), QuestText.Small(QuestText.Checklist(progress)));
        }

        // The giver's line and the progress. The progress says what will be taken, so it must never be pushed
        // out of the box: a short line shares the screen with it, a longer one is said first, page by page.
        private static DialogueNode ReadyNode(Talk talk, QuestDefinition quest)
        {
            string checklist = QuestText.Checklist(QuestLog.Check(quest, talk.Backpack));
            var pages = QuestText.Pages(talk.Giver.ReadyText);

            string last;   // what the screen with the two answers shows
            if (checklist.Length == 0)
            {
                last = pages.Count > 0 ? pages[pages.Count - 1] : quest.Title;
                if (pages.Count > 0) pages.RemoveAt(pages.Count - 1);
            }
            else if (pages.Count == 1 && !pages[0].Contains("\n") && pages[0].Length + checklist.Length <= QuestText.PageLimit)
            {
                last = pages[0] + "\n" + QuestText.Small(checklist);
                pages.Clear();
            }
            else last = QuestText.Small(checklist);

            return pages.Count == 0
                ? ReadyAnswers(talk, quest, last)
                : Page(pages, 0, () => ReadyAnswers(talk, quest, last), AnswerNext);
        }

        private static DialogueNode ReadyAnswers(Talk talk, QuestDefinition quest, string text)
        {
            var node = new DialogueNode { Text = text };
            node.Choices.Add(Go(AnswerTurnIn, () =>
            {
                var result = QuestLog.TryTurnIn(quest, talk.Backpack);
                if (result == TurnInResult.NoRoom) return Say(talk.Giver.NoRoomText, () => Menu(talk, 0));
                if (result != TurnInResult.Done) return Menu(talk, 0);

                string reward = QuestText.Reward(quest);
                return Say(quest.CompletedText, () => Menu(talk, 0), reward.Length > 0 ? QuestText.Small($"Получено: {reward}") : null);
            }));
            node.Choices.Add(Go(AnswerLater, () => Menu(talk, 0)));
            return node;
        }

        /// <summary>
        /// The pages of a text one after another, with an optional last page written by the game; the answer under
        /// the last page leads on to <paramref name="then"/>.
        /// </summary>
        private static DialogueNode Say(string text, Func<DialogueNode> then, string tail = null)
        {
            var pages = QuestText.Pages(text);
            if (!string.IsNullOrEmpty(tail)) pages.Add(tail);
            return pages.Count == 0 ? then() : Page(pages, 0, then, AnswerOk);
        }

        // One page with one answer: "next" between pages, <paramref name="lastAnswer"/> under the last one.
        private static DialogueNode Page(List<string> pages, int index, Func<DialogueNode> then, string lastAnswer)
        {
            var node = new DialogueNode { Text = pages[index] };
            node.Choices.Add(index < pages.Count - 1
                ? Go(AnswerNext, () => Page(pages, index + 1, then, lastAnswer))
                : Go(lastAnswer, then));
            return node;
        }

        // The stash of the shelter, when the talk happens there: what lies in it is mentioned, not counted.
        private static GridInventory Stash()
        {
            var chest = UnityEngine.Object.FindFirstObjectByType<StashChest>();
            return chest != null ? chest.Inventory : null;
        }
    }
}
