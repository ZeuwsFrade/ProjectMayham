using ProjectMayham.Core;
using ProjectMayham.Interaction;
using UnityEngine;

namespace ProjectMayham.Shelter
{
    /// <summary>Base of the objects of the shelter that the player uses with E (stash, bed, trader, module slots).</summary>
    public abstract class ShelterInteractable : MonoBehaviour, IInteractable
    {
        public abstract string Prompt { get; }
        public Vector2 Position => transform.position;

        protected virtual void OnEnable() => Interactables.Register(this);
        protected virtual void OnDisable() => Interactables.Unregister(this);

        public virtual bool CanInteract(GameObject actor) => !ModalState.IsOpen;
        public abstract void Interact(GameObject actor);
    }
}
