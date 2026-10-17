using ProjectMayham.Core;
using UnityEngine;

namespace ProjectMayham.Combat
{
    /// <summary>
    /// The brief freeze of the whole game when a blow lands: it makes a hit feel heavy. Goes through
    /// <see cref="ModalState.Slowdown"/>, so it never lifts a pause that a window put on.
    /// </summary>
    public static class HitStop
    {
        // Longer freezes are a bug, not a style.
        private const float MaxSeconds = 0.25f;

        private static float remaining;

        public static bool IsActive => remaining > 0f;

        /// <summary>Freezes the game for <paramref name="seconds"/> of real time. A longer freeze already running is kept.</summary>
        /// <param name="scale">Time scale during the freeze: 0 = stopped, a little above 0 = a crawl.</param>
        public static void Request(float seconds, float scale = 0f)
        {
            if (seconds <= 0f) return;

            CombatRuntime.Ensure();
            remaining = Mathf.Min(MaxSeconds, Mathf.Max(remaining, seconds));
            ModalState.Slowdown = scale;
        }

        /// <summary>Called by <see cref="CombatRuntime"/> every frame with the real time passed.</summary>
        public static void Tick(float unscaledDelta)
        {
            if (remaining <= 0f) return;

            remaining -= unscaledDelta;
            if (remaining > 0f) return;
            remaining = 0f;
            ModalState.Slowdown = 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => remaining = 0f;
    }
}
