using System;
using ProjectMayham.Combat;
using ProjectMayham.Items;
using UnityEngine;

namespace ProjectMayham.Player
{
    /// <summary>
    /// Health and stamina. Sprinting drains stamina (faster the more is carried); after a short pause without
    /// sprinting it regenerates, and the health is healed slowly at the cost of stamina. Both depend on the needs
    /// (<see cref="PlayerNeeds"/>): hunger lowers the stamina the player can use, a wound slows the stamina down
    /// (the less health, the slower), thirst makes both worse. Reaching zero stamina leaves the player out of
    /// breath: no sprinting until it is back to a threshold.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        // Changes smaller than this do not raise events.
        private const float Epsilon = 0.0001f;

        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [Tooltip("Health healed per second while the player rests and has stamina to pay for it.")]
        [SerializeField, Min(0f)] private float healthRegen = 0.8f;
        [Tooltip("Stamina paid for a point of health (more when thirsty).")]
        [SerializeField, Min(0f)] private float staminaPerHealth = 2f;

        [Header("Stamina")]
        [SerializeField, Min(1f)] private float maxStamina = 100f;
        [Tooltip("Stamina spent per second of sprinting with nothing carried.")]
        [SerializeField, Min(0f)] private float sprintDrain = 20f;
        [Tooltip("Stamina restored per second once the regeneration delay is over (at full health, not thirsty).")]
        [SerializeField, Min(0f)] private float regenRate = 15f;
        [Tooltip("Seconds without sprinting (or taking damage) before regeneration starts.")]
        [SerializeField, Min(0f)] private float regenDelay = 1f;
        [Tooltip("Regeneration delay after stamina has run out completely.")]
        [SerializeField, Min(0f)] private float exhaustedRegenDelay = 2f;
        [Tooltip("Share of the usable stamina needed to sprint again after running out.")]
        [SerializeField, Range(0f, 1f)] private float exhaustedRecoverThreshold = 0.3f;
        [Tooltip("Share of the stamina that full hunger takes away.")]
        [SerializeField, Range(0f, 0.9f)] private float hungerStaminaLoss = 0.5f;
        [Tooltip("The stamina regeneration never drops below this share of its speed, however wounded and thirsty.")]
        [SerializeField, Range(0f, 1f)] private float minRegenShare = 0.1f;

        [Header("Carried weight")]
        [Tooltip("Sprint drain multiplier when carrying exactly the comfortable weight (empty hands = 1).")]
        [SerializeField, Min(1f)] private float drainAtComfortableWeight = 2f;

        private PlayerController controller;
        private Inventory inventory;
        private PlayerNeeds needs;

        private float health;
        private float stamina;
        private float cap;
        private float regenTimer;
        private bool exhausted;
        private bool recoveringHealth;
        private bool dead;

        /// <summary>(current, max) after every change of health.</summary>
        public event Action<float, float> HealthChanged;
        /// <summary>(current, max) after every change of stamina.</summary>
        public event Action<float, float> StaminaChanged;
        /// <summary>(usable, max) when hunger changes how much of the stamina can be used.</summary>
        public event Action<float, float> StaminaCapChanged;
        /// <summary>True when the player runs out of breath, false when they can sprint again.</summary>
        public event Action<bool> ExhaustedChanged;
        /// <summary>True while the health is healing.</summary>
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
        /// <summary>The stamina the player can use now: hunger takes part of the scale away.</summary>
        public float StaminaCap => cap;
        public float StaminaCapFraction => cap / maxStamina;
        public bool IsExhausted => exhausted;
        public bool IsRecoveringHealth => recoveringHealth;
        public bool IsDead => dead;
        public bool CanSprint => !dead && !exhausted && stamina > 0f;
        /// <summary>Hunger, thirst and the call of nature.</summary>
        public PlayerNeeds Needs => needs;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            inventory = GetComponent<Inventory>();
            needs = GetComponent<PlayerNeeds>();
            if (needs == null) needs = gameObject.AddComponent<PlayerNeeds>();
            health = maxHealth;
            cap = maxStamina;
            stamina = maxStamina;
        }

        private void Start()
        {
            // Lets listeners that subscribed in OnEnable show the starting values.
            HealthChanged?.Invoke(health, maxHealth);
            StaminaChanged?.Invoke(stamina, maxStamina);
            StaminaCapChanged?.Invoke(cap, maxStamina);
        }

        private void Update()
        {
            if (dead) return;

            ApplyCap();

            float dt = Time.deltaTime;
            if (controller.IsSprinting)
            {
                SetRecoveringHealth(false);
                Drain(sprintDrain * DrainMultiplier() * dt);
                return;
            }

            if (regenTimer > 0f)
            {
                regenTimer -= dt;
                SetRecoveringHealth(false);
                return;
            }
            RegenerateStamina(regenRate * dt);
            RegenerateHealth(dt);
        }

        Team IDamageable.Team => Team.Player;
        bool IDamageable.IsAlive => !dead;

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
                CombatEvents.RaiseActorDied(gameObject);
            }
        }

        /// <summary>A blow from an enemy: the damage, a shove away from it and a shake of the screen.</summary>
        void IDamageable.TakeDamage(in DamageInfo info)
        {
            if (dead) return;

            TakeDamage(info.Amount);
            if (info.Knockback > 0f) controller.Knock(info.Direction * info.Knockback);
            CameraShake.Add(Mathf.Clamp01(info.Amount / maxHealth * 2.5f));
            HitStop.Request(dead ? 0.12f : 0.04f);
        }

        public void Heal(float amount)
        {
            if (dead || amount <= 0f) return;
            SetHealth(health + amount);
        }

        /// <summary>Gives stamina at once (coffee). It cannot go over the stamina the player can use, and ends being out of breath if it is enough.</summary>
        public void AddStamina(float amount)
        {
            if (dead || amount <= 0f) return;
            SetStamina(stamina + amount);
            if (exhausted && stamina >= cap * exhaustedRecoverThreshold) SetExhausted(false);
        }

        /// <summary>Sets health without damage effects (loading a save, sleeping). A dead player stays dead.</summary>
        public void RestoreHealth(float value)
        {
            if (dead) return;
            SetHealth(Mathf.Max(1f, value));
            SetRecoveringHealth(false);
        }

        /// <summary>Health, stamina and the needs back to the best.</summary>
        public void RestoreAll()
        {
            if (dead) return;
            needs.RestoreAll();
            ApplyCap();
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

        /// <summary>Hunger moves the top of the scale; what was above it is lost.</summary>
        private void ApplyCap()
        {
            float newCap = NeedsMath.StaminaCap(maxStamina, needs.Hunger, hungerStaminaLoss);
            if (Mathf.Abs(newCap - cap) >= Epsilon)
            {
                cap = newCap;
                StaminaCapChanged?.Invoke(cap, maxStamina);
            }
            if (stamina > cap) SetStamina(cap);
        }

        private void RegenerateStamina(float amount)
        {
            amount *= NeedsMath.StaminaRegenFactor(HealthFraction, needs.Thirst, minRegenShare);
            SetStamina(Mathf.Min(stamina + amount, cap));
            if (exhausted && stamina >= cap * exhaustedRecoverThreshold) SetExhausted(false);
        }

        /// <summary>Health heals slowly and the stamina pays for it: when the stamina is out, the healing stops.</summary>
        private void RegenerateHealth(float dt)
        {
            if (health >= maxHealth - Epsilon || exhausted || stamina <= 0f)
            {
                SetRecoveringHealth(false);
                return;
            }

            float gain = healthRegen * needs.HealthRegenMultiplier * NeedsMath.HealthRegenThirstFactor(needs.Thirst) * dt;
            gain = Mathf.Min(gain, maxHealth - health);
            float price = NeedsMath.StaminaPerHealth(staminaPerHealth, needs.Thirst);
            float cost = gain * price;
            if (cost > stamina)
            {
                gain *= stamina / cost;
                cost = stamina;
            }
            if (gain <= 0f)
            {
                SetRecoveringHealth(false);
                return;
            }

            SetHealth(health + gain);
            SetStamina(stamina - cost);
            SetRecoveringHealth(true);
        }

        private void SetHealth(float value)
        {
            value = Mathf.Clamp(value, 0f, maxHealth);
            if (Mathf.Approximately(value, health)) return;
            health = value;
            HealthChanged?.Invoke(health, maxHealth);
        }

        private void SetStamina(float value)
        {
            value = Mathf.Clamp(value, 0f, cap);
            if (Mathf.Approximately(value, stamina)) return;
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
