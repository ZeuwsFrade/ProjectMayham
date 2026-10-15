using UnityEngine;

namespace ProjectMayham.Core
{
    /// <summary>
    /// Counts the open modal windows (dialogue, trade, stash, confirmations). While any is open the player cannot
    /// interact with the world, and the game is paused unless every open window lets the time go on (the window
    /// of a container that is being searched does).
    /// Also owns the time scale, so that a pause and the short freeze of a hit (<see cref="Slowdown"/>) do not
    /// undo each other.
    /// </summary>
    public static class ModalState
    {
        private static int count;
        // Open windows that stop the time.
        private static int pausing;
        private static float slowdown = 1f;

        public static bool IsOpen => count > 0;
        /// <summary>True while a window that stops the time is open.</summary>
        public static bool IsPaused => pausing > 0;

        /// <summary>
        /// Time scale of the game while no window pauses it: 1 = normal, 0 = frozen. Only the freeze of a hit sets
        /// it; a pause wins over it.
        /// </summary>
        public static float Slowdown
        {
            get => slowdown;
            set
            {
                slowdown = Mathf.Clamp01(value);
                Apply();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            count = 0;
            pausing = 0;
            slowdown = 1f;
            Time.timeScale = 1f;
        }

        /// <param name="pause">False for a window behind which the game goes on.</param>
        public static void Push(bool pause = true)
        {
            count++;
            if (!pause) return;
            pausing++;
            Apply();
        }

        /// <param name="pause">The value the window was pushed with.</param>
        public static void Pop(bool pause = true)
        {
            if (count == 0) return;
            count--;
            if (pause && pausing > 0) pausing--;
            Apply();
        }

        private static void Apply() => Time.timeScale = pausing > 0 ? 0f : slowdown;
    }
}
