using ProjectMayham.Interaction;
using ProjectMayham.Items;
using TMPro;
using UnityEngine;

namespace ProjectMayham.UI
{
    /// <summary>
    /// Shows "[E] ..." above the hotbar for the object the player is standing next to, the progress of an action
    /// that needs the key to be held and the reason why a climb did not start.
    /// </summary>
    public class InteractPrompt : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string fullMessage = "Нет места в инвентаре";
        [SerializeField] private string tooHeavyMessage = "Слишком тяжело";

        private PlayerClimber climber;
        private string shown;

        private void Start()
        {
            if (interactor != null) climber = interactor.GetComponent<PlayerClimber>();
        }

        private void Update()
        {
            string text = BuildText();
            if (text == shown) return;
            shown = text;
            label.text = text;
        }

        private string BuildText()
        {
            if (interactor == null) return string.Empty;
            if (climber != null && climber.Message.Length > 0) return climber.Message;

            var hold = interactor.HoldTarget;
            if (hold != null) return $"{hold.HoldLabel}… {Mathf.RoundToInt(interactor.HoldProgress * 100f)}%";

            var current = interactor.Current;
            if (current == null) return string.Empty;

            string text = interactor.CurrentUsable ? $"[E] {current.Prompt}" : BlockedMessage(current);
            if (current is IPromptHints hints && !string.IsNullOrEmpty(hints.Hints))
                text = string.IsNullOrEmpty(text) ? hints.Hints : $"{text} · {hints.Hints}";
            return text;
        }

        private string BlockedMessage(IInteractable current)
        {
            if (!(current is ItemPickup pickup)) return string.Empty;

            if (interactor.TryGetComponent<Inventory>(out var inventory)
                && inventory.HasRoomFor(pickup.Item) && !inventory.FitsByWeight(pickup.Item))
            {
                return tooHeavyMessage;
            }
            return fullMessage;
        }
    }
}
