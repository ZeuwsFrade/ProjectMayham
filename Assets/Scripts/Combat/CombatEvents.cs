using System;
using UnityEngine;

namespace ProjectMayham.Combat
{
    /// <summary>A blow is being swung.</summary>
    public struct SwingEvent
    {
        public GameObject Attacker;
        /// <summary>The weapon; null for an enemy's own attack.</summary>
        public WeaponDefinition Weapon;
        public Vector2 Position;
        /// <summary>Unit vector the blow is aimed along.</summary>
        public Vector2 Direction;
        public bool Heavy;
    }

    /// <summary>A blow has landed.</summary>
    public struct HitEvent
    {
        public GameObject Attacker;
        public WeaponDefinition Weapon;
        public IDamageable Target;
        public Vector2 Point;
        public Vector2 Direction;
        public float Damage;
        public bool Heavy;
        /// <summary>The blow killed the target.</summary>
        public bool Killed;
    }

    /// <summary>How much of the world is awake to the player. Music follows it.</summary>
    public enum AlertLevel
    {
        Calm,
        /// <summary>Somebody heard or half saw something and is looking into it.</summary>
        Suspicious,
        /// <summary>Somebody hunts the player.</summary>
        Alarm
    }

    /// <summary>
    /// What the rest of the game hears about the fight. Sound, music and effects subscribe here and know nothing of
    /// the player or the enemies. Handlers are dropped whenever a play session starts, so a stopped game leaves nothing behind.
    /// </summary>
    public static class CombatEvents
    {
        /// <summary>The swing began (the wind-up of an enemy as well): the swish.</summary>
        public static event Action<SwingEvent> SwingStarted;
        /// <summary>A blow landed: the thud.</summary>
        public static event Action<HitEvent> HitLanded;
        /// <summary>A blow went through the air and touched nothing.</summary>
        public static event Action<SwingEvent> SwingMissed;
        public static event Action<WeaponDefinition> WeaponBroken;
        /// <summary>Anyone who dies, the player included.</summary>
        public static event Action<GameObject> ActorDied;
        /// <summary>A shout that wakes others (a Dokhodyaga calling its pack).</summary>
        public static event Action<GameObject, Vector2> EnemyShouted;
        /// <summary>The highest alert of all enemies changed. See <see cref="AI.AlertDirector"/>.</summary>
        public static event Action<AlertLevel> AlertChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SwingStarted = null;
            HitLanded = null;
            SwingMissed = null;
            WeaponBroken = null;
            ActorDied = null;
            EnemyShouted = null;
            AlertChanged = null;
        }

        public static void RaiseSwingStarted(in SwingEvent e) => SwingStarted?.Invoke(e);
        public static void RaiseHitLanded(in HitEvent e) => HitLanded?.Invoke(e);
        public static void RaiseSwingMissed(in SwingEvent e) => SwingMissed?.Invoke(e);
        public static void RaiseWeaponBroken(WeaponDefinition weapon) => WeaponBroken?.Invoke(weapon);
        public static void RaiseActorDied(GameObject actor) => ActorDied?.Invoke(actor);
        public static void RaiseEnemyShouted(GameObject enemy, Vector2 position) => EnemyShouted?.Invoke(enemy, position);
        public static void RaiseAlertChanged(AlertLevel level) => AlertChanged?.Invoke(level);
    }
}
