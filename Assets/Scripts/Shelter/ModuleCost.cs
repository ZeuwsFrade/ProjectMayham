using System.Collections.Generic;
using ProjectMayham.Items;
using ProjectMayham.Trade;

namespace ProjectMayham.Shelter
{
    /// <summary>
    /// Counting and paying what a module takes out of several inventories at once (the backpack and the stash):
    /// what lies in their grids and what their hands hold. Paying empties them in the order they are given.
    /// </summary>
    public static class ModuleCost
    {
        /// <summary>How many pieces of the item the inventories hold together.</summary>
        public static int Count(ItemDefinition item, IEnumerable<GridInventory> stores)
        {
            int total = 0;
            if (item == null || stores == null) return total;
            foreach (var store in stores)
            {
                if (store != null) total += store.CountOf(item) + store.HeldCount(item);
            }
            return total;
        }

        /// <summary>True when the inventories hold everything the cost asks for.</summary>
        public static bool CanAfford(IReadOnlyList<ItemAmount> cost, IEnumerable<GridInventory> stores)
        {
            if (cost == null) return true;
            foreach (var part in cost)
            {
                if (part == null || part.item == null) continue;
                if (Count(part.item, stores) < Need(part)) return false;
            }
            return true;
        }

        /// <summary>Takes the cost out of the inventories, the first one first, grids before hands. False when they do not hold it all.</summary>
        public static bool Take(IReadOnlyList<ItemAmount> cost, IReadOnlyList<GridInventory> stores)
        {
            if (!CanAfford(cost, stores)) return false;
            if (cost == null) return true;
            foreach (var part in cost)
            {
                if (part == null || part.item == null) continue;
                int left = Need(part);
                foreach (var store in stores)
                {
                    if (store == null || left <= 0) continue;
                    left -= store.RemoveItems(part.item, left);
                    while (left > 0 && store.TakeFromHands(part.item)) left--;
                }
            }
            return true;
        }

        /// <summary>
        /// Where the shelter pays from and puts things into: the actor's backpack first, then the stash of the scene
        /// (when there is one).
        /// </summary>
        public static List<GridInventory> StoresOf(UnityEngine.GameObject actor)
        {
            var stores = new List<GridInventory>();
            if (actor != null && actor.TryGetComponent<GridInventory>(out var backpack)) stores.Add(backpack);
            var stash = UnityEngine.Object.FindFirstObjectByType<StashChest>();
            if (stash != null && stash.Inventory != null) stores.Add(stash.Inventory);
            return stores;
        }

        /// <summary>The number a cost line asks for: an amount left at 0 means one piece.</summary>
        public static int Need(ItemAmount part) => part.count > 0 ? part.count : 1;
    }
}
