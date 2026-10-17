using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Interaction;
using UnityEngine;

namespace ProjectMayham.Items
{
    /// <summary>
    /// What the player wears: something on the head, a backpack and something on the feet. The backpack sets the
    /// size of the inventory grid (without one there are only the pockets); the head and the feet do nothing yet.
    /// Worn things weigh like carried ones. When the grid gets smaller (a smaller backpack, or none), what no longer
    /// fits falls on the floor.
    /// </summary>
    [RequireComponent(typeof(GridInventory))]
    public class PlayerEquipment : MonoBehaviour
    {
        /// <summary>The slots in the order they are shown.</summary>
        public static readonly EquipSlot[] Slots = { EquipSlot.Head, EquipSlot.Back, EquipSlot.Feet };

        [Tooltip("Columns and rows of the inventory without a backpack: the pockets.")]
        [SerializeField] private Vector2Int pockets = new Vector2Int(4, 2);

        private readonly Dictionary<EquipSlot, ItemDefinition> worn = new Dictionary<EquipSlot, ItemDefinition>();
        private GridInventory inventory;

        /// <summary>Raised whenever something is put on or taken off.</summary>
        public event Action Changed;

        private GridInventory Inventory => inventory != null ? inventory : inventory = GetComponent<GridInventory>();

        public Vector2Int Pockets => new Vector2Int(Mathf.Max(1, pockets.x), Mathf.Max(1, pockets.y));

        /// <summary>What is worn in a slot, or null.</summary>
        public ItemDefinition this[EquipSlot slot] => worn.TryGetValue(slot, out var item) ? item : null;

        /// <summary>The size of the grid with this backpack on (null: no backpack, the pockets).</summary>
        public Vector2Int GridWith(ItemDefinition backpack) => backpack != null ? backpack.BackpackGrid : Pockets;

        public static string SlotTitle(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Head: return "Голова";
                case EquipSlot.Back: return "Рюкзак";
                case EquipSlot.Feet: return "Ноги";
                default: return string.Empty;
            }
        }

        // ---- what the player does ----

        /// <summary>
        /// Puts on a thing that lies in the grid. What was worn in its place goes into the grid; whatever fits
        /// nowhere falls on the floor. False when the thing is not worn at all.
        /// </summary>
        public bool WearFromGrid(GridInventory.Stack stack) => WearFrom(Inventory, stack);

        /// <summary>
        /// Puts on a thing that lies in some grid: the player's own, a container's, the stash. What was worn in its
        /// place takes the place of the new thing in that grid when it can, else goes into the player's grid, else
        /// falls on the floor, like whatever the grid no longer holds. False when the thing is not worn at all, or
        /// when coming from elsewhere it would make the player heavier than they can carry.
        /// </summary>
        public bool WearFrom(GridInventory source, GridInventory.Stack stack)
        {
            if (source == null || stack == null || stack.Sealed || !stack.Item.Wearable || !source.Stacks.Contains(stack)) return false;

            var item = stack.Item;
            var old = this[item.EquipSlot];
            bool own = source == Inventory;
            // From elsewhere it adds its weight, and what was worn leaves with its own.
            if (!own && Inventory.TotalWeight + item.Weight - (old != null ? old.Weight : 0f) > Inventory.MaxWeight + 0.0001f) return false;

            var origin = stack.Origin;
            bool rotated = stack.Rotated;
            bool lastPiece = stack.Count == 1;
            // Out of its grid first, so that it takes up no cells there.
            source.Reduce(stack, 1);
            var spilled = new List<(ItemDefinition item, int count)>();
            Put(item, spilled);
            if (old != null)
            {
                bool placed = !own && ((lastPiece && source.PlaceItem(old, 1, origin, rotated) == 1) || source.Add(old, 1) == 0);
                if (!placed) placed = Inventory.Add(old, 1) == 0;
                if (!placed) spilled.Insert(0, (old, 1));
            }
            Drop(spilled);
            return true;
        }

        /// <summary>Puts on a thing picked up from the floor when nothing is worn in its place. False otherwise.</summary>
        public bool WearIfFree(ItemDefinition item)
        {
            if (item == null || !item.Wearable || this[item.EquipSlot] != null) return false;

            var spilled = new List<(ItemDefinition item, int count)>();
            Put(item, spilled);
            Drop(spilled);
            return true;
        }

        /// <summary>
        /// Takes off what is worn in a slot and puts it into the grid: at <paramref name="origin"/> when given and
        /// free, else wherever it fits, else on the floor. False when nothing was worn there.
        /// </summary>
        public bool TakeOffToGrid(EquipSlot slot, Vector2Int? origin = null, bool rotated = false)
        {
            var spilled = new List<(ItemDefinition item, int count)>();
            var item = Remove(slot, spilled);
            if (item == null) return false;

            bool placed = origin.HasValue && Inventory.PlaceItem(item, 1, origin.Value, rotated) == 1;
            if (!placed) placed = Inventory.Add(item, 1) == 0;
            if (!placed) spilled.Insert(0, (item, 1));
            Drop(spilled);
            return true;
        }

        /// <summary>Takes off what is worn in a slot and throws it on the floor. False when nothing was worn there.</summary>
        public bool TakeOffToFloor(EquipSlot slot)
        {
            var spilled = new List<(ItemDefinition item, int count)>();
            var item = Remove(slot, spilled);
            if (item == null) return false;

            spilled.Insert(0, (item, 1));
            Drop(spilled);
            return true;
        }

        // ---- the rules, without dropping anything: what falls out is handed back ----

        /// <summary>
        /// Wears the thing in its slot and returns what was worn there before. A backpack resizes the grid; what no
        /// longer fits it is added to <paramref name="spilled"/>.
        /// </summary>
        public ItemDefinition Put(ItemDefinition item, List<(ItemDefinition item, int count)> spilled)
        {
            var slot = item.EquipSlot;
            var old = this[slot];
            worn[slot] = item;
            if (slot == EquipSlot.Back) Inventory.Resize(GridWith(item), spilled);
            Refresh();
            return old;
        }

        /// <summary>
        /// Takes off what is worn in a slot and returns it (null when nothing is). Without the backpack only the
        /// pockets are left; what does not fit them is added to <paramref name="spilled"/>.
        /// </summary>
        public ItemDefinition Remove(EquipSlot slot, List<(ItemDefinition item, int count)> spilled)
        {
            var item = this[slot];
            if (item == null) return null;

            worn.Remove(slot);
            if (slot == EquipSlot.Back) Inventory.Resize(Pockets, spilled);
            Refresh();
            return item;
        }

        /// <summary>Takes everything off and drops nothing: the player died and it is all lost.</summary>
        public void Clear()
        {
            worn.Clear();
            Inventory.Resize(Pockets, new List<(ItemDefinition item, int count)>());
            Refresh();
        }

        // ---- the save file ----

        public void Export(InventoryData data)
        {
            data.head = IdOf(EquipSlot.Head);
            data.back = IdOf(EquipSlot.Back);
            data.feet = IdOf(EquipSlot.Feet);
        }

        /// <summary>
        /// Puts on what the save says (a thing that no longer exists, or does not belong in that slot, is skipped)
        /// and sizes the grid to match. Called before the grid is filled from the same save.
        /// </summary>
        public void Import(InventoryData data)
        {
            worn.Clear();
            if (data != null)
            {
                Restore(EquipSlot.Head, data.head);
                Restore(EquipSlot.Back, data.back);
                Restore(EquipSlot.Feet, data.feet);
            }
            Inventory.Resize(GridWith(this[EquipSlot.Back]), new List<(ItemDefinition item, int count)>());
            Refresh();
        }

        private void Restore(EquipSlot slot, string id)
        {
            var item = string.IsNullOrEmpty(id) ? null : ItemDatabase.Find(id);
            if (item != null && item.EquipSlot == slot) worn[slot] = item;
        }

        private string IdOf(EquipSlot slot)
        {
            var item = this[slot];
            return item != null ? item.Id : string.Empty;
        }

        private void Refresh()
        {
            Inventory.ExtraWeight = worn.Values.Where(i => i != null).Sum(i => i.Weight);
            Changed?.Invoke();
        }

        private void Drop(List<(ItemDefinition item, int count)> things)
        {
            if (things.Count == 0) return;

            var interactor = GetComponent<PlayerInteractor>();
            for (int i = 0; i < things.Count; i++)
            {
                // A small ring keeps the things from lying exactly on top of each other.
                float angle = i * Mathf.PI * 2f / things.Count;
                var spread = things.Count > 1 ? new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.35f : Vector2.zero;
                var (item, count) = things[i];
                if (interactor == null || !interactor.DropToWorld(item, count, spread))
                    Debug.LogWarning($"PlayerEquipment: {item.Id} ×{count} could not be put on the floor and is lost.", this);
            }
        }
    }
}
