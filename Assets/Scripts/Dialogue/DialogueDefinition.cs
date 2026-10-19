using System;
using System.Collections.Generic;
using ProjectMayham.Items;
using ProjectMayham.Quests;
using UnityEngine;

namespace ProjectMayham.Dialogue
{
    /// <summary>What has to hold for an answer to show (or for a line to start the conversation).</summary>
    public enum DialogueConditionKind
    {
        /// <summary>The loyalty of this NPC is at least <see cref="DialogueCondition.Level"/>.</summary>
        MinLoyalty = 0,
        /// <summary>A quest is in a stage: not taken, on offer, taken, ready to hand in, handed in.</summary>
        Quest = 1,
        /// <summary>The player gave an answer that the story remembers (<see cref="DialogueActionKind.RememberChoice"/>).</summary>
        ChoiceMade = 2,
        /// <summary>The player never gave that answer.</summary>
        ChoiceNotMade = 3,
        /// <summary>The player carries at least this many of an item.</summary>
        HasItem = 4,
        /// <summary>The conversation happens in a raid (a lost NPC in the maze).</summary>
        InRaid = 5,
        /// <summary>The conversation happens at the base.</summary>
        AtBase = 6,
    }

    /// <summary>Where a quest is, as a condition sees it.</summary>
    public enum QuestStage
    {
        /// <summary>Not taken: locked or on offer.</summary>
        NotTaken = 0,
        /// <summary>On offer right now.</summary>
        Available = 1,
        /// <summary>Taken and not handed in, done or not.</summary>
        Active = 2,
        /// <summary>Taken and everything it asks for is at hand.</summary>
        Ready = 3,
        TurnedIn = 4,
    }

    /// <summary>What picking an answer does, besides leading to the next line.</summary>
    public enum DialogueActionKind
    {
        /// <summary>The story remembers that the player gave this answer: quests and other answers can depend on it.</summary>
        RememberChoice = 0,
        /// <summary>The talk about work: the new quests this NPC offers. The answer shows only when there are some.</summary>
        OpenWork = 1,
        /// <summary>The trade window of this NPC.</summary>
        OpenTrade = 2,
        /// <summary>Adds (or with a negative amount takes) loyalty points of this NPC.</summary>
        AddLoyalty = 3,
        /// <summary>The NPC goes with the player (a lost NPC in the raid).</summary>
        TakeAlong = 4,
        /// <summary>The talk about the quests the player has taken from this NPC: hand in, how far, refuse.</summary>
        OpenTaken = 5,
    }

    [Serializable]
    public class DialogueCondition
    {
        [SerializeField] private DialogueConditionKind kind;
        [SerializeField, Range(1, 4)] private int level = 2;
        [SerializeField] private QuestDefinition quest;
        [SerializeField] private QuestStage stage = QuestStage.TurnedIn;
        [SerializeField] private string choice;
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(1)] private int count = 1;

        public DialogueConditionKind Kind => kind;
        public int Level => Mathf.Clamp(level, 1, 4);
        public QuestDefinition Quest => quest;
        public QuestStage Stage => stage;
        public string Choice => choice != null ? choice.Trim() : string.Empty;
        public ItemDefinition Item => item;
        public int Count => Mathf.Max(1, count);
    }

    [Serializable]
    public class DialogueAction
    {
        [SerializeField] private DialogueActionKind kind;
        [SerializeField] private string choice;
        [SerializeField] private int amount = 1;

        public DialogueActionKind Kind => kind;
        public string Choice => choice != null ? choice.Trim() : string.Empty;
        public int Amount => amount;
    }

    /// <summary>One answer of the player to a line.</summary>
    [Serializable]
    public class DialogueAnswer
    {
        [Tooltip("What the player says.")]
        [SerializeField] private string text;
        [Tooltip("Id of the line the NPC says next. Empty: the conversation ends.")]
        [SerializeField] private string next;
        [Tooltip("The answer shows only when all of these hold.")]
        [SerializeField] private List<DialogueCondition> conditions = new List<DialogueCondition>();
        [Tooltip("Done in this order when the answer is picked, before the next line.")]
        [SerializeField] private List<DialogueAction> actions = new List<DialogueAction>();

        public string Text => text;
        public string Next => next != null ? next.Trim() : string.Empty;
        public IReadOnlyList<DialogueCondition> Conditions => conditions;
        public IReadOnlyList<DialogueAction> Actions => actions;
    }

    /// <summary>One line of the NPC and the answers the player can give to it.</summary>
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("Name of the line, unique in the dialogue: answers lead to it by this name.")]
        [SerializeField] private string id;
        [Tooltip("What the NPC says. An empty line starts a new page; a page holds three lines at most.")]
        [TextArea(3, 8)] [SerializeField] private string text;
        [Tooltip("The conversation can start with this line: the first such line (from the top) whose conditions hold is used.")]
        [SerializeField] private bool start;
        [Tooltip("For a starting line: it is used only when all of these hold.")]
        [SerializeField] private List<DialogueCondition> conditions = new List<DialogueCondition>();
        [SerializeField] private List<DialogueAnswer> answers = new List<DialogueAnswer>();

        public string Id => id != null ? id.Trim() : string.Empty;
        public string Text => text;
        public bool Start => start;
        public IReadOnlyList<DialogueCondition> Conditions => conditions;
        public IReadOnlyList<DialogueAnswer> Answers => answers;
    }

    /// <summary>
    /// A conversation with an NPC, written in the Inspector: lines of the NPC, answers of the player that lead from
    /// line to line, with conditions (loyalty, quests, earlier answers, items) and actions (remember an answer, the
    /// talk about work, trade, loyalty). <see cref="Npcs.NpcConversation"/> plays it in the dialogue window.
    /// </summary>
    [CreateAssetMenu(menuName = "Mayham/Dialogue", fileName = "Dialogue_New")]
    public class DialogueDefinition : ScriptableObject
    {
        [SerializeField] private List<DialogueLine> lines = new List<DialogueLine>();

        public IReadOnlyList<DialogueLine> Lines => lines;

        /// <summary>The line with this id, or null.</summary>
        public DialogueLine Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            id = id.Trim();
            foreach (var line in lines)
            {
                if (line != null && line.Id == id) return line;
            }
            return null;
        }
    }
}
