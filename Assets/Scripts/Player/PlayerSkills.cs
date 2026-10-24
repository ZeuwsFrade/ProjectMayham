using System.Collections.Generic;
using ProjectMayham.Core;

namespace ProjectMayham.Player
{
    /// <summary>
    /// What the player has learned: skills opened by quests (<see cref="Quests.QuestDefinition.RewardSkill"/>), kept
    /// in the save file. A skill is an id; what it does lives where it is used.
    /// </summary>
    public static class PlayerSkills
    {
        /// <summary>Shaking a vending machine for a snack it holds on to.</summary>
        public const string VendingShake = "vending_shake";

        /// <summary>Every skill there is, with the name the player sees.</summary>
        public static readonly IReadOnlyDictionary<string, string> Known = new Dictionary<string, string>
        {
            { VendingShake, "Трясти автоматы" },
        };

        public static bool Has(string skill) => !string.IsNullOrEmpty(skill) && GameSession.Data.skills != null && GameSession.Data.skills.Contains(skill);

        public static void Learn(string skill)
        {
            if (string.IsNullOrWhiteSpace(skill) || Has(skill)) return;
            var data = GameSession.Data;
            data.skills ??= new List<string>();
            data.skills.Add(skill);
        }

        /// <summary>The name of a skill for the player, or the id of one nobody named.</summary>
        public static string Title(string skill) => Known.TryGetValue(skill, out var title) ? title : skill;
    }
}
