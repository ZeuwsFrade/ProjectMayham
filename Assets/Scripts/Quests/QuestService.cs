using System.Collections.Generic;

namespace ProjectMayham.Quests
{
    /// <summary>
    /// STUB of the quest system. The design document describes quests from Tyler and from NPCs met in raids; none
    /// of that exists yet. The dialogue already asks this service, so a real implementation only has to replace
    /// the body of <see cref="GetAvailable"/> and add a quest log.
    /// </summary>
    public static class QuestService
    {
        /// <summary>Titles of the quests this NPC can give right now. Always empty until quests are implemented.</summary>
        public static IReadOnlyList<string> GetAvailable(string npcId) => System.Array.Empty<string>();
    }
}
