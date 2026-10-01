using ProjectMayham.Interaction;
using UnityEngine;

namespace ProjectMayham.Items
{
    /// <summary>An item lying in the world. E puts it into the inventory of whoever interacts.</summary>
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemDefinition item;
        [Min(1)]
        [SerializeField] private int count = 1;

        public ItemDefinition Item => item;
        public int Count => count;
        public Vector2 Position => transform.position;

        public string Prompt
        {
            get
            {
                if (item == null) return string.Empty;
                return count > 1 ? $"Подобрать: {item.DisplayName} ×{count}" : $"Подобрать: {item.DisplayName}";
            }
        }

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        /// <summary>Sets what lies here; used when an item is dropped from the inventory.</summary>
        public void Set(ItemDefinition newItem, int newCount)
        {
            item = newItem;
            count = Mathf.Max(1, newCount);
        }

        public bool CanInteract(GameObject actor)
        {
            return item != null && actor.TryGetComponent<Inventory>(out var inventory) && inventory.CanAdd(item);
        }

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;

            int left = actor.GetComponent<Inventory>().Add(item, count);
            if (left <= 0) Destroy(gameObject);
            else count = left;
        }
    }
}
