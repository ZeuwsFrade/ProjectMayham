using ProjectMayham.Interaction;
using ProjectMayham.Player;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// A water cooler or a tap: hold E and thirst goes down by a fixed share. Drinking sends the call of nature up
    /// a little. The water never runs out.
    /// </summary>
    public class WaterPoint : MonoBehaviour, IHoldInteractable
    {
        [SerializeField] private string prompt = "Попить (удерживать)";
        [SerializeField, Min(0.5f)] private float seconds = 2f;
        [Tooltip("Thirst taken away by one drink, a share of the whole scale.")]
        [SerializeField, Range(0f, 1f)] private float hydration = 0.35f;
        [Tooltip("Call of nature added by one drink.")]
        [SerializeField, Range(0f, 1f)] private float natureCall = 0.08f;
        [Tooltip("Below this thirst there is no need to drink.")]
        [SerializeField, Range(0f, 0.5f)] private float minThirst = 0.03f;

        /// <summary>Someone has drunk at a cooler or a tap.</summary>
        public static event System.Action<GameObject> Used;

        public string Prompt => prompt;
        public float HoldSeconds => seconds;
        public string HoldLabel => "Пью";
        public Vector2 Position => transform.position;

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        public bool CanInteract(GameObject actor)
        {
            var stats = actor.GetComponent<PlayerStats>();
            return stats != null && stats.Needs != null && stats.Needs.Thirst > minThirst;
        }

        public void Interact(GameObject actor)
        {
            var stats = actor.GetComponent<PlayerStats>();
            if (stats == null || stats.Needs == null) return;
            stats.Needs.Drink(hydration, natureCall);
            Used?.Invoke(actor);
        }
    }
}
