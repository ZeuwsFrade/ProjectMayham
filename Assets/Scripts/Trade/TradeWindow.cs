using System;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMayham.Trade
{
    /// <summary>
    /// Trade window with three tabs: buy from the trader, sell from the player's grid, and barter (offers of
    /// things for things). Rows are rebuilt after every deal.
    /// </summary>
    public class TradeWindow : ModalWindow
    {
        private enum Tab { Buy, Sell, Barter }

        private TraderBook book;
        private GridInventory inventory;
        private Action onClosed;
        private Tab tab;

        private TMP_Text titleLabel;
        private TMP_Text moneyLabel;
        private TMP_Text statusLabel;
        private RectTransform list;
        private Button[] tabButtons;

        protected override int SortingOrder => 200;

        public static void Open(TraderBook book, GridInventory inventory, Action onClosed = null)
        {
            var window = Get<TradeWindow>();
            if (window.IsOpen) return;
            window.book = book;
            window.inventory = inventory;
            window.onClosed = onClosed;
            window.Show();
        }

        protected override void Build(Canvas canvas)
        {
            var dim = UIKit.Panel(canvas.transform, "Dim", UIKit.Dim);
            UIKit.Stretch(dim.rectTransform);

            var panel = UIKit.Panel(dim.transform, "Panel", UIKit.PanelColor).rectTransform;
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1280f, 860f));

            titleLabel = UIKit.Label(panel, string.Empty, 40f, TextAlignmentOptions.Left, UIKit.Accent);
            UIKit.Place(titleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(700f, 60f));
            titleLabel.rectTransform.pivot = new Vector2(0f, 1f);

            moneyLabel = UIKit.Label(panel, string.Empty, 34f, TextAlignmentOptions.Right);
            UIKit.Place(moneyLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(480f, 56f));
            moneyLabel.rectTransform.pivot = new Vector2(1f, 1f);

            tabButtons = new Button[3];
            string[] names = { "Купить", "Продать", "Обмен" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var button = UIKit.MakeButton(panel, names[i], () => SetTab((Tab)index), 28f);
                var rect = (RectTransform)button.transform;
                UIKit.Place(rect, new Vector2(0f, 1f), new Vector2(30f + i * 230f, -95f), new Vector2(220f, 56f));
                rect.pivot = new Vector2(0f, 1f);
                tabButtons[i] = button;
            }

            var content = UIKit.ScrollList(panel, "List", out _);
            var viewport = (RectTransform)content.parent;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(30f, 110f);
            viewport.offsetMax = new Vector2(-30f, -165f);
            list = content;

            statusLabel = UIKit.Label(panel, string.Empty, 26f, TextAlignmentOptions.Left, UIKit.Muted);
            UIKit.Place(statusLabel.rectTransform, new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(900f, 60f));
            statusLabel.rectTransform.pivot = new Vector2(0f, 0f);

            var close = UIKit.MakeButton(panel, "Закрыть (Esc)", Close, 26f);
            var closeRect = (RectTransform)close.transform;
            UIKit.Place(closeRect, new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(260f, 56f));
            closeRect.pivot = new Vector2(1f, 0f);
        }

        protected override void OnOpened()
        {
            titleLabel.text = book.Definition.DisplayName;
            tab = Tab.Buy;
            statusLabel.text = string.Empty;
            GameSession.MoneyChanged += OnMoneyChanged;
            Refresh();
        }

        protected override void OnClosed()
        {
            GameSession.MoneyChanged -= OnMoneyChanged;
            var callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }

        private void OnMoneyChanged(int money) => moneyLabel.text = $"Деньги: {money} $";

        private void SetTab(Tab newTab)
        {
            tab = newTab;
            statusLabel.text = string.Empty;
            Refresh();
        }

        private void Refresh()
        {
            moneyLabel.text = $"Деньги: {GameSession.Money} $";
            for (int i = 0; i < tabButtons.Length; i++)
            {
                // The open tab has an accent-colored caption.
                tabButtons[i].GetComponentInChildren<TMP_Text>().color = i == (int)tab ? UIKit.Accent : Color.white;
            }

            UIKit.ClearChildren(list);
            switch (tab)
            {
                case Tab.Buy: BuildBuy(); break;
                case Tab.Sell: BuildSell(); break;
                default: BuildBarter(); break;
            }
        }

        private void Report(string text, bool error)
        {
            statusLabel.text = text;
            statusLabel.color = error ? UIKit.Bad : UIKit.Muted;
        }

        // ---- tabs ----

        private void BuildBuy()
        {
            var wares = book.Wares.Where(w => w.Count > 0).ToList();
            if (wares.Count == 0) AddHint("У торговца ничего нет.");

            foreach (var ware in wares)
            {
                var item = ware.Item;
                int price = book.BuyPrice(item);
                var row = NewRow();
                AddIcon(row, item);
                AddText(row, $"{item.DisplayName}\n<size=22><color=#FFFFFF99>{item.Weight:0.##} кг, в наличии {ware.Count}</color></size>", 0f, true);
                AddText(row, $"{price} $", 140f, false, TextAlignmentOptions.Right);
                AddButton(row, "Купить", () => Buy(item, 1), GameSession.Money >= price);
                AddButton(row, "×5", () => Buy(item, 5), GameSession.Money >= price && ware.Count >= 2, 90f);
            }
        }

        private void BuildSell()
        {
            var items = inventory.Stacks.Select(s => s.Item).Distinct().OrderBy(i => i.DisplayName).ToList();
            if (items.Count == 0) AddHint("Ваш инвентарь пуст.");

            foreach (var item in items)
            {
                int price = book.SellPrice(item);
                int have = inventory.CountOf(item);
                var row = NewRow();
                AddIcon(row, item);
                AddText(row, $"{item.DisplayName}\n<size=22><color=#FFFFFF99>есть {have}</color></size>", 0f, true);
                AddText(row, price > 0 ? $"{price} $" : "не нужно", 140f, false, TextAlignmentOptions.Right);
                AddButton(row, "Продать", () => Sell(item, 1), price > 0);
                AddButton(row, "Все", () => Sell(item, have), price > 0 && have > 1, 90f);
            }
        }

        private void BuildBarter()
        {
            var offers = book.Definition.Barter;
            if (offers.Count == 0) AddHint("Торговец пока не предлагает обмен.");

            foreach (var offer in offers)
            {
                var row = NewRow();
                string give = Describe(offer.playerGives);
                string receive = Describe(offer.playerReceives);
                string title = string.IsNullOrEmpty(offer.title) ? string.Empty : $"<color=#FFD65A>{offer.title}</color>\n";
                AddText(row, $"{title}Отдаёте: {give}\nПолучаете: {receive}", 0f, true);
                AddButton(row, "Обменять", () => Barter(offer), TraderBook.HasItems(inventory, offer), 180f);
            }
        }

        private static string Describe(System.Collections.Generic.IEnumerable<ItemAmount> amounts) =>
            string.Join(", ", amounts.Where(a => a.item != null).Select(a => a.count > 1 ? $"{a.item.DisplayName} ×{a.count}" : a.item.DisplayName));

        // ---- deals ----

        private void Buy(ItemDefinition item, int count)
        {
            if (book.TryBuy(inventory, item, count, out string error)) Report($"Куплено: {item.DisplayName}", false);
            else Report(error, true);
            Refresh();
        }

        private void Sell(ItemDefinition item, int count)
        {
            if (book.TrySell(inventory, item, count, out string error)) Report($"Продано: {item.DisplayName}", false);
            else Report(error, true);
            Refresh();
        }

        private void Barter(BarterOffer offer)
        {
            if (book.TryBarter(inventory, offer, out string error)) Report("Обмен состоялся", false);
            else Report(error, true);
            Refresh();
        }

        // ---- row building ----

        private RectTransform NewRow()
        {
            var image = UIKit.Panel(list, "Row", UIKit.RowColor);
            var layout = image.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(12, 12, 6, 6);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            image.gameObject.AddComponent<LayoutElement>().minHeight = 76f;
            return image.rectTransform;
        }

        private static void AddIcon(RectTransform row, ItemDefinition item)
        {
            var icon = UIKit.Panel(row, "Icon", Color.white);
            icon.sprite = item.Icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var element = icon.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.preferredHeight = 60f;
        }

        private static void AddText(RectTransform row, string text, float width, bool flexible,
            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var label = UIKit.Label(row, text, 28f, align);
            label.richText = true;
            var element = label.gameObject.AddComponent<LayoutElement>();
            if (flexible) element.flexibleWidth = 1f;
            else element.preferredWidth = width;
        }

        private static void AddButton(RectTransform row, string text, Action onClick, bool enabled, float width = 150f)
        {
            var button = UIKit.MakeButton(row, text, () => onClick(), 26f);
            button.interactable = enabled;
            var element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = 56f;
        }

        private void AddHint(string text)
        {
            var label = UIKit.Label(list, text, 28f, TextAlignmentOptions.Center, UIKit.Muted);
            label.gameObject.AddComponent<LayoutElement>().minHeight = 80f;
        }
    }
}
