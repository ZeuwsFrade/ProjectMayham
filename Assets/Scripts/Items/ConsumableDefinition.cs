using UnityEngine;

namespace ProjectMayham.Items
{
    /// <summary>
    /// Food, drink and medicine: an item that is used up. Held in the hands it is used with the left mouse button;
    /// from the backpack with F (that takes a fixed, longer time). What it does is the
    /// list of numbers below: shares of a scale (0..1) for hunger, thirst and the call of nature, points for health
    /// and stamina.
    /// </summary>
    [CreateAssetMenu(menuName = "Mayham/Consumable", fileName = "NewConsumable")]
    public class ConsumableDefinition : ItemDefinition
    {
        [Header("Use")]
        [Tooltip("Verb for the prompt: Съесть, Выпить, Применить.")]
        [SerializeField] private string verb = "Съесть";
        [Tooltip("Seconds the left mouse button is held when the item is used from the hands.")]
        [SerializeField, Min(0.1f)] private float useSeconds = 1.5f;

        [Header("Needs (share of the whole scale, 0..1)")]
        [Tooltip("Hunger taken away.")]
        [SerializeField, Range(0f, 1f)] private float satiety;
        [Tooltip("Thirst taken away.")]
        [SerializeField, Range(0f, 1f)] private float hydration;
        [Tooltip("Call of nature added (coffee adds a lot).")]
        [SerializeField, Range(0f, 1f)] private float natureCall;

        [Header("Body")]
        [Tooltip("Health restored at once, in points.")]
        [SerializeField, Min(0f)] private float heal;
        [Tooltip("Stamina restored at once, in points (coffee).")]
        [SerializeField, Min(0f)] private float stamina;
        [Tooltip("Health regeneration is multiplied by this for the time below (1 = no effect).")]
        [SerializeField, Min(1f)] private float regenMultiplier = 1f;
        [SerializeField, Min(0f)] private float regenSeconds;

        public string Verb => string.IsNullOrEmpty(verb) ? "Использовать" : verb;
        public float UseSeconds => Mathf.Max(0.1f, useSeconds);
        public float Satiety => satiety;
        public float Hydration => hydration;
        public float NatureCall => natureCall;
        public float Heal => heal;
        public float Stamina => stamina;
        public float RegenMultiplier => Mathf.Max(1f, regenMultiplier);
        public float RegenSeconds => Mathf.Max(0f, regenSeconds);
    }
}
