using System.Linq;
using ProjectMayham.Core;
using UnityEngine;

namespace ProjectMayham.Npcs
{
    /// <summary>
    /// How much an NPC trusts the player, as in Tarkov: points that quests give, and levels 1 to 4 that the points
    /// reach (the thresholds are in <see cref="NpcDefinition"/>). Levels open quests, answers in the dialogue and
    /// wares of the trader. The points live in the save file.
    /// </summary>
    public static class Loyalty
    {
        public const int MaxLevel = 4;

        public static int Points(string npcId)
        {
            var record = GameSession.Data.loyalty.Find(l => l.npc == npcId);
            return record != null ? record.points : 0;
        }

        public static int Level(string npcId)
        {
            var npc = NpcCatalog.Find(npcId);
            return LevelFor(npc != null ? npc.LoyaltyLevels : NpcDefinition.DefaultLevels, Points(npcId));
        }

        /// <summary>The level these points reach: 1 and one more for every threshold reached, 4 at most.</summary>
        public static int LevelFor(int[] thresholds, int points)
        {
            int level = 1 + thresholds.Take(MaxLevel - 1).Count(t => points >= t);
            return Mathf.Clamp(level, 1, MaxLevel);
        }

        /// <summary>Adds points (a negative amount takes them; never below zero).</summary>
        public static void Add(string npcId, int points)
        {
            if (string.IsNullOrEmpty(npcId) || points == 0) return;
            var list = GameSession.Data.loyalty;
            var record = list.Find(l => l.npc == npcId);
            if (record == null)
            {
                record = new LoyaltyData { npc = npcId };
                list.Add(record);
            }
            record.points = Mathf.Max(0, record.points + points);
        }
    }
}
