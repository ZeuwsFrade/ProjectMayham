using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Dialogue;
using ProjectMayham.Items;
using ProjectMayham.Shelter;
using UnityEngine;

namespace ProjectMayham.Quests
{
    /// <summary>
    /// The talk about quests with a quest giver, built from the nodes of <see cref="DialogueWindow"/>. Two lists:
    /// the work the giver has (new quests to take) and the quests the player has taken from them (hand in what is
    /// ready, hear what is still missing, refuse where allowed). The offer of a quest can be taken and the talk
    /// ended, or taken and the talk go on from its start. Lists are as long as they need to be: the window scrolls.
    /// </summary>
    public static class QuestDialogue
    {
        public const string DefaultTakenChoice = "По поводу твоего задания";

        // What the player can answer; the same for every NPC.
        private const string AnswerAccept = "Берусь";
        private const string AnswerAcceptMore = "Берусь. И ещё вопрос";
        private const string AnswerBye = "До встречи";
        private const string AnswerDecline = "Не сейчас";
        private const string AnswerTurnIn = "Вот, держи";
        private const string AnswerLater = "Позже";
        private const string AnswerOk = "Понятно";
        private const string AnswerNext = "Дальше";
        private const string AnswerBack = "Назад";
        private const string AnswerAbandon = "Отказаться";
        private const string AnswerAbandonYes = "Да, отказываюсь";
        private const string AnswerAbandonNo = "Нет, сделаю";

        /// <summary>What a list is about.</summary>
        private enum ListKind
        {
            /// <summary>New quests the giver offers.</summary>
            Offers,
            /// <summary>Quests the player has taken from the giver.</summary>
            Taken,
        }

        // What one conversation is about.
        private class Talk
        {
            public string NpcId;
            public QuestGiverDefinition Giver;
            public GridInventory Backpack;
            public DialogueNode Back;
            public ListKind List;
            /// <summary>
            /// The start of the whole conversation, with this line of the giver in place of its own (null: its own
            /// line, as when the talk began); null when there is no conversation around the talk about quests.
            /// </summary>
            public Func<string, DialogueNode> Restart;
        }

        /// <summary>
        /// The answer that opens the list of new quests of this NPC. "Back" leads to <paramref name="back"/>;
        /// <paramref name="restart"/> builds the start of the conversation for "take it, and one more question".
        /// </summary>
        public static DialogueChoice WorkChoice(string npcId, GridInventory backpack, DialogueNode back, Func<string, DialogueNode> restart = null)
        {
            var talk = NewTalk(npcId, backpack, back, ListKind.Offers, restart);
            string ask = talk.Giver != null ? talk.Giver.AskChoice : QuestGiverDefinition.DefaultAskChoice;
            return Go(ask, () => Menu(talk));
        }

        /// <summary>The answer that opens the list of the quests the player has taken from this NPC.</summary>
        public static DialogueChoice TakenChoice(string npcId, GridInventory backpack, DialogueNode back, Func<string, DialogueNode> restart = null)
        {
            var talk = NewTalk(npcId, backpack, back, ListKind.Taken, restart);
            string ask = talk.Giver != null ? talk.Giver.TakenChoice : DefaultTakenChoice;
            return Go(ask, () => Menu(talk));
        }

        /// <summary>True when the NPC has a new quest to offer (the answer about work is shown only then).</summary>
        public static bool HasOffers(string npcId) => QuestLog.Offers(npcId).Any(q => !q.IsRescue);

        /// <summary>True when the player has taken a quest from the NPC (the answer about it is shown only then).</summary>
        public static bool HasTaken(string npcId) => QuestLog.ActiveOf(npcId).Any(q => !q.IsRescue);

        private static Talk NewTalk(string npcId, GridInventory backpack, DialogueNode back, ListKind list, Func<string, DialogueNode> restart) =>
            new Talk { NpcId = npcId, Giver = QuestCatalog.Giver(npcId), Backpack = backpack, Back = back, List = list, Restart = restart };

        // The window runs the action of an answer before it reads where the answer leads, so the next node can be
        // built at the moment of the click: what it says is then always up to date, also after trading.
        private static DialogueChoice Go(string text, Func<DialogueNode> build)
        {
            var choice = new DialogueChoice { Text = text };
            choice.Action = () => choice.Next = build();
            return choice;
        }

        private static DialogueNode Menu(Talk talk)
        {
            // A rescue is in neither list: it is taken when the NPC goes along and handed in when they arrive.
            var node = new DialogueNode();
            if (talk.List == ListKind.Offers)
            {
                var offers = QuestLog.Offers(talk.NpcId).Where(q => !q.IsRescue).ToList();
                if (offers.Count == 0) return Empty(talk, talk.Giver != null ? talk.Giver.NoWorkText : QuestGiverDefinition.DefaultNoWorkText);
                // There are quests, so their giver is known even when the catalog was asked too early.
                talk.Giver ??= offers[0].Giver;
                node.Text = talk.Giver.MenuText;
                foreach (var quest in offers) node.Choices.Add(Go($"Новое дело: {quest.Title}", () => OfferNode(talk, quest, 0)));
            }
            else
            {
                var taken = QuestLog.ActiveOf(talk.NpcId).Where(q => !q.IsRescue).ToList();
                if (taken.Count == 0) return Empty(talk, talk.Giver != null ? talk.Giver.NoTakenText : QuestGiverDefinition.DefaultNoTakenText);
                talk.Giver ??= taken[0].Giver;
                node.Text = talk.Giver.TakenText;
                // What can be handed in comes first.
                foreach (var quest in taken.OrderBy(q => QuestLog.Check(q, talk.Backpack).Ready ? 0 : 1))
                {
                    if (QuestLog.Check(quest, talk.Backpack).Ready) node.Choices.Add(Go($"Сдать: {quest.Title}", () => ReadyNode(talk, quest)));
                    else node.Choices.Add(Go($"Напомни: {quest.Title}", () => ReminderNode(talk, quest)));
                }
            }
            node.Choices.Add(new DialogueChoice { Text = AnswerBack, Next = talk.Back });
            return node;
        }

        private static DialogueNode Empty(Talk talk, string text)
        {
            var node = new DialogueNode { Text = text };
            node.Choices.Add(new DialogueChoice { Text = AnswerOk, Next = talk.Back });
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
            // Take it and go, or take it and go on talking from the start.
            node.Choices.Add(Go(AnswerAccept, () =>
            {
                QuestLog.Accept(quest);
                return Farewell(talk.Giver.AcceptedText);
            }));
            node.Choices.Add(Go(AnswerAcceptMore, () =>
            {
                QuestLog.Accept(quest);
                return talk.Restart != null ? SayThen(talk.Giver.AcceptedText, talk.Restart) : Say(talk.Giver.AcceptedText, () => Menu(talk));
            }));
            node.Choices.Add(Go(AnswerDecline, () => Menu(talk)));
            return node;
        }

        private static DialogueNode ReminderNode(Talk talk, QuestDefinition quest)
        {
            string text = string.IsNullOrWhiteSpace(quest.ReminderText) ? talk.Giver.ReminderText : quest.ReminderText;
            var progress = QuestLog.Check(quest, talk.Backpack, Stash());
            string checklist = QuestText.Small(QuestText.Checklist(progress));
            if (!QuestLog.CanAbandon(quest)) return Say(text, () => Menu(talk), checklist);

            // A quest that may be refused: the last page has a second answer.
            var pages = QuestText.Pages(text);
            pages.Add(checklist);
            return ReminderPage(talk, quest, pages, 0);
        }

        private static DialogueNode ReminderPage(Talk talk, QuestDefinition quest, List<string> pages, int index)
        {
            var node = new DialogueNode { Text = pages[index] };
            if (index < pages.Count - 1)
            {
                node.Choices.Add(Go(AnswerNext, () => ReminderPage(talk, quest, pages, index + 1)));
                return node;
            }
            node.Choices.Add(Go(AnswerOk, () => Menu(talk)));
            node.Choices.Add(Go(AnswerAbandon, () => AbandonAsk(talk, quest)));
            return node;
        }

        // The giver asks once more; the player confirms or changes their mind. What it costs is not shown.
        private static DialogueNode AbandonAsk(Talk talk, QuestDefinition quest)
        {
            string ask = string.IsNullOrWhiteSpace(talk.Giver.AbandonAskText) ? string.Empty : talk.Giver.AbandonAskText.Trim() + "\n";
            var node = new DialogueNode { Text = ask + QuestText.Small($"Отказаться от «{quest.Title}»?") };
            node.Choices.Add(Go(AnswerAbandonYes, () =>
            {
                QuestLog.Abandon(quest);
                return Say(talk.Giver.AbandonedText, () => Menu(talk));
            }));
            node.Choices.Add(Go(AnswerAbandonNo, () => Menu(talk)));
            return node;
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
                if (result == TurnInResult.NoRoom) return Say(talk.Giver.NoRoomText, () => Menu(talk));
                if (result != TurnInResult.Done) return Menu(talk);

                // Handed in: after the thanks and the reward the talk starts over from its beginning.
                string reward = QuestText.Reward(quest);
                return Say(quest.CompletedText, () => talk.Restart != null ? talk.Restart(null) : Menu(talk),
                    reward.Length > 0 ? QuestText.Small($"Получено: {reward}") : null);
            }));
            node.Choices.Add(Go(AnswerLater, () => Menu(talk)));
            return node;
        }

        /// <summary>
        /// What the giver says about a quest handed in without a talk (a rescue, on arrival at the shelter): thanks and
        /// what was received, or that the reward has no room; the last answer closes the window.
        /// </summary>
        public static DialogueNode Outcome(QuestDefinition quest, TurnInResult result)
        {
            var giver = quest.Giver;
            var pages = new List<string>();
            if (result == TurnInResult.Done)
            {
                pages.AddRange(QuestText.Pages(quest.CompletedText));
                string reward = QuestText.Reward(quest);
                if (reward.Length > 0) pages.Add(QuestText.Small($"Получено: {reward}"));
            }
            else pages.AddRange(QuestText.Pages(giver != null ? giver.NoRoomText : string.Empty));
            if (pages.Count == 0) pages.Add(quest.Title);
            return Closing(pages, AnswerOk);
        }

        // The giver's line, page by page, and the talk is over.
        private static DialogueNode Farewell(string text)
        {
            var pages = QuestText.Pages(text);
            if (pages.Count == 0) pages.Add(QuestGiverDefinition.DefaultAcceptedText);
            return Closing(pages, AnswerBye);
        }

        private static DialogueNode Closing(List<string> pages, string lastAnswer)
        {
            DialogueNode Build(int index)
            {
                var node = new DialogueNode { Text = pages[index] };
                node.Choices.Add(index < pages.Count - 1
                    ? Go(AnswerNext, () => Build(index + 1))
                    : new DialogueChoice { Text = lastAnswer, Closes = true });
                return node;
            }
            return Build(0);
        }

        // The giver's line, page by page; its last page is shown with the answers of <paramref name="then"/>.
        private static DialogueNode SayThen(string text, Func<string, DialogueNode> then)
        {
            var pages = QuestText.Pages(text);
            if (pages.Count == 0) return then(string.Empty);
            string last = pages[pages.Count - 1];
            pages.RemoveAt(pages.Count - 1);
            return pages.Count == 0 ? then(last) : Page(pages, 0, () => then(last), AnswerNext);
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
