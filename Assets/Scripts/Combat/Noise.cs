using System;
using UnityEngine;

namespace ProjectMayham.Combat
{
    /// <summary>A sound that somebody may hear.</summary>
    public struct NoiseEvent
    {
        public Vector2 Position;
        /// <summary>How far it carries through open air, in world units.</summary>
        public float Radius;
        public GameObject Source;
        /// <summary>Whose noise it is. Enemies only listen to the noise of the player's side.</summary>
        public Team Team;
    }

    /// <summary>
    /// The gameplay side of sound: who makes how much noise where. Footsteps, swings and landed blows put it out,
    /// enemies listen. (What the player hears in the speakers is a different matter: see <see cref="CombatEvents"/>.)
    /// </summary>
    public static class Noise
    {
        public static event Action<NoiseEvent> Emitted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Emitted = null;

        public static void Emit(Vector2 position, float radius, GameObject source, Team team = Team.Player)
        {
            if (radius <= 0f) return;
            Emitted?.Invoke(new NoiseEvent { Position = position, Radius = radius, Source = source, Team = team });
        }
    }
}
