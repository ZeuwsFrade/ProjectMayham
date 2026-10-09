using System;
using ProjectMayham.Items;
using UnityEngine;

namespace ProjectMayham.Player
{
    /// <summary>
    /// Health and stamina. Sprinting drains stamina (faster the more is carried); after a short pause without
    /// sprinting it regenerates. Reaching zero leaves the player out of breath: no sprinting until stamina is back
    /// to a threshold. While health is below the low-health threshold the whole regeneration goes into a slow
    /// health regeneration instead, until health is back at the threshold.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerStats : MonoBehaviour
    {
        // Changes smaller than this do not raise events.
        private const float Epsilon = 0.0001f;

        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [Tooltip("Below this share of max health stamina regeneration is spent on healing instead.")]
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.25f;
        [Tooltip("Health restored per point of stamina regeneration while health is low.")]
        [SerializeField, Min(0f)] private float healthPerStamina = 0.1f;

        [Header("Stamina")]
        [SerializeField, Min(1f)] private float maxStamina = 100f;
        [Tooltip("Stamina spent per second of sprinting with nothing carried.")]
        [SerializeField, Min(0f)] private float sprintDrain = 20f;
        [Tooltip("Stamina restored per second once the regeneration delay is over.")]
        [SerializeField, Min(0f)] private float regenRate = 15f;
        [Tooltip("Seconds without sprinting (or taking damage) before regeneration starts.")]
        [SerializeField, Min(0f)] private float regenDelay = 1f;
        [Tooltip("Regeneration delay after stamina has run out completely.")]
        [SerializeField, Min(0f)] private float exhaustedRegenDelay = 2f;
        [Tooltip("Share of max stamina needed to sprint again after running out.")]
        [SerializeField, Range(0f, 1f)] private float exhaustedRecoverThreshold = 0.3f;

        [Header("Carried weight")]
        [Tooltip("Sprint drain multiplier when carrying exactly the comfortable weight (empty hands = 1).")]
        [SerializeField, Min(1f)] private float drainAtComfortableWeight = 2f;

        private PlayerController controller;
        private Inventory inventory;

        private float health;
        private float stamina;
        private float regenTimer;
        private bool exhausted;
        private bool recoveringHealth;
        private bool dead;

        /// <summary>(current, max) after every change of health.</summary>
        public event Action<float, float> HealthChanged;
        /// <summary>(current, max) after every change of stamina.</summary>
        public event Action<float, float> StaminaChanged;
        /// <summary>True when the player runs out of breath, false when they can sprint again.</summary>
        public event Action<bool> ExhaustedChanged;
        /// <summary>True while stamina regeneration is spent on healing.</summary>
        public event Action<bool> HealthRecoveryChanged;
        /// <summary>Amount of health actually lost.</summary>
        public event Action<float> Damaged;
        public event Action Died;

        public float Health => health;
        public float MaxHealth => maxHealth;
        public float HealthFraction => health / maxHealth;
        public float Stamina => stamina;
        public float MaxStamina => maxStamina;
        public float StaminaFraction => stamina / maxStamina;
        public bool IsExhausted => exhausted;
        public bool IsRecoveringHealth => recoveringHealth;
        public bool IsDead => dead;
        public bool CanSprint => !dead && !exhausted && stamina > 0f;

        private float LowHealth => maxHealth * lowHealthThreshold;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            inventory = GetComponent<Inventory>();
            health = maxHealth;
            stamina = maxStamina;
        }

        private void Start()
        {
            // Lets listeners that subscribed in OnEnable show the starting values.
            HealthChanged?.Invoke(health, maxHealth);
            StaminaChanged?.Invoke(stamina, maxStamina);
        }

        private void Update()
        {
            if (dead) return;

            float dt = Time.deltaTime;
            if (controller.IsSprinting)
            {
                Drain(sprintDrain * DrainMultiplier() * dt);
                return;
            }

            if (regenTimer > 0f)
            {
                regenTimer -= dt;
                return;
            }
            Regenerate(regenRate * dt);
        }

        public void TakeDamage(float amount)
        {
            if (dead || amount <= 0f) return;

            float before = health;
            SetHealth(health - amount);
            regenTimer = Mathf.Max(regenTimer, regenDelay);
            Damaged?.Invoke(before - health);

            if (health <= 0f)
            {
                dead = true;
                SetRecoveringHealth(false);
                Died?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (dead || amount <= 0f) return;
            SetHealth(health + amount);
        }

        /// <summary>Sets health without damage effects (loading a save, sleeping). A dead player stays dead.</summary>
        public void RestoreHealth(float value)
        {
            if (dead) return;
            SetHealth(Mathf.Max(1f, value));
            SetRecoveringHealth(false);
        }

        /// <summary>Health and stamina back to the maximum.</summary>
        public void RestoreAll()
        {
            if (dead) return;
            SetHealth(maxHealth);
            SetStamina(maxStamina);
            SetExhausted(false);
            SetRecoveringHealth(false);
        }

        /// <summary>
        /// Pays for a one-off effort (climbing over a barricade). False, with nothing spent, when there is not
        /// enough stamina or the player is out of breath.
        /// </summary>
        public bool TrySpendStamina(float amount)
        {
            if (amount <= 0f) return !dead;
            if (dead || exhausted || stamina < amount) return false;
            Drain(amount);
            return true;
        }

        private float DrainMultiplier()
        {
            if (inventory == null || inventory.ComfortableWeight <= 0f) return 1f;
            float load = Mathf.Clamp01(inventory.TotalWeight / inventory.ComfortableWeight);
            return Mathf.Lerp(1f, drainAtComfortableWeight, load);
        }

        private void Drain(float amount)
        {
            SetStamina(stamina - amount);
            regenTimer = regenDelay;

            if (stamina <= 0f && !exhausted)
            {
                regenTimer = exhaustedRegenDelay;
                SetExhausted(true);
            }
        }

        private void Regenerate(float amount)
        {
            // The whole regeneration goes into health until it is back at the threshold.
            bool lowHealth = health < LowHealth - Epsilon;
            SetRecoveringHealth(lowHealth);
            if (lowHealth)
            {
                SetHealth(Mathf.Min(health + amount * healthPerStamina, LowHealth));
                return;
            }

            SetStamina(stamina + amount);
            if (exhausted && stamina >= maxStamina * exhaustedRecoverThreshold) SetExhausted(false);
        }

        private void SetHealth(float value)
        {
            value = Mathf.Clamp(value, 0f, maxHealth);
            if (Mathf.Abs(value - health) < Epsilon) return;
            health = value;
            HealthChanged?.Invoke(health, maxHealth);
        }

        private void SetStamina(float value)
        {
            value = Mathf.Clamp(value, 0f, maxStamina);
            if (Mathf.Abs(value - stamina) < Epsilon) return;
            stamina = value;
            StaminaChanged?.Invoke(stamina, maxStamina);
        }

        private void SetExhausted(bool value)
        {
            if (exhausted == value) return;
            exhausted = value;
            ExhaustedChanged?.Invoke(exhausted);
        }

        private void SetRecoveringHealth(bool value)
        {
            if (recoveringHealth == value) return;
            recoveringHealth = value;
            HealthRecoveryChanged?.Invoke(recoveringHealth);
        }
    }
}
