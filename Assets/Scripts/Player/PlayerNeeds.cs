using System;
using ProjectMayham.Combat;
using ProjectMayham.Items;
using UnityEngine;

namespace ProjectMayham.Player
{
    /// <summary>
    /// Hunger, thirst and the call of nature. They grow with time (faster under a heavy load), are taken away by
    /// food, drink and fixtures, and act on the rest of the player: hunger takes stamina away, thirst slows healing
    /// down (see <see cref="PlayerStats"/>), a full call of nature slows the player and fills the air with a smell
    /// that the enemies come to. In the shelter they go back down by themselves; sleeping resets them.
    /// The component is added to the player by <see cref="PlayerStats"/>, so no scene has to be changed for it.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerNeeds : MonoBehaviour
    {
        private const float Epsilon = 0.0001f;
        // Changes smaller than this (since the last report) do not raise Changed.
        private const float ReportStep = 0.002f;

        [Header("Growth (seconds from nothing to full, in a raid)")]
        [SerializeField, Min(10f)] private float secondsToStarve = 1500f;
        [SerializeField, Min(10f)] private float secondsToDehydrate = 900f;
        [Tooltip("Hunger and thirst grow this many times faster at the max weight (1 at the comfortable weight).")]
        [SerializeField, Min(1f)] private float weightGrowth = 2.5f;

        [Header("In the shelter (seconds from full to nothing)")]
        [SerializeField, Min(10f)] private float shelterHungerSeconds = 900f;
        [SerializeField, Min(10f)] private float shelterThirstSeconds = 600f;
        [SerializeField, Min(10f)] private float shelterNatureSeconds = 300f;

        [Header("Call of nature")]
        [Tooltip("From this share the player slows down and smells.")]
        [SerializeField, Range(0.1f, 0.95f)] private float natureThreshold = 0.6f;
        [Tooltip("Walking speed at a full call of nature (1 = no slowing).")]
        [SerializeField, Range(0.2f, 1f)] private float speedAtFull = 0.7f;
        [Tooltip("How far the smell is heard by the enemies at a full call of nature, in world units.")]
        [SerializeField, Min(0f)] private float smellRadius = 7f;
        [SerializeField, Min(0.2f)] private float smellInterval = 1.2f;
        [Tooltip("What is left of the scale after an accident.")]
        [SerializeField, Range(0f, 0.5f)] private float afterAccident = 0.1f;
        [Tooltip("Seconds the smell of an accident stays with the player, at its strongest.")]
        [SerializeField, Min(0f)] private float accidentSmellSeconds = 90f;
        [Tooltip("The noise of an accident, in world units.")]
        [SerializeField, Min(0f)] private float accidentRadius = 14f;

        private PlayerStats stats;
        private Inventory inventory;
        private float hunger;
        private float thirst;
        private float nature;
        private Vector3 reported;
        private float boostMultiplier = 1f;
        private float boostTimer;
        private float smellTimer;
        private float accidentTimer;
        private bool inShelter;
        private bool shelterKnown;

        /// <summary>Raised when something worth telling happens ("Вы не удержались").</summary>
        public event Action<string> Notice;
        /// <summary>Raised when hunger, thirst or the call of nature changes.</summary>
        public event Action Changed;

        /// <summary>0 = fed, 1 = starving.</summary>
        public float Hunger => hunger;
        /// <summary>0 = not thirsty, 1 = parched.</summary>
        public float Thirst => thirst;
        /// <summary>0 = nothing, 1 = it cannot wait any longer.</summary>
        public float NatureCall => nature;
        /// <summary>Healing runs this many times faster for as long as a bandage or a meal works.</summary>
        public float HealthRegenMultiplier => boostTimer > 0f ? boostMultiplier : 1f;
        public float RegenBoostSecondsLeft => boostTimer;
        /// <summary>Speed of the player (the part that comes from the needs; the weight is the controller's business).</summary>
        public float SpeedFactor => NeedsMath.NatureSpeed(nature, natureThreshold, speedAtFull);
        /// <summary>True from an accident until its smell has gone.</summary>
        public bool IsSoiled => accidentTimer > 0f;
        public float NatureThreshold => natureThreshold;
        /// <summary>The player is in the shelter, where the needs go down by themselves.</summary>
        public bool InShelter
        {
            get
            {
                if (!shelterKnown)
                {
                    shelterKnown = true;
                    inShelter = FindFirstObjectByType<Level.RaidController>() == null;
                }
                return inShelter;
            }
            set { inShelter = value; shelterKnown = true; }
        }

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            inventory = GetComponent<Inventory>();
            // The pieces that go with the needs come with them.
            if (GetComponent<PlayerConsumer>() == null) gameObject.AddComponent<PlayerConsumer>();
            if (GetComponent<UI.NeedsHUD>() == null) gameObject.AddComponent<UI.NeedsHUD>();
        }

        private void Update()
        {
            if (stats != null && stats.IsDead) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (boostTimer > 0f) boostTimer = Mathf.Max(0f, boostTimer - dt);
            if (accidentTimer > 0f) accidentTimer = Mathf.Max(0f, accidentTimer - dt);

            if (InShelter) RecoverInShelter(dt);
            else Grow(dt);

            if (nature >= 1f - Epsilon && !InShelter) Accident();
            Smell(dt);
        }

        private void Grow(float dt)
        {
            float load = inventory != null ? NeedsMath.WeightNeedsMultiplier(inventory.Encumbrance, weightGrowth) : 1f;
            Set(hunger + dt * load / secondsToStarve, thirst + dt * load / secondsToDehydrate, nature);
        }

        private void RecoverInShelter(float dt)
        {
            Set(hunger - dt / shelterHungerSeconds, thirst - dt / shelterThirstSeconds, nature - dt / shelterNatureSeconds);
        }

        // ---- the smell ----

        private void Smell(float dt)
        {
            float radius = NeedsMath.SmellRadius(nature, natureThreshold, smellRadius);
            // After an accident the smell stays at its strongest and then fades with the time left.
            if (accidentTimer > 0f && accidentSmellSeconds > 0f)
                radius = Mathf.Max(radius, smellRadius * Mathf.Lerp(0.3f, 1.2f, accidentTimer / accidentSmellSeconds));
            if (radius <= 0f) { smellTimer = 0f; return; }

            smellTimer -= dt;
            if (smellTimer > 0f) return;
            smellTimer = smellInterval;
            Noise.Emit(transform.position, radius, gameObject);
        }

        private void Accident()
        {
            Set(hunger, thirst, afterAccident);
            accidentTimer = accidentSmellSeconds;
            Noise.Emit(transform.position, accidentRadius, gameObject);
            CameraShake.Add(0.15f);
            Notice?.Invoke("Вы не удержались…");
        }

        // ---- what the world does to the needs ----

        /// <summary>Eats or drinks a consumable: its effect on the needs, the health, the stamina and the healing.</summary>
        public void Apply(ConsumableDefinition item)
        {
            if (item == null) return;

            Set(hunger - item.Satiety, thirst - item.Hydration, nature + item.NatureCall);
            if (stats != null)
            {
                if (item.Heal > 0f) stats.Heal(item.Heal);
                if (item.Stamina > 0f) stats.AddStamina(item.Stamina);
            }
            if (item.RegenSeconds > 0f && item.RegenMultiplier > 1f) AddRegenBoost(item.RegenMultiplier, item.RegenSeconds);
        }

        /// <summary>Quenches thirst at a cooler or a tap. A drink always sends the call of nature up a little.</summary>
        public void Drink(float hydration, float natureCall)
        {
            Set(hunger, thirst - hydration, nature + natureCall);
        }

        /// <summary>The toilet: the call of nature is gone.</summary>
        public void Relieve()
        {
            accidentTimer = Mathf.Min(accidentTimer, accidentSmellSeconds * 0.25f);
            Set(hunger, thirst, 0f);
        }

        /// <summary>Faster healing for a while. A stronger or a longer effect replaces a weaker one.</summary>
        public void AddRegenBoost(float multiplier, float seconds)
        {
            if (multiplier <= 1f || seconds <= 0f) return;
            boostMultiplier = boostTimer > 0f ? Mathf.Max(boostMultiplier, multiplier) : multiplier;
            boostTimer = Mathf.Max(boostTimer, seconds);
            Changed?.Invoke();
        }

        /// <summary>Sleep, and a new life: everything is as new.</summary>
        public void RestoreAll()
        {
            boostTimer = 0f;
            accidentTimer = 0f;
            Set(0f, 0f, 0f);
        }

        /// <summary>Puts the saved values back (loading a game).</summary>
        public void SetValues(float newHunger, float newThirst, float newNature) => Set(newHunger, newThirst, newNature);

        private void Set(float newHunger, float newThirst, float newNature)
        {
            newHunger = Mathf.Clamp01(newHunger);
            newThirst = Mathf.Clamp01(newThirst);
            newNature = Mathf.Clamp01(newNature);
            hunger = newHunger;
            thirst = newThirst;
            nature = newNature;

            // The values creep a little every frame; listeners hear about it when it adds up.
            if (Mathf.Abs(hunger - reported.x) < ReportStep && Mathf.Abs(thirst - reported.y) < ReportStep
                && Mathf.Abs(nature - reported.z) < ReportStep) return;
            reported = new Vector3(hunger, thirst, nature);
            Changed?.Invoke();
        }
    }
}
