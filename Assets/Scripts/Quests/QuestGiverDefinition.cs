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

        [Tooltip("The answer of the player that opens the talk about work.")]
        [SerializeField] private string askChoice = DefaultAskChoice;
        [Tooltip("Said above the list of quests.")]
        [TextArea] [SerializeField] private string menuText = "Есть пара дел.";
        [Tooltip("Said when there is nothing to offer, remind of or take.")]
        [TextArea] [SerializeField] private string noWorkText = DefaultNoWorkText;
        [Tooltip("Said when the player takes a quest.")]
        [TextArea] [SerializeField] private string acceptedText = "Договорились.";
        [Tooltip("Said about an unfinished quest that has no reminder of its own.")]
        [TextArea] [SerializeField] private string reminderText = "Ты ещё не всё сделал.";
        [Tooltip("Said when everything for a quest is there.")]
        [TextArea] [SerializeField] private string readyText = "Вижу, всё при тебе.";
        [Tooltip("Said when the reward does not fit into the backpack.")]
        [TextArea] [SerializeField] private string noRoomText = "Тебе некуда положить награду. Освободи место и приходи.";

        /// <summary>The NPC id: the name of the asset.</summary>
        public string Id => name;
        public string AskChoice => string.IsNullOrWhiteSpace(askChoice) ? DefaultAskChoice : askChoice;
        public string MenuText => menuText;
        public string NoWorkText => string.IsNullOrWhiteSpace(noWorkText) ? DefaultNoWorkText : noWorkText;
        public string AcceptedText => acceptedText;
        public string ReminderText => reminderText;
        public string ReadyText => readyText;
        public string NoRoomText => noRoomText;
    }
}
