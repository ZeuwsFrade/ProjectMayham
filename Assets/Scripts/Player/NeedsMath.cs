using UnityEngine;

namespace ProjectMayham.Player
{
    /// <summary>
    /// The formulas of the needs, kept apart from the components so that they can be checked without a scene.
    /// Hunger, thirst and the call of nature are shares of their scale: 0 = fine, 1 = the worst.
    /// </summary>
    public static class NeedsMath
    {
        /// <summary>The largest stamina the player can use: full hunger takes <paramref name="maxLoss"/> of it away.</summary>
        public static float StaminaCap(float maxStamina, float hunger, float maxLoss = 0.5f) =>
            maxStamina * (1f - Mathf.Clamp01(maxLoss) * Mathf.Clamp01(hunger));

        /// <summary>
        /// How much of its speed the stamina regeneration keeps. Every missing percent of health takes a percent
        /// of it away (a quarter of the health left: four times slower), and thirst doubles that at its worst
        /// (every missing percent takes two). It never drops below <paramref name="floor"/>, so that the player
        /// cannot be stuck out of breath for good.
        /// </summary>
        public static float StaminaRegenFactor(float healthFraction, float thirst, float floor = 0.1f)
        {
            float missing = 1f - Mathf.Clamp01(healthFraction);
            float factor = 1f - missing * (1f + Mathf.Clamp01(thirst));
            return Mathf.Clamp(factor, Mathf.Clamp01(floor), 1f);
        }

        /// <summary>Thirst slows the healing down: at its worst the health comes back half as fast.</summary>
        public static float HealthRegenThirstFactor(float thirst) => 1f - 0.5f * Mathf.Clamp01(thirst);

        /// <summary>Stamina spent for a point of health. Thirst doubles the price at its worst.</summary>
        public static float StaminaPerHealth(float basePrice, float thirst) => basePrice * (1f + Mathf.Clamp01(thirst));

        /// <summary>
        /// Hunger and thirst grow faster under a heavy load: 1 at or under the comfortable weight (encumbrance 0),
        /// <paramref name="atMaxWeight"/> at the max weight (encumbrance 1).
        /// </summary>
        public static float WeightNeedsMultiplier(float encumbrance, float atMaxWeight = 2.5f) =>
            Mathf.Lerp(1f, Mathf.Max(1f, atMaxWeight), Mathf.Clamp01(encumbrance));

        /// <summary>Walking speed under a full call of nature: 1 up to the threshold, then down to <paramref name="atFull"/>.</summary>
        public static float NatureSpeed(float nature, float threshold = 0.6f, float atFull = 0.7f)
        {
            if (nature <= threshold) return 1f;
            return Mathf.Lerp(1f, atFull, Mathf.InverseLerp(threshold, 1f, nature));
        }

        /// <summary>How far the smell of the player carries: nothing up to the threshold, <paramref name="atFull"/> at 100%.</summary>
        public static float SmellRadius(float nature, float threshold = 0.6f, float atFull = 7f)
        {
            if (nature <= threshold) return 0f;
            return Mathf.Lerp(atFull * 0.3f, atFull, Mathf.InverseLerp(threshold, 1f, nature));
        }
    }
}
