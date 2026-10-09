using UnityEngine;

namespace ProjectMayham.Core
{
    /// <summary>
    /// Counts the open modal windows (dialogue, trade, stash, confirmations). While any is open the game is paused
    /// and the player cannot interact with the world.
    /// </summary>
    public static class ModalState
    {
        private static int count;

        public static bool IsOpen => count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            count = 0;
            Time.timeScale = 1f;
        }

        public static void Push()
        {
            count++;
            Time.timeScale = 0f;
        }

        public static void Pop()
        {
            if (count == 0) return;
            count--;
            if (count == 0) Time.timeScale = 1f;
        }
    }
}
