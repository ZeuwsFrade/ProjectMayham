using ProjectMayham.Interaction;
using TMPro;
using UnityEngine;

namespace ProjectMayham.UI
{
    /// <summary>Shows "[E] ..." above the hotbar for the object the player is standing next to.</summary>
    public class InteractPrompt : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string fullMessage = "Нет места в инвентаре";

        private string shown;

        private void Update()
        {
            string text = string.Empty;
            var current = interactor != null ? interactor.Current : null;
            if (current != null) text = interactor.CurrentUsable ? $"[E] {current.Prompt}" : fullMessage;

            if (text == shown) return;
            shown = text;
            label.text = text;
        }
    }
}
