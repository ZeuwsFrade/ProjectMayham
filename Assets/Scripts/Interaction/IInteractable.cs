using UnityEngine;

namespace ProjectMayham.Interaction
{
    /// <summary>Anything the player can use with the Interact key (E) when standing close to it.</summary>
    public interface IInteractable
    {
        /// <summary>Short text for the prompt, for example "Подобрать: Книги".</summary>
        string Prompt { get; }
        Vector2 Position { get; }
        /// <summary>False when the object is in range but cannot be used right now (the prompt still shows).</summary>
        bool CanInteract(GameObject actor);
        void Interact(GameObject actor);
    }
}
