using System.Collections.Generic;
using ProjectMayham.Trade;
using UnityEngine;

namespace ProjectMayham.Quests
{
    /// <summary>
    /// One quest: who gives it, what unlocks it, what it asks for, what the giver says and what it pays.
    /// Lives in Resources/Quests/&lt;giver&gt;; the name of the asset is the id in the save file, so renaming an
    /// asset resets the progress of that quest (use <see cref="order"/> to arrange the list instead).
    /// </summary>
    [CreateAssetMenu(menuName = "Mayham/Quests/Quest", fileName = "NewQuest")]
    public class QuestDefinition : ScriptableObject
    {
        [Tooltip("Who gives the quest and takes it back.")]
        [SerializeField] private QuestGiverDefinition giver;
        [Tooltip("The quest is offered once all of these have been turned in. Empty: offered from the start.")]
        [SerializeField] private List<QuestDefinition> requires = new List<QuestDefinition>();
        [Tooltip("Place in the list of offers: smaller comes first.")]
        [SerializeField] private int order;
        [Tooltip("The loyalty level of the giver it takes for the quest to be offered (1: any).")]
        [SerializeField, Range(1, 4)] private int minLoyalty = 1;
        [Tooltip("Offered only after the player gave all of these answers (ids of remembered choices in the dialogues).")]
        [SerializeField] private List<string> requiredChoices = new List<string>();
        [Tooltip("Never offered once the player gave one of these answers: the other branch of a fork.")]
        [SerializeField] private List<string> excludingChoices = new List<string>();

        [Tooltip("Short name, shown on the answer button.")]
        [SerializeField] private string title = "Квест";
        [Tooltip("What the giver says when offering. An empty line starts a new page.")]
        [TextArea(3, 8)] [SerializeField] private string offerText;
        [SerializeField] private List<QuestObjective> objectives = new List<QuestObjective>();
        [Tooltip("What the giver says while the quest is not done. Empty: the giver's general line.")]
        [TextArea(2, 6)] [SerializeField] private string reminderText;
        [Tooltip("The thanks at turn-in. An empty line starts a new page.")]
        [TextArea(3, 8)] [SerializeField] private string completedText;

        [Header("Reward")]
        [SerializeField, Min(0)] private int rewardMoney;
        [SerializeField] private List<ItemAmount> rewardItems = new List<ItemAmount>();
        [Tooltip("Loyalty points of the giver for handing it in. Hidden from the player: not shown in the reward.")]
        [SerializeField, Min(0)] private int rewardLoyalty;
        [Tooltip("A skill the player learns by handing it in (an id, for example \"vending_shake\": shaking vending machines). Empty: none.")]
        [SerializeField] private string rewardSkill;

        [Header("Daily and refusal")]
        [Tooltip("A daily: offered again after it is turned in or refused. Every day (a day is a raid) the giver offers a few of his dailies, picked at random.")]
        [SerializeField] private bool repeatable;
        [Tooltip("Raids after turning in or refusing before a daily can be offered again.")]
        [SerializeField, Min(1)] private int repeatAfterRaids = 1;
        [Tooltip("The player may refuse the quest after taking it; the giver trusts them less then. Never for a rescue. A refused quest is offered again the next day.")]
        [SerializeField] private bool canAbandon;
        [Tooltip("Loyalty points the giver takes off for a refusal (hidden from the player). 0: as many as the quest gives.")]
        [SerializeField, Min(0)] private int abandonPenalty;

        /// <summary>Stable key used in the save file: the name of the asset.</summary>
        public string Id => name;
        public QuestGiverDefinition Giver => giver;
        public IReadOnlyList<QuestDefinition> Requires => requires;
        public int Order => order;
        public int MinLoyalty => Mathf.Clamp(minLoyalty, 1, 4);
        public IReadOnlyList<string> RequiredChoices => requiredChoices;
        public IReadOnlyList<string> ExcludingChoices => excludingChoices;
        public int RewardLoyalty => Mathf.Max(0, rewardLoyalty);
        public string Title => string.IsNullOrWhiteSpace(title) ? name : title;
        public string OfferText => offerText;
        public IReadOnlyList<QuestObjective> Objectives => objectives;
        public string ReminderText => reminderText;
        public string CompletedText => completedText;
        public int RewardMoney => Mathf.Max(0, rewardMoney);
        public IReadOnlyList<ItemAmount> RewardItems => rewardItems;
        public string RewardSkill => rewardSkill != null ? rewardSkill.Trim() : string.Empty;
        /// <summary>A daily: offered again some raids after it was turned in or refused.</summary>
        public bool Repeatable => repeatable && !IsRescue;
        public int RepeatAfterRaids => Mathf.Max(1, repeatAfterRaids);
        /// <summary>The flag as set in the Inspector; whether the player can refuse is <see cref="QuestLog.CanAbandon"/>.</summary>
        public bool CanAbandon => canAbandon;
        public int AbandonPenalty => abandonPenalty > 0 ? abandonPenalty : RewardLoyalty;
        /// <summary>Asks to bring the giver, lost in the maze, home: never repeated, never refused.</summary>
        public bool IsRescue => objectives.Exists(o => o != null && o.Kind == QuestObjectiveKind.Rescue);

        /// <summary>False when the quest cannot be offered or checked; <paramref name="reason"/> says why.</summary>
        public bool IsUsable(out string reason)
        {
            if (giver == null)
            {
                reason = "it has no giver";
                return false;
            }
            foreach (var objective in objectives)
            {
                if (objective != null && objective.IsUsable) continue;
                reason = "an objective has no item";
                return false;
            }
            reason = null;
            return true;
        }
    }
}
