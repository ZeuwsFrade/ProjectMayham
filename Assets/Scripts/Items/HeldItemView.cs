using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.Items
{
    /// <summary>
    /// Shows what the player holds next to the player (it turns with them): the right hand's thing at the item's
    /// own place, the left hand's mirrored to the other side, a two-handed thing at its own place. Q makes the other
    /// hand the active one; H puts away what both hands hold: back into the inventory, or on the floor when the
    /// inventory is full. Things the hands make room for go on the floor the same way.
    /// </summary>
    [RequireComponent(typeof(GridInventory))]
    public class HeldItemView : MonoBehaviour
    {
        [SerializeField] private Key putAwayKey = Key.H;
        [SerializeField] private Key switchKey = Key.Q;
        [Tooltip("Drawn this many sorting orders above the player's sprite.")]
        [SerializeField] private int orderAbovePlayer = 1;
        [Tooltip("Seconds the note about the active hand stays on screen after Q.")]
        [SerializeField, Min(0f)] private float noteSeconds = 1.5f;

        private GridInventory inventory;
        private SpriteRenderer rightView;
        private SpriteRenderer leftView;
        private SpriteRenderer posedView;

        /// <summary>
        /// Puts the thing in use (the active hand's, or the two-handed one) at a place in the world instead of in the
        /// hands: a weapon being swung. Stays there until <see cref="ClearPose"/>.
        /// </summary>
        /// <param name="position">World position of the middle of the sprite.</param>
        /// <param name="angle">World rotation of the sprite around Z, in degrees, as the item's own hand angle would be.</param>
        public void SetPose(Vector2 position, float angle)
        {
            var view = ActiveView;
            if (view == null || !view.enabled) return;

            posedView = view;
            // The swing is worked out for a thing held the right way round.
            view.flipX = false;
            view.transform.SetPositionAndRotation(new Vector3(position.x, position.y, view.transform.position.z),
                Quaternion.Euler(0f, 0f, angle));
        }

        /// <summary>The thing goes back to the hands.</summary>
        public void ClearPose()
        {
            if (posedView == null) return;

            posedView = null;
            Refresh();
        }

        private SpriteRenderer ActiveView =>
            inventory.HoldsTwoHanded || inventory.ActiveHand == Hand.Right ? rightView : leftView;

        private void Awake()
        {
            inventory = GetComponent<GridInventory>();
            rightView = MakeView("HeldItem");
            leftView = MakeView("HeldItemLeft");
            inventory.DropOutside = DropOnFloor;
        }

        private SpriteRenderer MakeView(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var view = go.AddComponent<SpriteRenderer>();
            if (TryGetComponent<SpriteRenderer>(out var body))
            {
                view.sortingLayerID = body.sortingLayerID;
                view.sortingOrder = body.sortingOrder + orderAbovePlayer;
            }
            return view;
        }

        private bool DropOnFloor(ItemDefinition item) =>
            TryGetComponent<Interaction.PlayerInteractor>(out var interactor) && interactor.DropToWorld(item, 1);

        private void OnEnable()
        {
            inventory.HeldChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => inventory.HeldChanged -= Refresh;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[switchKey].wasPressedThisFrame)
            {
                inventory.SwitchHand();
                var item = inventory.HeldItem;
                string hand = inventory.ActiveHand == Hand.Right ? "Правая рука" : "Левая рука";
                UI.ScreenNote.Show(item != null ? $"{hand}: {item.DisplayName}" : $"{hand}: пусто", noteSeconds);
            }
            if (keyboard[putAwayKey].wasPressedThisFrame && !inventory.HandsEmpty) inventory.PutAwayHands();
        }

        private void Refresh()
        {
            posedView = null;
            Show(rightView, inventory.InHand(Hand.Right), false);
            Show(leftView, inventory.HoldsTwoHanded ? null : inventory.InHand(Hand.Left), true);
        }

        private void Show(SpriteRenderer view, ItemDefinition item, bool mirrored)
        {
            view.enabled = item != null && item.HeldSprite != null;
            if (!view.enabled) return;

            view.sprite = item.HeldSprite;
            view.flipX = mirrored;
            var offset = item.HeldOffset;
            view.transform.localPosition = mirrored ? new Vector2(-offset.x, offset.y) : offset;
            view.transform.localRotation = Quaternion.Euler(0f, 0f, mirrored ? -item.HeldAngle : item.HeldAngle);

            // HeldScale is a size in the world, so the player's own scale is divided out.
            Vector3 parent = transform.lossyScale;
            float size = item.HeldScale;
            view.transform.localScale = new Vector3(size / Mathf.Max(0.0001f, Mathf.Abs(parent.x)), size / Mathf.Max(0.0001f, Mathf.Abs(parent.y)), 1f);
        }
    }
}
