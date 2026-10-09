using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Items;
using UnityEngine;

namespace ProjectMayham.Trade
{
    /// <summary>
    /// The running state of one trader: what is left on his shelves and all the rules of buying, selling and
    /// bartering. The stock is read from and written to the game session. UI-free, so any window can use it.
    /// </summary>
    public class TraderBook
    {
        public class Ware
        {
            public ItemDefinition Item;
            public int Count;
            public int PriceOverride;
        }

        private readonly TraderDefinition definition;
        private readonly List<Ware> wares = new List<Ware>();

        public TraderBook(TraderDefinition definition)
        {
            this.definition = definition;
            LoadStock();
            GameSession.SaveRequested += SaveStock;
        }

        /// <summary>Stops listening to saves; call when the scene object that owns the book goes away.</summary>
        public void Dispose() => GameSession.SaveRequested -= SaveStock;

        public TraderDefinition Definition => definition;
        public IReadOnlyList<Ware> Wares => wares;

        // ---- prices ----

        /// <summary>What the player pays for one piece.</summary>
        public int BuyPrice(ItemDefinition item)
        {
            var ware = wares.Find(w => w.Item == item);
            if (ware != null && ware.PriceOverride > 0) return ware.PriceOverride;
            return Mathf.Max(1, Mathf.CeilToInt(item.Value * definition.SellMarkup));
        }

        /// <summary>What the player gets for one piece; 0 means the trader does not want it.</summary>
        public int SellPrice(ItemDefinition item)
        {
            if (item.Value <= 0) return 0;
            return Mathf.Max(1, Mathf.FloorToInt(item.Value * definition.BuyRate));
        }

        public int CountInStock(ItemDefinition item) => wares.Where(w => w.Item == item).Sum(w => w.Count);

        // ---- buying and selling ----

        /// <summary>Buys items from the trader. On failure nothing changes and <paramref name="error"/> says why.</summary>
        public bool TryBuy(GridInventory inventory, ItemDefinition item, int count, out string error)
        {
            error = null;
            var ware = wares.Find(w => w.Item == item);
            if (ware == null || ware.Count <= 0) { error = "Этого больше нет"; return false; }

            count = Mathf.Min(count, ware.Count);
            int price = BuyPrice(item) * count;
            if (GameSession.Money < price) { error = "Не хватает денег"; return false; }
            if (!inventory.CanFit(item, count)) { error = "Нет места или слишком тяжело"; return false; }

            GameSession.TrySpend(price);
            inventory.Add(item, count);
            ware.Count -= count;
            return true;
        }

        /// <summary>Sells items from the player's grid to the trader, who keeps them in stock.</summary>
        public bool TrySell(GridInventory inventory, ItemDefinition item, int count, out string error)
        {
            error = null;
            int unit = SellPrice(item);
            if (unit <= 0) { error = "Торговцу это не нужно"; return false; }

            count = Mathf.Min(count, inventory.CountOf(item));
            if (count <= 0) { error = "Нет предмета"; return false; }

            int taken = inventory.RemoveItems(item, count);
            GameSession.AddMoney(unit * taken);

            var ware = wares.Find(w => w.Item == item);
            if (ware == null) wares.Add(new Ware { Item = item, Count = taken });
            else ware.Count += taken;
            return true;
        }

        // ---- barter ----

        /// <summary>True when the player has everything the offer asks for.</summary>
        public static bool HasItems(GridInventory inventory, BarterOffer offer) =>
            offer.playerGives.All(g => g.item != null && inventory.CountOf(g.item) >= g.count);

        /// <summary>Performs a barter. The inventory is restored exactly when the result does not fit.</summary>
        public bool TryBarter(GridInventory inventory, BarterOffer offer, out string error)
        {
            error = null;
            if (!HasItems(inventory, offer)) { error = "Не хватает предметов для обмена"; return false; }

            var snapshot = inventory.Export();
            foreach (var give in offer.playerGives) inventory.RemoveItems(give.item, give.count);

            foreach (var receive in offer.playerReceives)
            {
                if (receive.item == null || inventory.Add(receive.item, receive.count) <= 0) continue;

                inventory.Import(snapshot);
                error = "Нет места или слишком тяжело";
                return false;
            }
            return true;
        }

        // ---- stock persistence ----

        private void LoadStock()
        {
            wares.Clear();
            var saved = GameSession.Data.traders.Find(t => t.trader == definition.Id);
            if (saved == null)
            {
                foreach (var entry in definition.Stock)
                {
                    if (entry.item != null) wares.Add(new Ware { Item = entry.item, Count = entry.count, PriceOverride = entry.priceOverride });
                }
                return;
            }

            foreach (var line in saved.stock)
            {
                var item = ItemDatabase.Find(line.item);
                if (item == null) continue;
                var defined = definition.Stock.FirstOrDefault(e => e.item == item);
                wares.Add(new Ware { Item = item, Count = line.count, PriceOverride = defined != null ? defined.priceOverride : 0 });
            }
        }

        private void SaveStock()
        {
            var data = GameSession.Data;
            var saved = data.traders.Find(t => t.trader == definition.Id);
            if (saved == null)
            {
                saved = new TraderStockData { trader = definition.Id };
                data.traders.Add(saved);
            }
            saved.stock = wares.Select(w => new StockData { item = w.Item.Id, count = w.Count }).ToList();
        }
    }
}
