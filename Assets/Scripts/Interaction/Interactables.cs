using System.Collections.Generic;

namespace ProjectMayham.Interaction
{
    /// <summary>Every enabled <see cref="IInteractable"/>. They register themselves, so no physics colliders are needed.</summary>
    public static class Interactables
    {
        private static readonly List<IInteractable> all = new List<IInteractable>();

        public static IReadOnlyList<IInteractable> All => all;

        public static void Register(IInteractable interactable)
        {
            if (!all.Contains(interactable)) all.Add(interactable);
        }

        public static void Unregister(IInteractable interactable) => all.Remove(interactable);
    }
}
