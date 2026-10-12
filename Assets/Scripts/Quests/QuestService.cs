using System.Collections.Generic;
using System.Linq;

namespace ProjectMayham.Quests
{
    /// <summary>
    /// The first, small door into the quest system, kept for code that only wants to know whether an NPC has work.
    /// The rules live in <see cref="QuestLog"/>, the conversation in <see cref="QuestDialogue"/>.
    /// </summary>
    public static class QuestService
    {
        /// <summary>Titles of the quests this NPC can give right now.</summary>
        public static IReadOnlyList<string> GetAvailable(string npcId) =>
            QuestLog.Offers(npcId).Select(quest => quest.Title).ToList();
    }
}
