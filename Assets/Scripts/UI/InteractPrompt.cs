using ProjectMayham.Interaction;
using ProjectMayham.Items;
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
        [SerializeField] private string tooHeavyMessage = "Слишком тяжело";

        private string shown;

        private void Update()
        {
            string text = string.Empty;
            var current = interactor != null ? interactor.Current : null;
            if (current != null) text = interactor.CurrentUsable ? $"[E] {current.Prompt}" : BlockedMessage(current);

            if (text == shown) return;
            shown = text;
            label.text = text;
        }

        private string BlockedMessage(IInteractable current)
        {
            if (current is ItemPickup pickup && interactor.TryGetComponent<Inventory>(out var inventory)
                && inventory.HasRoomFor(pickup.Item) && !inventory.FitsByWeight(pickup.Item))
            {
                return tooHeavyMessage;
            }
            return fullMessage;
        }
    }
}
