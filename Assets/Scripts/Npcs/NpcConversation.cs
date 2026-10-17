using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Dialogue;
using ProjectMayham.Items;
using ProjectMayham.Quests;
using ProjectMayham.Trade;
using UnityEngine;

namespace ProjectMayham.Npcs
{
    /// <summary>Who the conversation is with and what it can reach.</summary>
    public class ConversationContext
    {
        public NpcDefinition Npc;
        /// <summary>The player's grid: items for conditions, the talk about work, trade.</summary>
        public GridInventory Backpack;
        /// <summary>The trade of this NPC, or null when they do not trade here.</summary>
        public TraderBook Book;
        /// <summary>
        /// What "take along" does here: null when it worked, or what keeps the player from it. Null itself when the NPC
        /// cannot go with the player here.
        /// </summary>
        public Func<string> TakeAlong;
        /// <summary>The conversation happens in a raid, not at the base.</summary>
        public bool InRaid;
    }

    /// <summary>
    /// Plays a <see cref="DialogueDefinition"/> in the <see cref="DialogueWindow"/>: picks the line to start with,
    /// shows the answers whose conditions hold, runs their actions and goes to the next line. Every node is built at
    /// the moment it is shown, so what it offers is always up to date (after trading, a quest handed in, a choice
    /// made). Long lines are paged, and more answers than the window holds are paged too.
    /// </summary>
    public static class NpcConversation
    {
        /// <summary>Answers on one page of a line; with more, the last place is taken by "more".</summary>
        public const int AnswersPerPage = 4;

        private const string AnswerNext = "Дальше";
        private const string AnswerMore = "Ещё…";
        private const string AnswerLeave = "Я пойду";

        /// <summary>Starts the conversation. Without a dialogue asset: a greeting, trade and work, as it was before.</summary>
        public static void Open(ConversationContext context, Sprite portrait)
        {
            var npc = context.Npc;
            // Loyalty is a hidden parameter: the player sees only what it opens.
            DialogueWindow.Open(npc.DisplayName, npc.Portrait != null ? npc.Portrait : portrait, Start(context));
        }

        /// <summary>The first node of the conversation.</summary>
        public static DialogueNode Start(ConversationContext context)
        {
            var dialogue = context.Npc.Dialogue;
            if (dialogue == null) return Plain(context);

            var line = dialogue.Lines.FirstOrDefault(l => l != null && l.Start && Holds(l.Conditions, context))
                ?? dialogue.Lines.FirstOrDefault(l => l != null);
            return line != null ? LineNode(context, line) : Plain(context);
        }

        // ---- conditions ----

        public static bool Holds(IReadOnlyList<DialogueCondition> conditions, ConversationContext context) =>
            conditions == null || conditions.All(c => c == null || Holds(c, context));

        public static bool Holds(DialogueCondition condition, ConversationContext context)
        {
            switch (condition.Kind)
            {
                case DialogueConditionKind.MinLoyalty:
                    return Loyalty.Level(context.Npc.Id) >= condition.Level;
                case DialogueConditionKind.Quest:
                {
                    if (condition.Quest == null) return false;
                    var stage = StageOf(condition.Quest, context.Backpack);
                    // Not taken covers both a locked quest and one on offer.
                    return stage == condition.Stage || (condition.Stage == QuestStage.NotTaken && stage == QuestStage.Available);
                }
                case DialogueConditionKind.ChoiceMade:
                    return PlayerChoices.Has(condition.Choice);
                case DialogueConditionKind.ChoiceNotMade:
                    return !PlayerChoices.Has(condition.Choice);
                case DialogueConditionKind.HasItem:
                    return condition.Item != null && context.Backpack != null && QuestLog.Carried(context.Backpack, condition.Item) >= condition.Count;
                case DialogueConditionKind.InRaid:
                    return context.InRaid;
                case DialogueConditionKind.AtBase:
                    return !context.InRaid;
                default:
                    return false;
            }
        }

        /// <summary>Where a quest is for a condition. Ready means taken and everything it asks for is at hand.</summary>
        public static QuestStage StageOf(QuestDefinition quest, GridInventory backpack)
        {
            switch (QuestLog.StatusOf(quest))
            {
                case QuestStatus.Available: return QuestStage.Available;
                case QuestStatus.TurnedIn: return QuestStage.TurnedIn;
                case QuestStatus.Active:
                    return backpack != null && QuestLog.Check(quest, backpack).Ready ? QuestStage.Ready : QuestStage.Active;
                default: return QuestStage.NotTaken;
            }
        }

        // ---- nodes ----

        private static DialogueChoice Go(string text, Func<DialogueNode> build)
        {
            var choice = new DialogueChoice { Text = text };
            choice.Action = () =>
            {
                choice.Next = build();
                choice.Closes = choice.Next == null;
            };
            return choice;
        }

        private static DialogueNode LineNode(ConversationContext context, DialogueLine line)
        {
            var pages = QuestText.Pages(line.Text);
            if (pages.Count == 0) pages.Add(string.Empty);
            return Page(context, line, pages, 0);
        }

        private static DialogueNode Page(ConversationContext context, DialogueLine line, List<string> pages, int index)
        {
            if (index < pages.Count - 1)
            {
                var node = new DialogueNode { Text = pages[index] };
                node.Choices.Add(Go(AnswerNext, () => Page(context, line, pages, index + 1)));
                return node;
            }
            return Answers(context, line, pages[index], 0);
        }

        private static DialogueNode Answers(ConversationContext context, DialogueLine line, string text, int first)
        {
            var node = new DialogueNode { Text = text };
            var shown = line.Answers.Where(a => a != null && Holds(a.Conditions, context)).ToList();
            if (shown.Count == 0)
            {
                // A line nobody can answer still has to let the player go.
                node.Choices.Add(new DialogueChoice { Text = AnswerLeave, Closes = true });
                return node;
            }

            bool paged = shown.Count - first > AnswersPerPage;
            int take = paged ? AnswersPerPage - 1 : AnswersPerPage;
            foreach (var answer in shown.Skip(first).Take(take))
            {
                var picked = answer;
                node.Choices.Add(Go(answer.Text, () => Pick(context, line, picked)));
            }
            if (paged) node.Choices.Add(Go(AnswerMore, () => Answers(context, line, text, first + take)));
            return node;
        }

        /// <summary>Runs the actions of the answer and returns the next node (null ends the conversation).</summary>
        private static DialogueNode Pick(ConversationContext context, DialogueLine line, DialogueAnswer answer)
        {
            bool work = false;
            foreach (var action in answer.Actions)
            {
                if (action == null) continue;
                switch (action.Kind)
                {
                    case DialogueActionKind.RememberChoice:
                        PlayerChoices.Remember(action.Choice);
                        break;
                    case DialogueActionKind.AddLoyalty:
                        Loyalty.Add(context.Npc.Id, action.Amount);
                        break;
                    case DialogueActionKind.OpenTrade:
                        if (context.Book != null && context.Backpack != null) TradeWindow.Open(context.Book, context.Backpack);
                        break;
                    case DialogueActionKind.OpenWork:
                        work = true;
                        break;
                    case DialogueActionKind.TakeAlong:
                        if (context.TakeAlong == null) break;
                        string refusal = context.TakeAlong();
                        // They go along: the conversation is over. Or the player hears why not.
                        if (string.IsNullOrEmpty(refusal)) return null;
                        return Refusal(context, line, refusal);
                }
            }

            var target = context.Npc.Dialogue != null ? context.Npc.Dialogue.Find(answer.Next) : null;
            if (!work) return target != null ? LineNode(context, target) : null;

            // The talk about work, and back to the line the answer leads to (or this one).
            var back = LineNode(context, target ?? line);
            var choice = QuestDialogue.WorkChoice(context.Npc.Id, context.Backpack, back);
            choice.Action?.Invoke();
            return choice.Next;
        }

        private static DialogueNode Refusal(ConversationContext context, DialogueLine line, string text)
        {
            var node = new DialogueNode { Text = text };
            node.Choices.Add(Go("Ясно", () => LineNode(context, line)));
            return node;
        }

        // An NPC without a dialogue: what the trader said before dialogues were assets.
        private static DialogueNode Plain(ConversationContext context)
        {
            var npc = context.Npc;
            var root = new DialogueNode { Text = npc.Trader != null ? npc.Trader.Greeting : $"Привет, [ИГРОК]." };
            if (context.Book != null && context.Backpack != null)
                root.Choices.Add(new DialogueChoice { Text = "Давай посмотрим товары", Action = () => TradeWindow.Open(context.Book, context.Backpack) });
            root.Choices.Add(QuestDialogue.WorkChoice(npc.Id, context.Backpack, root));
            root.Choices.Add(new DialogueChoice { Text = AnswerLeave, Closes = true });
            return root;
        }
    }
}
