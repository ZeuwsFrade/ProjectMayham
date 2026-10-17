using ProjectMayham.Core;

namespace ProjectMayham.Npcs
{
    /// <summary>
    /// Answers the story remembers: a dialogue answer with the action "remember choice" writes its id here, and
    /// quests and other answers can ask for it, or for its absence. This is how a quest line forks on what the player
    /// said. Kept in the save file.
    /// </summary>
    public static class PlayerChoices
    {
        public static bool Has(string id) => !string.IsNullOrWhiteSpace(id) && GameSession.Data.choices.Contains(id.Trim());

        public static void Remember(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            id = id.Trim();
            if (!GameSession.Data.choices.Contains(id)) GameSession.Data.choices.Add(id);
        }
    }
}
