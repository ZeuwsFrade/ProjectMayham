using System;
using UnityEngine;

namespace ProjectMayham.Items
{
    /// <summary>
    /// Fixed number of slots, each holding a stack of one item kind. The selected slot is the one the hotbar
    /// highlights and the one that is dropped.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [Serializable]
        public struct Slot
        {
            public ItemDefinition item;
            public int count;

            public bool IsEmpty => item == null || count <= 0;
        }

        [SerializeField, Min(1)] private int slotCount = 5;

        private Slot[] slots;
        private int selected;

        // Created on first use: the hotbar can subscribe before this component's Awake has run.
        private Slot[] Slots => slots ??= new Slot[slotCount];

        /// <summary>Raised whenever the contents of any slot change.</summary>
        public event Action Changed;
        /// <summary>Raised with the new index when the selected slot changes.</summary>
        public event Action<int> SelectionChanged;

        public int SlotCount => Slots.Length;
        public int Selected => selected;
        public Slot this[int index] => Slots[index];

        /// <summary>True when at least one item of this kind would be accepted.</summary>
        public bool CanAdd(ItemDefinition item)
        {
            if (item == null) return false;
            foreach (var slot in Slots)
            {
                if (slot.IsEmpty || (slot.item == item && slot.count < item.MaxStack)) return true;
            }
            return false;
        }

        /// <summary>Adds items to existing stacks first, then to empty slots. Returns how many did not fit.</summary>
        public int Add(ItemDefinition item, int count)
        {
            if (item == null || count <= 0) return count;

            int left = count;
            for (int i = 0; i < Slots.Length && left > 0; i++)
            {
                if (Slots[i].IsEmpty || Slots[i].item != item) continue;
                int moved = Mathf.Min(left, item.MaxStack - Slots[i].count);
                Slots[i].count += moved;
                left -= moved;
            }
            for (int i = 0; i < Slots.Length && left > 0; i++)
            {
                if (!Slots[i].IsEmpty) continue;
                int moved = Mathf.Min(left, item.MaxStack);
                Slots[i] = new Slot { item = item, count = moved };
                left -= moved;
            }

            if (left != count) Changed?.Invoke();
            return left;
        }

        /// <summary>Empties the selected slot and hands back what was in it.</summary>
        public bool TakeSelected(out ItemDefinition item, out int count)
        {
            item = null;
            count = 0;
            if (Slots[selected].IsEmpty) return false;

            item = Slots[selected].item;
            count = Slots[selected].count;
            Slots[selected] = default;
            Changed?.Invoke();
            return true;
        }

        public void Select(int index)
        {
            index = Mathf.Clamp(index, 0, Slots.Length - 1);
            if (index == selected) return;
            selected = index;
            SelectionChanged?.Invoke(selected);
        }

        /// <summary>Moves the selection by <paramref name="step"/> slots and wraps around.</summary>
        public void Cycle(int step) => Select((int)Mathf.Repeat(selected + step, Slots.Length));
    }
}
