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

        /// <summary>Stable key used in the save file: the name of the asset.</summary>
        public string Id => name;
        public QuestGiverDefinition Giver => giver;
        public IReadOnlyList<QuestDefinition> Requires => requires;
        public int Order => order;
        public string Title => string.IsNullOrWhiteSpace(title) ? name : title;
        public string OfferText => offerText;
        public IReadOnlyList<QuestObjective> Objectives => objectives;
        public string ReminderText => reminderText;
        public string CompletedText => completedText;
        public int RewardMoney => Mathf.Max(0, rewardMoney);
        public IReadOnlyList<ItemAmount> RewardItems => rewardItems;

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
