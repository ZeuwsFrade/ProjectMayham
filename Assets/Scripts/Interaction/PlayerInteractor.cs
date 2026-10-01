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
            var slot = inventory[inventory.Selected];
            if (slot.IsEmpty || slot.item.WorldPrefab == null) return;
            if (!inventory.TakeSelected(out var item, out int count)) return;

            Vector2 origin = transform.position;
            Vector2 facing = player != null ? player.AimPoint - origin : (Vector2)transform.up;
            facing = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector2.up;

            Vector2 spot = origin + facing * dropDistance;
            if (Physics2D.OverlapCircle(spot, 0.2f, blockMask) != null) spot = origin;

            var dropped = Instantiate(item.WorldPrefab, spot, Quaternion.identity);
            if (dropped.TryGetComponent<ItemPickup>(out var pickup)) pickup.Set(item, count);
        }
    }
}
