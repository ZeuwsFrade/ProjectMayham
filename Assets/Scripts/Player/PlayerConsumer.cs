using ProjectMayham.Core;
using ProjectMayham.Interaction;
using ProjectMayham.Items;
using ProjectMayham.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.Player
{
    /// <summary>
    /// Eating, drinking and bandaging. A consumable in the hands is used by holding the left mouse button for its
    /// use time; from the backpack (F over it in the inventory window) it takes a fixed, longer time. Letting go,
    /// a blow, a change of what is in the hands or death interrupts it and nothing is used up.
    /// Added to the player by <see cref="PlayerNeeds"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerStats), typeof(GridInventory))]
    public class PlayerConsumer : MonoBehaviour
    {
        [Tooltip("Seconds it takes to use something straight from the backpack, whatever it is.")]
        [SerializeField, Min(0.5f)] private float inventoryUseSeconds = 3f;
        [Tooltip("Walking speed while something in the hands is being used.")]
        [SerializeField, Range(0.1f, 1f)] private float speedWhileUsing = 0.6f;
        [Tooltip("Seconds a message (nothing to heal) stays on screen.")]
        [SerializeField, Min(0.5f)] private float messageSeconds = 1.6f;

        private PlayerStats stats;
        private PlayerController controller;
        private GridInventory inventory;
        private PlayerClimber climber;
        private InventoryWindow inventoryWindow;

        private ConsumableDefinition item;
        private bool fromHands;
        private float duration;
        private float elapsed;
        private bool slowed;
        private float messageTimer;
        private bool leftWasHeld;

        /// <summary>An item has been eaten, drunk or applied.</summary>
        public event System.Action<ConsumableDefinition> Used;
        /// <summary>True while an item is being used.</summary>
        public bool IsUsing => item != null;
        /// <summary>0..1 while an item is being used.</summary>
        public float Progress => item != null ? Mathf.Clamp01(elapsed / duration) : 0f;
        public ConsumableDefinition Item => item;
        /// <summary>"Съесть: Батончик" while an item is being used.</summary>
        public string Label => item != null ? $"{item.Verb}: {item.DisplayName}" : string.Empty;
        /// <summary>Why something could not be used. Empty when there is nothing to tell.</summary>
        public string Message { get; private set; } = string.Empty;
        public float InventoryUseSeconds => inventoryUseSeconds;

        // Looked up each time: this component can be created while the stats are still setting up their own.
        private PlayerNeeds Needs => stats.Needs;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            controller = GetComponent<PlayerController>();
            inventory = GetComponent<GridInventory>();
            climber = GetComponent<PlayerClimber>();
        }

        private void OnEnable()
        {
            stats.Damaged += OnDamaged;
            inventory.HeldChanged += OnHeldChanged;
        }

        private void OnDisable()
        {
            stats.Damaged -= OnDamaged;
            inventory.HeldChanged -= OnHeldChanged;
            Cancel();
        }

        private void Update()
        {
            if (messageTimer > 0f)
            {
                messageTimer -= Time.unscaledDeltaTime;
                if (messageTimer <= 0f) Message = string.Empty;
            }

            if (inventoryWindow == null) inventoryWindow = FindFirstObjectByType<InventoryWindow>(FindObjectsInactive.Include);

            // A press is also seen when the button is down now and was not at the last look (see MeleeCombat).
            var mouse = Mouse.current;
            bool held = mouse != null && mouse.leftButton.isPressed;
            bool pressed = mouse != null && (mouse.leftButton.wasPressedThisFrame || (held && !leftWasHeld));
            leftWasHeld = held;

            if (Blocked()) { Cancel(); return; }

            if (item == null)
            {
                if (pressed && !WindowOpen() && inventory.HeldItem is ConsumableDefinition inHands)
                    Begin(inHands, true, inHands.UseSeconds);
                return;
            }

            if (fromHands && (!held || WindowOpen())) { Cancel(); return; }

            elapsed += Time.deltaTime;
            if (elapsed >= duration) Finish();
        }

        /// <summary>Starts using an item from the backpack. False (with a message) when it cannot be used now.</summary>
        public bool TryUseFromInventory(ConsumableDefinition consumable)
        {
            if (consumable == null || Blocked() || item != null) return false;
            if (inventory.CountOf(consumable) <= 0) return false;
            return Begin(consumable, false, inventoryUseSeconds);
        }

        /// <summary>Whether using the item would change anything: eating on a full stomach is a waste.</summary>
        public bool IsUseful(ConsumableDefinition consumable)
        {
            if (consumable == null) return false;
            if (consumable.Satiety > 0f && Needs.Hunger > 0.001f) return true;
            if (consumable.Hydration > 0f && Needs.Thirst > 0.001f) return true;
            if ((consumable.Heal > 0f || consumable.RegenSeconds > 0f) && stats.Health < stats.MaxHealth - 0.01f) return true;
            if (consumable.Stamina > 0f && stats.Stamina < stats.StaminaCap - 0.01f) return true;
            return false;
        }

        private bool Begin(ConsumableDefinition consumable, bool hands, float seconds)
        {
            if (!IsUseful(consumable))
            {
                Say("Сейчас это не нужно");
                return false;
            }

            item = consumable;
            fromHands = hands;
            duration = Mathf.Max(0.1f, seconds);
            elapsed = 0f;
            if (hands) Slow(true);
            return true;
        }

        private void Finish()
        {
            var used = item;
            bool hands = fromHands;
            Cancel();

            bool taken = hands
                ? inventory.HeldItem == used && inventory.TakeHeld(out _)
                : inventory.RemoveItems(used, 1) > 0;
            if (!taken) return;

            Needs.Apply(used);
            Used?.Invoke(used);
            if (hands) TakeNextInHands(used);
        }

        // The next one of the same kind is taken in the hands, so that the player can go on eating.
        private void TakeNextInHands(ItemDefinition used)
        {
            foreach (var stack in inventory.Stacks)
            {
                if (stack.Item != used || stack.Sealed) continue;
                inventory.Hold(stack);
                return;
            }
        }

        private void Cancel()
        {
            if (item == null) return;
            item = null;
            elapsed = 0f;
            Slow(false);
        }

        private void Slow(bool on)
        {
            if (on == slowed || controller == null) return;
            slowed = on;
            controller.SpeedMultiplier = on ? speedWhileUsing : 1f;
            controller.SprintBlocked = on;
        }

        private bool Blocked()
        {
            return stats.IsDead || ModalState.IsOpen || SceneLoader.IsLoading || (climber != null && climber.IsClimbing);
        }

        private bool WindowOpen() => inventoryWindow != null && inventoryWindow.IsOpen;

        private void OnDamaged(float amount) => Cancel();

        private void OnHeldChanged()
        {
            // Taking something else in the hands stops what the hands were doing; using from the backpack goes on.
            if (item != null && fromHands && inventory.HeldItem != item) Cancel();
        }

        private void Say(string text)
        {
            Message = text;
            messageTimer = messageSeconds;
        }
    }
}
