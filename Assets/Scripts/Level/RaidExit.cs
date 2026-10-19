using ProjectMayham.Core;
using ProjectMayham.Interaction;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// The way out of a raid: the player walks up to it and presses E, and is asked whether to go back to the shelter
    /// with what they carry. One a raid, put on corridor floor far from the start by <see cref="RaidGenerator"/>.
    /// </summary>
    public class RaidExit : MonoBehaviour, IInteractable
    {
        /// <summary>The exit of the raid that is on, or null: a raid without one lets Esc leave from anywhere.</summary>
        public static RaidExit Active { get; private set; }

        public string Prompt => "Выйти из рейда";
        public Vector2 Position => transform.position;

        private void OnEnable()
        {
            Active = this;
            Interactables.Register(this);
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
            Interactables.Unregister(this);
        }

        public bool CanInteract(GameObject actor) => !ModalState.IsOpen;

        public void Interact(GameObject actor)
        {
            var controller = FindFirstObjectByType<RaidController>();
            if (controller != null) controller.AskToLeave();
        }
    }
}
