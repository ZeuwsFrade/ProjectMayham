using System.Collections.Generic;
using ProjectMayham.Combat;
using UnityEngine;

namespace ProjectMayham.AI
{
    /// <summary>
    /// Adds up what all enemies feel into one alert level for the music: calm, suspicious, alarm. A rise is
    /// published at once; a fall waits a few seconds, so that the music does not flicker when one enemy loses
    /// sight of the player for a moment. The level goes out as <see cref="CombatEvents.AlertChanged"/>.
    /// </summary>
    public static class AlertDirector
    {
        private static readonly Dictionary<EnemyAI, AlertLevel> reports = new Dictionary<EnemyAI, AlertLevel>();
        private static AlertLevel current = AlertLevel.Calm;
        private static float lowerTimer;

        /// <summary>Seconds the alarm lasts after the last hunter gave up.</summary>
        public const float AlarmHoldSeconds = 5f;
        /// <summary>Seconds the suspicion lasts after the last suspicious enemy calmed down.</summary>
        public const float SuspicionHoldSeconds = 2.5f;

        public static AlertLevel Current => current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            reports.Clear();
            current = AlertLevel.Calm;
            lowerTimer = 0f;
        }

        /// <summary>An enemy tells how alert it is now.</summary>
        public static void Report(EnemyAI enemy, AlertLevel level)
        {
            if (level == AlertLevel.Calm) reports.Remove(enemy);
            else reports[enemy] = level;
            Recompute();
        }

        /// <summary>The enemy is gone (dead, destroyed).</summary>
        public static void Remove(EnemyAI enemy)
        {
            if (reports.Remove(enemy)) Recompute();
        }

        /// <summary>Called every frame by <see cref="CombatRuntime"/> with real time, to let a lowered level go through.</summary>
        public static void Tick(float unscaledDelta)
        {
            if (lowerTimer <= 0f) return;

            lowerTimer -= unscaledDelta;
            if (lowerTimer <= 0f) Set(Highest());
        }

        private static void Recompute()
        {
            CombatRuntime.Ensure();
            var highest = Highest();
            if (highest > current)
            {
                lowerTimer = 0f;
                Set(highest);
            }
            else if (highest < current)
            {
                // The level falls only after a pause, and only if it is still lower by then. A pause that is
                // already running is not started over by every report.
                if (lowerTimer <= 0f) lowerTimer = current == AlertLevel.Alarm ? AlarmHoldSeconds : SuspicionHoldSeconds;
            }
            else lowerTimer = 0f;
        }

        private static AlertLevel Highest()
        {
            var highest = AlertLevel.Calm;
            foreach (var level in reports.Values)
            {
                if (level > highest) highest = level;
            }
            return highest;
        }

        private static void Set(AlertLevel level)
        {
            lowerTimer = 0f;
            if (level == current) return;
            current = level;
            CombatEvents.RaiseAlertChanged(level);
        }
    }
}
