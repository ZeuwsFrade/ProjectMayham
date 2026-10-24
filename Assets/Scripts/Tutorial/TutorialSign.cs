using ProjectMayham.Core;
using ProjectMayham.Interaction;
using UnityEngine;

namespace ProjectMayham.Tutorial
{
    /// <summary>A note on the wall or the floor of the tutorial: E reads it (<see cref="NoteWindow"/>).</summary>
    public class TutorialSign : MonoBehaviour, IInteractable
    {
        [SerializeField] private string title = "Записка";
        [SerializeField, TextArea(3, 10)] private string text;

        public string Prompt => $"Прочитать: {title}";
        public Vector2 Position => transform.position;

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        public bool CanInteract(GameObject actor) => !ModalState.IsOpen;

        public void Interact(GameObject actor) => NoteWindow.Open(title, text);
    }
}
