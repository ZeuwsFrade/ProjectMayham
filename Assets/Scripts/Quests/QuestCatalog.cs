using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMayham.Quests
{
    /// <summary>
    /// Every quest and quest giver of the game, found in Resources/Quests (no list to keep up to date).
    /// Loaded on first use; a quest that cannot be checked is left out with a warning.
    /// </summary>
    public static class QuestCatalog
    {
        public const string ResourceFolder = "Quests";

        private static List<QuestDefinition> quests;
        private static Dictionary<string, QuestDefinition> byId;
        private static Dictionary<string, QuestGiverDefinition> givers;

        /// <summary>All usable quests, by order and then by name.</summary>
        public static IReadOnlyList<QuestDefinition> All
        {
            get
            {
                Load();
                return quests;
            }
        }

        /// <summary>Forgets what was loaded; the next use reads Resources again.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            quests = null;
            byId = null;
            givers = null;
        }

        /// <summary>The quest with this id, or null.</summary>
        public static QuestDefinition Find(string id)
        {
            Load();
            return id != null && byId.TryGetValue(id, out var quest) ? quest : null;
        }

        /// <summary>The quests of an NPC, in the order they are listed.</summary>
        public static List<QuestDefinition> ByGiver(string npcId)
        {
            Load();
            return quests.Where(q => q.Giver.Id == npcId).ToList();
        }

        /// <summary>The giver with this NPC id, or null when the NPC gives no quests.</summary>
        public static QuestGiverDefinition Giver(string npcId)
        {
            Load();
            return npcId != null && givers.TryGetValue(npcId, out var giver) ? giver : null;
        }

        /// <summary>Uses these instead of the assets in Resources until <see cref="Reset"/>: for the self-test.</summary>
        public static void Override(IEnumerable<QuestDefinition> allQuests, IEnumerable<QuestGiverDefinition> allGivers) =>
            Build(allQuests, allGivers);

        private static void Load()
        {
            if (quests != null) return;
            Build(Resources.LoadAll<QuestDefinition>(ResourceFolder), Resources.LoadAll<QuestGiverDefinition>(ResourceFolder));
        }

        private static void Build(IEnumerable<QuestDefinition> allQuests, IEnumerable<QuestGiverDefinition> allGivers)
        {
            givers = new Dictionary<string, QuestGiverDefinition>();
            foreach (var giver in allGivers)
            {
                if (giver != null) givers[giver.Id] = giver;
            }

            byId = new Dictionary<string, QuestDefinition>();
            var list = new List<QuestDefinition>();
            foreach (var quest in allQuests)
            {
                if (quest == null) continue;
                if (!quest.IsUsable(out string reason))
                {
                    Debug.LogWarning($"Quest '{quest.Id}' is skipped: {reason}.", quest);
                    continue;
                }
                if (byId.ContainsKey(quest.Id))
                {
                    Debug.LogWarning($"Two quests are named '{quest.Id}': the second one is skipped.", quest);
                    continue;
                }
                byId[quest.Id] = quest;
                list.Add(quest);
                // A giver that lies outside Resources/Quests is still known through its quests.
                if (!givers.ContainsKey(quest.Giver.Id)) givers[quest.Giver.Id] = quest.Giver;
            }
            list.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));
            quests = list;
        }
    }
}
