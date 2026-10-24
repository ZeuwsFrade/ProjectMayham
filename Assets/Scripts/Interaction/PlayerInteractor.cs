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
    /// Finds the nearest interactable the player can see and reach, uses it on E (some need the key to be held),
    /// drops the selected item on a tap of G and throws what is in the hands while G is held (aim with the mouse,
    /// let go to throw). A thrown thing lands with a noise that draws the enemies to it.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class PlayerInteractor : MonoBehaviour
    {
        private const int MarkerSegments = 20;

        private readonly RaycastHit2D[] reachHits = new RaycastHit2D[8];

        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("How close an object has to be to be used, in world units.")]
        [SerializeField] private float interactRadius = 1.4f;
        [Tooltip("Where dropped items land, in front of the player.")]
        [SerializeField] private float dropDistance = 0.9f;
        [Tooltip("Layers that stop a dropped item from being placed inside a wall.")]
        [SerializeField] private LayerMask blockMask;
        [Tooltip("Layers nothing can be used through: walls and the glass of windows.")]
        [SerializeField] private LayerMask reachBlockMask;

        [Header("Throw (hold G)")]
        [Tooltip("Seconds G has to be held before it aims a throw instead of dropping.")]
        [SerializeField, Min(0.05f)] private float throwHoldSeconds = 0.25f;
        [Tooltip("How far a thing can be thrown, in world units.")]
        [SerializeField, Min(1f)] private float throwRange = 7f;
        [Tooltip("Speed of a thing in flight, units per second.")]
        [SerializeField, Min(1f)] private float throwSpeed = 13f;

        private Inventory inventory;
        private float dropHeld = -1f;
        private bool dropWasPressed;
        private bool aiming;
        private LineRenderer aimLine;
        private LineRenderer aimRing;
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
        /// <summary>True while G is held long enough to aim a throw.</summary>
        public bool IsAimingThrow => aiming;

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
            CancelDrop();
        }

        private void OnDestroy()
        {
            if (aimLine != null) Destroy(aimLine.gameObject);
            if (aimRing != null) Destroy(aimRing.gameObject);
        }

        private void Update()
        {
            // Windows that pause the game own the keyboard: nothing in the world can be used meanwhile.
            if (ModalState.IsOpen)
            {
                Current = null;
                CurrentUsable = false;
                CancelHold();
                CancelDrop();
                return;
            }

            FindCurrent();
            UpdateInteract();
            UpdateDrop();
        }

        // A tap of G drops, as it always did (on letting go now, so a hold can become a throw).
        private void UpdateDrop()
        {
            // The edge is taken from the held state too: a press and release between two frames is not lost.
            bool pressed = dropAction.IsPressed();
            if (dropAction.WasPressedThisFrame() || (pressed && !dropWasPressed)) dropHeld = 0f;
            dropWasPressed = pressed;
            if (dropHeld < 0f) return;

            if (pressed)
            {
                dropHeld += Time.deltaTime;
                aiming = dropHeld >= throwHoldSeconds && CanThrow();
                ShowAim(aiming);
                return;
            }

            if (aiming) Throw();
            else DropSelected();
            CancelDrop();
        }

        private void CancelDrop()
        {
            dropHeld = -1f;
            aiming = false;
            ShowAim(false);
        }

        // ---- throwing ----

        // What a throw would take: the thing in the hands, or else one of the selected stack. People are carried, not thrown.
        private bool CanThrow()
        {
            if (inventory is GridInventory grid && grid.HeldItem != null) return Throwable(grid.HeldItem);
            var selected = (inventory as GridInventory)?.SelectedStack;
            return selected != null && Throwable(selected.Item);
        }

        private static bool Throwable(ItemDefinition item) => item != null && item.WorldPrefab != null && !item.IsPerson;

        private void Throw()
        {
            if (!TakeOneToThrow(out var item)) return;

            Vector2 origin = transform.position;
            Vector2 target = ThrowTarget(origin);
            var thrown = Instantiate(item.WorldPrefab, origin, Quaternion.identity);
            if (thrown.TryGetComponent<ItemPickup>(out var pickup)) pickup.Set(item, 1);
            ThrownItem.Launch(thrown, origin, target, throwSpeed, ThrownItem.NoiseOf(item));
        }

        private bool TakeOneToThrow(out ItemDefinition item)
        {
            item = null;
            if (!(inventory is GridInventory grid)) return false;
            if (grid.HeldItem != null) return Throwable(grid.HeldItem) && grid.TakeHeld(out item);

            if (!grid.TakeSelected(out item, out int count)) return false;
            if (!Throwable(item))
            {
                grid.Add(item, count);
                item = null;
                return false;
            }
            // One goes, the rest of the stack stays.
            if (count > 1 && grid.Add(item, count - 1) > 0) Debug.LogWarning($"PlayerInteractor: no room to put back {item.DisplayName}.", this);
            return true;
        }

        /// <summary>Where a throw lands: at the mouse, no farther than the range, and short of the first wall on the way.</summary>
        private Vector2 ThrowTarget(Vector2 origin)
        {
            Vector2 aim = player != null ? player.AimPoint : origin + (Vector2)transform.up * throwRange;
            Vector2 delta = Vector2.ClampMagnitude(aim - origin, throwRange);
            if (delta.sqrMagnitude < 0.01f) delta = (Vector2)transform.up * 0.5f;

            // Over the furniture, not through walls and glass.
            LayerMask walls = reachBlockMask != 0 ? reachBlockMask : blockMask;
            var hit = Physics2D.Linecast(origin, origin + delta, walls);
            if (hit.collider != null) return hit.point - delta.normalized * 0.3f;
            return origin + delta;
        }

        private void ShowAim(bool show)
        {
            if (!show)
            {
                if (aimLine != null) aimLine.enabled = false;
                if (aimRing != null) aimRing.enabled = false;
                return;
            }

            if (aimLine == null) aimLine = MakeLine("Throw Aim Line", 2, false);
            if (aimRing == null) aimRing = MakeLine("Throw Aim Ring", MarkerSegments, true);
            aimLine.enabled = true;
            aimRing.enabled = true;

            Vector2 origin = transform.position;
            Vector2 target = ThrowTarget(origin);
            aimLine.SetPosition(0, origin);
            aimLine.SetPosition(1, target);
            // The ring is as big as the noise the thing will make, scaled down: a hint of how loud it is.
            var item = (inventory as GridInventory)?.HeldItem ?? (inventory as GridInventory)?.SelectedStack?.Item;
            float radius = 0.25f + ThrownItem.NoiseOf(item) * 0.04f;
            for (int i = 0; i < MarkerSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / MarkerSegments;
                aimRing.SetPosition(i, target + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private static LineRenderer MakeLine(string name, int points, bool loop)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.positionCount = points;
            line.loop = loop;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.05f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = new Color(1f, 0.85f, 0.3f, 0.8f);
            line.sortingOrder = 50;
            return line;
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
                // The object's own colliders do not hide it: a closed door is a wall, and still has to be reached.
                var own = (candidate as Component)?.transform;
                // Things the player cannot see (in the dark or behind a wall) cannot be used.
                if (cone != null && !cone.IsPointVisible(origin + delta, own)) continue;
                // Seeing is not enough: a window shows what is behind it but nothing can be taken through the glass.
                if (reachBlockMask != 0 && ReachBlocked(origin, origin + delta, own)) continue;

                best = candidate;
                bestDistance = distance;
            }

            Current = best;
            CurrentUsable = best != null && best.CanInteract(gameObject);
        }

        /// <summary>True when a wall or a window stands between the two points; colliders under <paramref name="ignore"/> do not count.</summary>
        private bool ReachBlocked(Vector2 from, Vector2 to, Transform ignore)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(reachBlockMask);
            int count = Physics2D.Linecast(from, to, filter, reachHits);
            for (int i = 0; i < count; i++)
            {
                if (ignore == null || !reachHits[i].collider.transform.IsChildOf(ignore)) return true;
            }
            return false;
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
