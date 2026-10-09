using System.Collections.Generic;
using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.Player;
using ProjectMayham.Vision;
using ProjectMayham.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.Interaction
{
    /// <summary>
    /// Finds the nearest interactable the player can see and reach, uses it on E (some need the key to be held)
    /// and drops the selected item on G.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("How close an object has to be to be used, in world units.")]
        [SerializeField] private float interactRadius = 1.4f;
        [Tooltip("Where dropped items land, in front of the player.")]
        [SerializeField] private float dropDistance = 0.9f;
        [Tooltip("Layers that stop a dropped item from being placed inside a wall.")]
        [SerializeField] private LayerMask blockMask;
        [Tooltip("Layers nothing can be used through: walls and the glass of windows.")]
        [SerializeField] private LayerMask reachBlockMask;

        private Inventory inventory;
        private PlayerController player;
        private InputAction interactAction;
        private InputAction dropAction;
        private IHoldInteractable holdTarget;
        private float holdTimer;

        /// <summary>The object the prompt is about, or null.</summary>
        public IInteractable Current { get; private set; }
        public bool CurrentUsable { get; private set; }
        /// <summary>The object the Interact key is being held on, or null.</summary>
        public IHoldInteractable HoldTarget => holdTarget;
        /// <summary>0..1 while the Interact key is held on <see cref="HoldTarget"/>.</summary>
        public float HoldProgress => holdTarget != null ? Mathf.Clamp01(holdTimer / holdTarget.HoldSeconds) : 0f;

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
            player = GetComponent<PlayerController>();
            interactAction = inputActions.FindAction("Player/Interact", true);
            dropAction = inputActions.FindAction("Player/Drop", true);
        }

        private void OnEnable()
        {
            interactAction.Enable();
            dropAction.Enable();
        }

        private void OnDisable()
        {
            interactAction.Disable();
            dropAction.Disable();
            Current = null;
            CancelHold();
        }

        private void Update()
        {
            // Windows that pause the game own the keyboard: nothing in the world can be used meanwhile.
            if (ModalState.IsOpen)
            {
                Current = null;
                CurrentUsable = false;
                CancelHold();
                return;
            }

            FindCurrent();
            UpdateInteract();
            if (dropAction.WasPressedThisFrame()) DropSelected();
        }

        private void UpdateInteract()
        {
            bool usable = Current != null && CurrentUsable;
            if (usable && interactAction.WasPressedThisFrame())
            {
                var hold = Current as IHoldInteractable;
                if (hold == null || hold.HoldSeconds <= 0f)
                {
                    Current.Interact(gameObject);
                    return;
                }
                holdTarget = hold;
                holdTimer = 0f;
            }

            if (holdTarget == null) return;
            // Letting go of the key, walking away or losing sight of the object starts over.
            if (!usable || !interactAction.IsPressed() || !ReferenceEquals(Current, holdTarget))
            {
                CancelHold();
                return;
            }

            holdTimer += Time.deltaTime;
            if (holdTimer < holdTarget.HoldSeconds) return;

            var finished = holdTarget;
            CancelHold();
            finished.Interact(gameObject);
        }

        private void CancelHold()
        {
            holdTarget = null;
            holdTimer = 0f;
        }

        private void FindCurrent()
        {
            Vector2 origin = transform.position;
            var wrap = WrapWorld.Active;
            var cone = VisionCone.Active;

            IInteractable best = null;
            float bestDistance = interactRadius * interactRadius;
            foreach (var candidate in Interactables.All)
            {
                Vector2 delta = wrap != null ? wrap.Delta(origin, candidate.Position) : candidate.Position - origin;
                float distance = delta.sqrMagnitude;
                if (distance >= bestDistance) continue;
                // Things the player cannot see (in the dark or behind a wall) cannot be used.
                if (cone != null && !cone.IsPointVisible(origin + delta)) continue;
                // Seeing is not enough: a window shows what is behind it but nothing can be taken through the glass.
                if (reachBlockMask != 0 && Physics2D.Linecast(origin, origin + delta, reachBlockMask)) continue;

                best = candidate;
                bestDistance = distance;
            }

            Current = best;
            CurrentUsable = best != null && best.CanInteract(gameObject);
        }

        private void DropSelected()
        {
            // Everything selected goes at once; the list is filled first so that items that cannot lie on
            // the floor can be put back without being taken again.
            var taken = new List<(ItemDefinition item, int count)>();
            while (inventory.TakeSelected(out var item, out int count)) taken.Add((item, count));
            // With nothing selected G throws away what is in the hands.
            if (taken.Count == 0 && inventory is GridInventory grid && grid.TakeHeld(out var heldItem)) taken.Add((heldItem, 1));

            for (int i = 0; i < taken.Count; i++)
            {
                // A small ring keeps several dropped items from lying exactly on top of each other.
                Vector2 spread = taken.Count > 1
                    ? new Vector2(Mathf.Cos(i * Mathf.PI * 2f / taken.Count), Mathf.Sin(i * Mathf.PI * 2f / taken.Count)) * 0.35f
                    : Vector2.zero;
                if (!DropToWorld(taken[i].item, taken[i].count, spread)) inventory.Add(taken[i].item, taken[i].count);
            }
        }

        /// <summary>Puts an item in front of the player (or at their feet when the spot is inside a wall).</summary>
        public bool DropToWorld(ItemDefinition item, int count, Vector2 offset = default)
        {
            if (item == null || item.WorldPrefab == null || count <= 0) return false;

            Vector2 origin = transform.position;
            Vector2 facing = player != null ? player.AimPoint - origin : (Vector2)transform.up;
            facing = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector2.up;

            Vector2 spot = origin + facing * dropDistance + offset;
            if (Physics2D.OverlapCircle(spot, 0.2f, blockMask) != null) spot = origin;

            var dropped = Instantiate(item.WorldPrefab, spot, Quaternion.identity);
            if (dropped.TryGetComponent<ItemPickup>(out var pickup)) pickup.Set(item, count);
            return true;
        }
    }
}
