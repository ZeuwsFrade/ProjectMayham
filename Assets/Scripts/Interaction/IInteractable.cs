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

    /// <summary>An interactable that needs the Interact key to be held for a while, like taking a barricade apart.</summary>
    public interface IHoldInteractable : IInteractable
    {
        /// <summary>Seconds the key has to be held before <see cref="IInteractable.Interact"/> is called.</summary>
        float HoldSeconds { get; }
        /// <summary>Text shown while the key is held, for example "Разбираю баррикаду".</summary>
        string HoldLabel { get; }
    }

    /// <summary>Other ways to deal with the object besides the Interact key; shown after the prompt.</summary>
    public interface IPromptHints
    {
        string Hints { get; }
    }
}
