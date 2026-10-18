using UnityEngine;

namespace ProjectMayham.Quests
{
    /// <summary>
    /// An NPC that gives quests: the lines of the talk about work that belong to no single quest. The name of the
    /// asset is the NPC id; for a trader it has to equal <see cref="Trade.TraderDefinition.Id"/> ("Tyler").
    /// Lives in Resources/Quests/Givers.
    /// </summary>
    [CreateAssetMenu(menuName = "Mayham/Quests/Quest Giver", fileName = "NewQuestGiver")]
    public class QuestGiverDefinition : ScriptableObject
    {
        public const string DefaultAskChoice = "Есть для меня работа?";
        public const string DefaultNoWorkText = "Работы пока нет, [ИГРОК]. Загляни позже.";
        public const string DefaultNoTakenText = "Ты сейчас ничего для меня не делаешь.";
        public const string DefaultAcceptedText = "Договорились.";

        [Tooltip("The answer of the player that opens the talk about work.")]
        [SerializeField] private string askChoice = DefaultAskChoice;
        [Tooltip("Said above the list of quests.")]
        [TextArea] [SerializeField] private string menuText = "Есть пара дел.";
        [Tooltip("Said when there is nothing to offer, remind of or take.")]
        [TextArea] [SerializeField] private string noWorkText = DefaultNoWorkText;
        [Tooltip("Said when the player takes a quest.")]
        [TextArea] [SerializeField] private string acceptedText = DefaultAcceptedText;
        [Tooltip("Said about an unfinished quest that has no reminder of its own.")]
        [TextArea] [SerializeField] private string reminderText = "Ты ещё не всё сделал.";
        [Tooltip("Said when everything for a quest is there.")]
        [TextArea] [SerializeField] private string readyText = "Вижу, всё при тебе.";
        [Tooltip("Said when the reward does not fit into the backpack.")]
        [TextArea] [SerializeField] private string noRoomText = "Тебе некуда положить награду. Освободи место и приходи.";

        [Header("Taken quests")]
        [Tooltip("The answer of the player that opens the list of quests they have taken (for an NPC without a dialogue asset; in a dialogue it is the answer with the action \"Разговор о взятых заданиях\").")]
        [SerializeField] private string takenChoice = QuestDialogue.DefaultTakenChoice;
        [Tooltip("Said above the list of quests the player has taken.")]
        [TextArea] [SerializeField] private string takenText = "Ну? Как продвигается?";
        [Tooltip("Said when the player has taken nothing from this NPC.")]
        [TextArea] [SerializeField] private string noTakenText = DefaultNoTakenText;

        [Header("Dailies and refusals")]
        [Tooltip("How many dailies the giver offers a day (a day is a raid), picked at random from his repeatable quests. The ones taken and not done yet count.")]
        [SerializeField, Min(0)] private int dailySlots = 2;
        [Tooltip("Said when the player wants to refuse a quest, before they confirm.")]
        [TextArea] [SerializeField] private string abandonAskText = "Бросаешь? Ну смотри, я это запомню.";
        [Tooltip("Said when the player has refused a quest.")]
        [TextArea] [SerializeField] private string abandonedText = "Ладно. Найду кого-нибудь другого.";

        /// <summary>The NPC id: the name of the asset.</summary>
        public string Id => name;
        public string AskChoice => string.IsNullOrWhiteSpace(askChoice) ? DefaultAskChoice : askChoice;
        public string MenuText => menuText;
        public string NoWorkText => string.IsNullOrWhiteSpace(noWorkText) ? DefaultNoWorkText : noWorkText;
        public string AcceptedText => acceptedText;
        public string ReminderText => reminderText;
        public string ReadyText => readyText;
        public string NoRoomText => noRoomText;
        public string TakenChoice => string.IsNullOrWhiteSpace(takenChoice) ? QuestDialogue.DefaultTakenChoice : takenChoice;
        public string TakenText => takenText;
        public string NoTakenText => string.IsNullOrWhiteSpace(noTakenText) ? DefaultNoTakenText : noTakenText;
        public int DailySlots => Mathf.Max(0, dailySlots);
        public string AbandonAskText => abandonAskText;
        public string AbandonedText => abandonedText;
    }
}
