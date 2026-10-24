using System;
using ProjectMayham.Core;
using ProjectMayham.Interaction;
using ProjectMayham.Items;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// A door in a doorway. Closed it stops walking, sight and sound like the wall around it; open it is out of the
    /// way for good. It can be locked in two ways: with a key (an item; E with the key in the inventory unlocks and
    /// opens it) or by the game (<see cref="Unlock"/> from a script, say when the goals of a room are done).
    /// An unlocked door opens on E.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Door : MonoBehaviour, IInteractable, IPromptHints
    {
        [SerializeField] private string title = "Дверь";
        [Tooltip("The item that unlocks the door. Empty: no key fits it.")]
        [SerializeField] private ItemDefinition key;
        [Tooltip("The key is used up (left in the lock) when the door is unlocked.")]
        [SerializeField] private bool keyIsUsedUp = true;
        [Tooltip("Locked at the start. Without a key only a script can unlock it.")]
        [SerializeField] private bool locked = true;
        [Tooltip("Said when the door is locked and the player has no way to open it.")]
        [SerializeField] private string lockedHint = "Заперто";

        [Header("Picture")]
        [Tooltip("Hidden when the door opens. Empty: the sprite on this object.")]
        [SerializeField] private SpriteRenderer picture;
        [Tooltip("Shown instead of the closed picture when the door is open. Empty: nothing is left to see.")]
        [SerializeField] private Sprite openSprite;

        private BoxCollider2D box;

        /// <summary>Raised once, when the door opens.</summary>
        public event Action<Door> Opened;

        public bool IsOpen { get; private set; }
        public bool IsLocked => locked;
        public ItemDefinition Key => key;

        public string Prompt
        {
            get
            {
                if (!locked) return $"Открыть: {title}";
                return key != null ? $"Отпереть: {title}" : title;
            }
        }

        public string Hints => locked && !opensForLastActor ? (key != null ? $"Нужен ключ: {key.DisplayName}" : lockedHint) : string.Empty;

        public Vector2 Position => transform.position;

        // Whether the player who looked last could open it: the hint says what is missing.
        private bool opensForLastActor = true;

        private void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            if (picture == null) picture = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            if (!IsOpen) Interactables.Register(this);
        }

        private void OnDisable() => Interactables.Unregister(this);

        public bool CanInteract(GameObject actor)
        {
            opensForLastActor = !IsOpen && !ModalState.IsOpen && (!locked || HasKey(actor));
            return opensForLastActor;
        }

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;

            if (locked)
            {
                if (keyIsUsedUp && actor.TryGetComponent<GridInventory>(out var inventory)) inventory.RemoveItems(key, 1);
                locked = false;
            }
            Open();
        }

        /// <summary>Takes the lock off; the door stays closed until somebody opens it.</summary>
        public void Unlock() => locked = false;

        /// <summary>Opens the door whatever its lock: it no longer stops anything.</summary>
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            locked = false;
            box.enabled = false;
            if (picture != null)
            {
                if (openSprite != null) picture.sprite = openSprite;
                else picture.enabled = false;
            }
            Interactables.Unregister(this);
            Opened?.Invoke(this);
        }

        private bool HasKey(GameObject actor) =>
            key != null && actor.TryGetComponent<GridInventory>(out var inventory) && inventory.CountOf(key) > 0;
    }
}
