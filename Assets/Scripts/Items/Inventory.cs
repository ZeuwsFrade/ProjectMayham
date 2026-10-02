using System;
using UnityEngine;

namespace ProjectMayham.Items
{
    /// <summary>
    /// Fixed number of slots, each holding a stack of one item kind. The selected slot is the one the hotbar
    /// highlights and the one that is dropped. Also tracks the carried weight: above the comfortable weight the
    /// owner is encumbered, and nothing that would go over the max weight is accepted.
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

        // Keeps float sums like 0.1 + 0.2 from refusing an item that fits exactly.
        private const float WeightEpsilon = 0.0001f;

        [SerializeField, Min(1)] private int slotCount = 5;

        [Header("Weight")]
        [Tooltip("Up to this weight (kg) the owner moves at full speed and can sprint.")]
        [SerializeField, Min(0f)] private float comfortableWeight = 10f;
        [Tooltip("Hard limit (kg): items that would go over it cannot be picked up.")]
        [SerializeField, Min(0f)] private float maxWeight = 25f;

        private Slot[] slots;
        private int selected;
        private float totalWeight;

        // Created on first use: the hotbar can subscribe before this component's Awake has run.
        private Slot[] Slots => slots ??= new Slot[slotCount];

        /// <summary>Raised whenever the contents of any slot change.</summary>
        public event Action Changed;
        /// <summary>Raised with the new index when the selected slot changes.</summary>
        public event Action<int> SelectionChanged;

        public int SlotCount => Slots.Length;
        public int Selected => selected;
        public Slot this[int index] => Slots[index];

        public float TotalWeight => totalWeight;
        public float ComfortableWeight => comfortableWeight;
        public float MaxWeight => Mathf.Max(maxWeight, comfortableWeight);
        /// <summary>True above the comfortable weight: slower movement, no sprinting.</summary>
        public bool IsOverloaded => totalWeight > comfortableWeight + WeightEpsilon;
        /// <summary>0 at or below the comfortable weight, 1 at the max weight.</summary>
        public float Encumbrance
        {
            get
            {
                float range = MaxWeight - comfortableWeight;
                if (range <= WeightEpsilon) return IsOverloaded ? 1f : 0f;
                return Mathf.Clamp01((totalWeight - comfortableWeight) / range);
            }
        }

        /// <summary>True when at least one item of this kind would be accepted.</summary>
        public bool CanAdd(ItemDefinition item) => HasRoomFor(item) && FitsByWeight(item);

        /// <summary>True when a slot could take one more item of this kind, ignoring weight.</summary>
        public bool HasRoomFor(ItemDefinition item)
        {
            if (item == null) return false;
            foreach (var slot in Slots)
            {
                if (slot.IsEmpty || (slot.item == item && slot.count < item.MaxStack)) return true;
            }
            return false;
        }

        /// <summary>True when one more item of this kind stays within the max weight.</summary>
        public bool FitsByWeight(ItemDefinition item) => item != null && CountByWeight(item) > 0;

        /// <summary>Adds items to existing stacks first, then to empty slots. Returns how many did not fit.</summary>
        public int Add(ItemDefinition item, int count)
        {
            if (item == null || count <= 0) return count;

            int accepted = Mathf.Min(count, CountByWeight(item));
            int left = accepted;
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

            int added = accepted - left;
            if (added > 0)
            {
                RecalculateWeight();
                Changed?.Invoke();
            }
            return count - added;
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
            RecalculateWeight();
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

        /// <summary>How many items of this kind the remaining weight allowance can take.</summary>
        private int CountByWeight(ItemDefinition item)
        {
            if (item.Weight <= 0f) return int.MaxValue;
            float free = MaxWeight - totalWeight;
            return Mathf.Max(0, Mathf.FloorToInt((free + WeightEpsilon) / item.Weight));
        }

        private void RecalculateWeight()
        {
            float sum = 0f;
            foreach (var slot in Slots)
            {
                if (!slot.IsEmpty) sum += slot.item.Weight * slot.count;
            }
            totalWeight = sum;
        }
    }
}
