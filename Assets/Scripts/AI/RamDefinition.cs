using UnityEngine;

namespace ProjectMayham.AI
{
    /// <summary>
    /// The numbers of a <see cref="Ram"/>: everything of an ordinary enemy plus its charge. Its "attack" is the charge:
    /// <see cref="EnemyDefinition.AttackRange"/> is how near the player has to be seen for it to start aiming,
    /// <see cref="EnemyDefinition.WindupSeconds"/> is how long it aims, and <see cref="EnemyDefinition.TurnSpeed"/> how
    /// quickly it can follow the player while aiming.
    /// </summary>
    [CreateAssetMenu(menuName = "Mayham/Enemy (Ram)", fileName = "NewRam")]
    public class RamDefinition : EnemyDefinition
    {
        [Header("Charge")]
        [SerializeField, Min(1f)] private float chargeSpeed = 9f;
        [Tooltip("How quickly it gets up to speed, units/s^2.")]
        [SerializeField, Min(1f)] private float chargeAcceleration = 26f;
        [Tooltip("How far it runs when nothing stops it, in world units.")]
        [SerializeField, Min(1f)] private float chargeDistance = 16f;
        [Tooltip("Damage to the player it runs down.")]
        [SerializeField, Min(0f)] private float chargeDamage = 30f;
        [Tooltip("Damage to any other enemy in its way.")]
        [SerializeField, Min(0f)] private float trampleDamage = 30f;
        [Tooltip("Speed whatever it runs down is thrown away with.")]
        [SerializeField, Min(0f)] private float chargeKnockback = 10f;
        [Tooltip("Seconds whatever it runs down loses its footing for.")]
        [SerializeField, Min(0f)] private float chargeStagger = 0.9f;
        [Tooltip("Seconds it stands dazed after running into a wall. The time to hit it.")]
        [SerializeField, Min(0f)] private float stunSeconds = 2.5f;
        [Tooltip("How far the crash into a wall is heard.")]
        [SerializeField, Min(0f)] private float crashNoise = 11f;
        [Tooltip("How far its steps are heard while it runs.")]
        [SerializeField, Min(0f)] private float chargeNoise = 5f;

        public float ChargeSpeed => chargeSpeed;
        public float ChargeAcceleration => chargeAcceleration;
        public float ChargeDistance => chargeDistance;
        public float ChargeDamage => chargeDamage;
        public float TrampleDamage => trampleDamage;
        public float ChargeKnockback => chargeKnockback;
        public float ChargeStagger => chargeStagger;
        public float StunSeconds => stunSeconds;
        public float CrashNoise => crashNoise;
        public float ChargeNoise => chargeNoise;
    }
}
