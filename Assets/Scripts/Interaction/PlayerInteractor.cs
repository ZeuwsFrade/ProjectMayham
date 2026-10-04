using System.Collections.Generic;
using ProjectMayham.Items;
using ProjectMayham.Player;
using ProjectMayham.Vision;
using ProjectMayham.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.Interaction
{
    /// <summary>
    /// Finds the nearest interactable the player can see, uses it on E and drops the selected item on G.
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

        private Inventory inventory;
        private PlayerController player;
        private InputAction interactAction;
        private InputAction dropAction;

        /// <summary>The object the prompt is about, or null.</summary>
        public IInteractable Current { get; private set; }
        public bool CurrentUsable { get; private set; }

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
        }

        private void Update()
        {
            FindCurrent();

            if (interactAction.WasPressedThisFrame() && Current != null && CurrentUsable) Current.Interact(gameObject);
            if (dropAction.WasPressedThisFrame()) DropSelected();
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
