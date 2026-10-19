namespace ProjectMayham.Npcs
{
    /// <summary>
    /// When a lost NPC hides and when they come back: hidden as soon as an enemy is near, back once no enemy has
    /// been near for a while. Only the rule; <see cref="LostNpc"/> fades the NPC and stops the talk while hidden.
    /// </summary>
    public class NpcHiding
    {
        private float quiet;

        public bool Hidden { get; private set; }

        /// <summary>
        /// Moves on by <paramref name="seconds"/>: an enemy near hides the NPC (and keeps them hidden), a quiet of
        /// <paramref name="returnAfter"/> seconds brings them back. True when <see cref="Hidden"/> changed.
        /// </summary>
        public bool Step(bool enemyNear, float seconds, float returnAfter)
        {
            if (enemyNear)
            {
                quiet = 0f;
                if (Hidden) return false;
                Hidden = true;
                return true;
            }

            if (!Hidden) return false;
            quiet += seconds;
            if (quiet < returnAfter) return false;
            Hidden = false;
            quiet = 0f;
            return true;
        }
    }
}
