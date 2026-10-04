using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.Items
{
    /// <summary>
    /// Shows the item in the player's hands next to the player (it turns with them) and puts it away on H:
    /// back into the inventory, or on the floor when the inventory is full.
    /// </summary>
    [RequireComponent(typeof(GridInventory))]
    public class HeldItemView : MonoBehaviour
    {
        [SerializeField] private Key putAwayKey = Key.H;
        [Tooltip("Drawn this many sorting orders above the player's sprite.")]
        [SerializeField] private int orderAbovePlayer = 1;

        private GridInventory inventory;
        private SpriteRenderer view;

        private void Awake()
        {
            inventory = GetComponent<GridInventory>();

            var go = new GameObject("HeldItem");
            go.transform.SetParent(transform, false);
            view = go.AddComponent<SpriteRenderer>();
            if (TryGetComponent<SpriteRenderer>(out var body))
            {
                view.sortingLayerID = body.sortingLayerID;
                view.sortingOrder = body.sortingOrder + orderAbovePlayer;
            }
        }

        private void OnEnable()
        {
            inventory.HeldChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => inventory.HeldChanged -= Refresh;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[putAwayKey].wasPressedThisFrame || inventory.HeldItem == null) return;

            if (inventory.Unhold()) return;
            // No room in the inventory: the item goes on the floor instead.
            if (inventory.TakeHeld(out var item) && TryGetComponent<Interaction.PlayerInteractor>(out var interactor)
                && !interactor.DropToWorld(item, 1))
            {
                inventory.Add(item, 1);
            }
        }

        private void Refresh()
        {
            var item = inventory.HeldItem;
            view.enabled = item != null && item.HeldSprite != null;
            if (!view.enabled) return;

            view.sprite = item.HeldSprite;
            view.transform.localPosition = item.HeldOffset;
            view.transform.localRotation = Quaternion.Euler(0f, 0f, item.HeldAngle);

            // HeldScale is a size in the world, so the player's own scale is divided out.
            Vector3 parent = transform.lossyScale;
            float size = item.HeldScale;
            view.transform.localScale = new Vector3(size / Mathf.Max(0.0001f, Mathf.Abs(parent.x)), size / Mathf.Max(0.0001f, Mathf.Abs(parent.y)), 1f);
        }
    }
}
