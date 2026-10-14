using ProjectMayham.Items;
using UnityEngine;

namespace ProjectMayham.Combat
{
    /// <summary>
    /// A melee weapon: an item that can be taken in the hands, plus everything that makes it swing the way it does.
    /// The weapon is data only; <see cref="MeleeCombat"/> on the player does the swinging.
    /// Make one with Create > Mayham > Weapon and add it to the item database (Mayham/Refresh Item Database).
    /// </summary>
    [CreateAssetMenu(menuName = "Mayham/Weapon", fileName = "NewWeapon")]
    public class WeaponDefinition : ItemDefinition
    {
        [Header("Damage")]
        [SerializeField, Min(0f)] private float damage = 20f;
        [Tooltip("Damage of a charged blow, as a multiple of the normal one.")]
        [SerializeField, Min(1f)] private float heavyDamageMultiplier = 2.2f;

        [Header("Reach")]
        [Tooltip("How far from the centre of the player the blow reaches, in world units.")]
        [SerializeField, Min(0.3f)] private float range = 1.6f;
        [Tooltip("Width of the swing in degrees.")]
        [SerializeField, Range(20f, 270f)] private float arc = 100f;
        [Tooltip("The charged blow sweeps this much wider than the normal one.")]
        [SerializeField, Min(0.5f)] private float heavyArcMultiplier = 1.15f;

        [Header("Timing")]
        [Tooltip("Seconds the swing itself takes. Smaller = a faster weapon.")]
        [SerializeField, Min(0.05f)] private float swingSeconds = 0.22f;
        [SerializeField, Min(0.05f)] private float heavySwingSeconds = 0.34f;
        [Tooltip("Seconds after a normal blow before the next action.")]
        [SerializeField, Min(0f)] private float recoverSeconds = 0.15f;
        [SerializeField, Min(0f)] private float heavyRecoverSeconds = 0.35f;
        [Tooltip("Seconds the attack button has to be held for the blow to be a charged one.")]
        [SerializeField, Min(0.1f)] private float chargeSeconds = 0.7f;

        [Header("Stamina")]
        [SerializeField, Min(0f)] private float staminaCost = 10f;
        [SerializeField, Min(0f)] private float heavyStaminaCost = 24f;

        [Header("Weight of the blow")]
        [Tooltip("Speed the victim is thrown back with, in units per second.")]
        [SerializeField, Min(0f)] private float knockback = 4f;
        [SerializeField, Min(1f)] private float heavyKnockbackMultiplier = 1.8f;
        [Tooltip("Seconds the victim loses its footing. A blow with no stagger does not interrupt anything.")]
        [SerializeField, Min(0f)] private float stagger = 0.35f;
        [SerializeField, Min(0f)] private float heavyStagger = 0.9f;
        [Tooltip("Seconds the whole game freezes when the blow lands.")]
        [SerializeField, Range(0f, 0.2f)] private float hitStop = 0.04f;
        [SerializeField, Range(0f, 0.25f)] private float heavyHitStop = 0.09f;
        [Tooltip("Camera shake of a landed blow, 0..1.")]
        [SerializeField, Range(0f, 1f)] private float shake = 0.25f;
        [SerializeField, Range(0f, 1f)] private float heavyShake = 0.6f;

        [Header("Stance")]
        [Tooltip("Walking speed with the weapon raised, as a share of the normal one.")]
        [SerializeField, Range(0.1f, 1f)] private float raisedSpeedMultiplier = 0.6f;
        [Tooltip("Walking speed in the middle of a swing.")]
        [SerializeField, Range(0f, 1f)] private float swingSpeedMultiplier = 0.3f;
        [Tooltip("Width of the view cone with the weapon raised, as a share of the normal one.")]
        [SerializeField, Range(0.2f, 1f)] private float raisedConeScale = 0.6f;

        [Header("Wear")]
        [Tooltip("Hits the weapon takes before it breaks. 0 = it never wears out.")]
        [SerializeField, Min(0f)] private float maxDurability = 60f;
        [Tooltip("Durability lost by one landed blow. A charged one costs the multiple below.")]
        [SerializeField, Min(0f)] private float durabilityPerHit = 1f;
        [SerializeField, Min(1f)] private float heavyWearMultiplier = 2f;
        [Tooltip("What is left in the hands when the weapon breaks (scrap); empty = nothing.")]
        [SerializeField] private ItemDefinition brokenItem;

        [Header("Noise")]
        [Tooltip("How far an enemy hears the swing, in world units.")]
        [SerializeField, Min(0f)] private float swingNoise = 4f;
        [Tooltip("How far an enemy hears a landed blow.")]
        [SerializeField, Min(0f)] private float hitNoise = 8f;

        public float Damage => damage;
        public float HeavyDamage => damage * heavyDamageMultiplier;
        public float Range => range;
        public float Arc => arc;
        public float HeavyArc => Mathf.Min(360f, arc * heavyArcMultiplier);
        public float SwingSeconds => swingSeconds;
        public float HeavySwingSeconds => heavySwingSeconds;
        public float RecoverSeconds => recoverSeconds;
        public float HeavyRecoverSeconds => heavyRecoverSeconds;
        public float ChargeSeconds => chargeSeconds;
        public float StaminaCost => staminaCost;
        public float HeavyStaminaCost => heavyStaminaCost;
        public float Knockback => knockback;
        public float HeavyKnockback => knockback * heavyKnockbackMultiplier;
        public float Stagger => stagger;
        public float HeavyStagger => heavyStagger;
        public float HitStop => hitStop;
        public float HeavyHitStop => heavyHitStop;
        public float Shake => shake;
        public float HeavyShake => heavyShake;
        public float RaisedSpeedMultiplier => raisedSpeedMultiplier;
        public float SwingSpeedMultiplier => swingSpeedMultiplier;
        public float RaisedConeScale => raisedConeScale;
        public float MaxDurability => maxDurability;
        public bool Wears => maxDurability > 0f && durabilityPerHit > 0f;
        public float WearPerHit(bool heavy) => durabilityPerHit * (heavy ? heavyWearMultiplier : 1f);
        public ItemDefinition BrokenItem => brokenItem;
        public float SwingNoise => swingNoise;
        public float HitNoise => hitNoise;

        public float DamageOf(bool heavy) => heavy ? HeavyDamage : damage;
        public float ArcOf(bool heavy) => heavy ? HeavyArc : arc;
        public float SwingSecondsOf(bool heavy) => heavy ? heavySwingSeconds : swingSeconds;
        public float RecoverSecondsOf(bool heavy) => heavy ? heavyRecoverSeconds : recoverSeconds;
        public float StaminaCostOf(bool heavy) => heavy ? heavyStaminaCost : staminaCost;
    }
}
