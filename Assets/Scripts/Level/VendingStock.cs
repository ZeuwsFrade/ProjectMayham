using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Items;

namespace ProjectMayham.Level
{
    /// <summary>
    /// What is inside one vending machine and how it behaves, without the machine itself: a few things rolled from a
    /// loot table when the raid starts (sometimes none), taken out one by one when bought; shaking gives a random one
    /// with some luck, and after a few failures the machine jams for good.
    /// </summary>
    public class VendingStock
    {
        public class Slot
        {
            public ItemDefinition Item;
            public int Count;
        }

        public enum ShakeResult { Dropped, Nothing, Jammed, Empty, AlreadyJammed }

        private readonly List<Slot> slots = new List<Slot>();
        private int failures;

        public IReadOnlyList<Slot> Slots => slots;
        public bool IsEmpty => slots.Count == 0;
        public bool Jammed { get; private set; }
        public int Total => slots.Sum(s => s.Count);

        /// <summary>
        /// Rolls the contents: nothing with <paramref name="emptyChance"/>, else between <paramref name="min"/> and
        /// <paramref name="max"/> things from the table (the same thing may come up more than once).
        /// </summary>
        public void Fill(LootTable table, int min, int max, float emptyChance, System.Random dice)
        {
            slots.Clear();
            failures = 0;
            Jammed = false;
            if (table == null || dice.NextDouble() < emptyChance) return;

            int count = dice.Next(min, max + 1);
            for (int i = 0; i < count; i++)
            {
                var entry = table.Roll(dice);
                if (entry == null || entry.item == null) continue;
                Add(entry.item, 1);
            }
        }

        public void Add(ItemDefinition item, int count)
        {
            if (item == null || count <= 0) return;
            var slot = slots.FirstOrDefault(s => s.Item == item);
            if (slot != null) slot.Count += count;
            else slots.Add(new Slot { Item = item, Count = count });
        }

        /// <summary>Takes one of the item out; false when there is none.</summary>
        public bool Take(ItemDefinition item)
        {
            var slot = slots.FirstOrDefault(s => s.Item == item);
            if (slot == null) return false;
            if (--slot.Count <= 0) slots.Remove(slot);
            return true;
        }

        /// <summary>
        /// One shake: <paramref name="roll"/> (0..1) below <paramref name="chance"/> drops a random thing (picked with
        /// <paramref name="pick"/>, 0..1); otherwise a failure, and the failure number <paramref name="jamAfter"/> jams it.
        /// </summary>
        public ShakeResult Shake(double roll, double pick, float chance, int jamAfter, out ItemDefinition dropped)
        {
            dropped = null;
            if (Jammed) return ShakeResult.AlreadyJammed;
            if (IsEmpty) return ShakeResult.Empty;
            if (roll < chance)
            {
                // Every thing inside is as likely as any other.
                int index = (int)(pick * Total);
                foreach (var slot in slots.ToList())
                {
                    if (index < slot.Count)
                    {
                        dropped = slot.Item;
                        Take(slot.Item);
                        return ShakeResult.Dropped;
                    }
                    index -= slot.Count;
                }
                dropped = slots[slots.Count - 1].Item;
                Take(dropped);
                return ShakeResult.Dropped;
            }

            if (++failures < jamAfter) return ShakeResult.Nothing;
            Jammed = true;
            return ShakeResult.Jammed;
        }

        /// <summary>Takes one coin from the backpack, from the grid first, then the hands. False when there is none.</summary>
        public static bool PayCoin(GridInventory backpack, ItemDefinition coin)
        {
            if (backpack == null || coin == null) return false;
            if (backpack.RemoveItems(coin, 1) > 0) return true;
            return backpack.TakeFromHands(coin);
        }

        /// <summary>Coins the player carries: in the grid and in the hands.</summary>
        public static int Coins(GridInventory backpack, ItemDefinition coin) =>
            backpack == null || coin == null ? 0 : backpack.CountOf(coin) + backpack.HeldCount(coin);
    }
}
