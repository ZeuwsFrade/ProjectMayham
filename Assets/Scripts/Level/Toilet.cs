using ProjectMayham.Interaction;
using ProjectMayham.Player;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>A toilet: hold E for a few seconds and the call of nature is gone. Works whenever there is anything to relieve.</summary>
    public class Toilet : MonoBehaviour, IHoldInteractable
    {
        [SerializeField, Min(0.5f)] private float seconds = 4f;
        [Tooltip("Below this share of the scale there is nothing to do here.")]
        [SerializeField, Range(0f, 0.5f)] private float minNeed = 0.05f;

        /// <summary>Someone has used a toilet.</summary>
        public static event System.Action<GameObject> Used;

        public string Prompt => "Справить нужду (удерживать)";
        public float HoldSeconds => seconds;
        public string HoldLabel => "Справляю нужду";
        public Vector2 Position => transform.position;

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        public bool CanInteract(GameObject actor)
        {
            var stats = actor.GetComponent<PlayerStats>();
            return stats != null && stats.Needs != null && stats.Needs.NatureCall > minNeed;
        }

        public void Interact(GameObject actor)
        {
            var stats = actor.GetComponent<PlayerStats>();
            if (stats == null || stats.Needs == null) return;
            stats.Needs.Relieve();
            Used?.Invoke(actor);
        }
    }
}
