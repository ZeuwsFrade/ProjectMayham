using UnityEngine;

namespace ProjectMayham.Core
{
    /// <summary>
    /// Counts the open modal windows (dialogue, trade, stash, confirmations). While any is open the player cannot
    /// interact with the world, and the game is paused unless every open window lets the time go on (the window
    /// of a container that is being searched does).
    /// </summary>
    public static class ModalState
    {
        private static int count;
        // Open windows that stop the time.
        private static int pausing;

        public static bool IsOpen => count > 0;
        /// <summary>True while a window that stops the time is open.</summary>
        public static bool IsPaused => pausing > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            count = 0;
            pausing = 0;
            Time.timeScale = 1f;
        }

        /// <param name="pause">False for a window behind which the game goes on.</param>
        public static void Push(bool pause = true)
        {
            count++;
            if (!pause) return;
            pausing++;
            Time.timeScale = 0f;
        }

        /// <param name="pause">The value the window was pushed with.</param>
        public static void Pop(bool pause = true)
        {
            if (count == 0) return;
            count--;
            if (pause && pausing > 0) pausing--;
            if (pausing == 0) Time.timeScale = 1f;
        }
    }
}
