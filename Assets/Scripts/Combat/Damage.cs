using UnityEngine;

namespace ProjectMayham.Combat
{
    /// <summary>Whose side something is on. Attacks only hurt the other teams, so enemies do not cut each other down.</summary>
    public enum Team
    {
        Player,
        Enemy,
        /// <summary>Hurt by everyone (a breakable prop).</summary>
        Neutral
    }

    /// <summary>One blow: how much it hurts, where it came from and how hard it pushes.</summary>
    public struct DamageInfo
    {
        public float Amount;
        /// <summary>Where the blow landed.</summary>
        public Vector2 Point;
        /// <summary>From the attacker towards the victim, unit length.</summary>
        public Vector2 Direction;
        /// <summary>Speed the victim is thrown away with, in units per second.</summary>
        public float Knockback;
        /// <summary>Seconds the victim loses its footing for. 0 = the blow does not stagger.</summary>
        public float StaggerSeconds;
        public bool Heavy;
        public GameObject Attacker;
        /// <summary>The weapon that dealt it; null for the claws of an enemy.</summary>
        public WeaponDefinition Weapon;
    }

    /// <summary>Anything a blow can hurt: the player, an enemy, a prop.</summary>
    public interface IDamageable
    {
        Team Team { get; }
        bool IsAlive { get; }
        void TakeDamage(in DamageInfo info);
    }
}
