using System.Collections.Generic;
using ProjectMayham.Items;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    /// <summary>
    /// Two grid inventories side by side (the player's and a storage) with items moved between them: drag a stack
    /// with the left mouse button (R turns it), or right click / Shift+click to send it to the other side at once.
    /// Ctrl+click on a stack opens the split panel: how many, then "move" them to the other side or "split" them off
    /// into a stack of their own. Next to the player's grid are the two hands and what is worn, as in the inventory:
    /// drag a thing onto a hand to hold it (a two-handed thing takes both), onto its slot to put it on (what was worn
    /// takes its place), from one hand onto the other to move it there; drag out of a slot, or right click /
    /// Shift+click it, to put the thing away. A double click on a thing that is
    /// worn puts it on, from either side. Dropping on a stack of the same item merges them. A sealed stack (a thing
    /// in a container that is not found yet) is drawn as "?" and cannot be touched. The pointer is read straight
    /// from the devices.
    /// </summary>
    public class TransferWindow : ModalWindow
    {
        private const float Cell = 56f;
        private const string PersonRefusal = "Человека сюда не положить";
        // Two presses on the same thing within this time are a double click.
        private const float DoubleClickSeconds = 0.35f;
        // How long a refusal stays under the grids before the hover text comes back.
        private const float MessageSeconds = 2f;
        // A storage that can hold more than this has no weight limit worth showing.
        private const float UnlimitedWeight = 10000f;
        // A grid narrower than this has room for the name of its storage only, not for the weight next to it.
        private const float WeightLabelMinWidth = 400f;
        // The column of the hands and the worn things.
        private const float SlotSize = 76f, SlotTitle = 24f, SlotGap = 8f, ColumnGap = 24f;
        // The width of the canvas the windows are laid out for.
        private const float ScreenWidth = 1920f;

        private static readonly Color ItemColor = new Color(0.17f, 0.21f, 0.25f, 0.96f);
        private static readonly Color SealedColor = new Color(0.11f, 0.12f, 0.15f, 0.96f);
        private static readonly Color ValidColor = new Color(0.3f, 0.9f, 0.4f, 0.4f);
        private static readonly Color MergeColor = new Color(0.35f, 0.7f, 1f, 0.45f);
        private static readonly Color InvalidColor = new Color(0.95f, 0.3f, 0.3f, 0.4f);
        private static readonly Color SlotIdle = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color SecondHandIcon = new Color(1f, 1f, 1f, 0.4f);

        private class View
        {
            public GridInventory Inventory;
            public RectTransform Grid;       // top-left pivot; cell (0, 0) is at its top-left corner
            public RectTransform Items;
            public Image Highlight;
            public TMP_Text WeightLabel;
            public string Name;
            public readonly List<GameObject> Widgets = new List<GameObject>();
        }

        // A hand, or a slot of what is worn.
        private class SlotView
        {
            public Hand? Hand;
            public EquipSlot Slot;
            public RectTransform Rect;
            public Image Frame;
            public Image Icon;
            public bool IsHands => Hand.HasValue;
        }

        private readonly View[] views = new View[2];
        private readonly List<SlotView> slots = new List<SlotView>();
        // The sizes of the grids the UI was built for: the player's grid changes with the backpack.
        private Vector2Int builtLeft, builtRight;
        private RectTransform panel;
        private RectTransform ghost;
        private TMP_Text infoLabel;
        private PlayerEquipment equipment;

        private GridInventory.Stack lastClicked;
        private float lastClickTime;
        private float messageUntil;

        private View dragFrom;
        private GridInventory.Stack dragged;
        private SlotView draggedSlot;      // dragging out of the hands or a slot instead of a grid
        private ItemDefinition draggedItem;
        private bool dragRotated;
        private Vector2 grabOffset;

        // The split panel.
        private RectTransform splitPanel;
        private TMP_Text splitTitle, splitValue;
        private View splitFrom;
        private GridInventory.Stack splitStack;
        private int splitAmount;

        protected override int SortingOrder => 200;

        /// <summary>Opens the player's inventory (left) and a storage (right).</summary>
        public static void Open(GridInventory player, GridInventory storage, string storageName)
        {
            var window = Get<TransferWindow>();
            if (window.IsOpen) return;
            window.Prepare(player, storage, storageName);
        }

        protected void Prepare(GridInventory player, GridInventory storage, string storageName)
        {
            // The layout depends on the sizes of the two grids, so another pair of inventories gets a new one.
            if (views[0] == null || views[0].Inventory != player || views[1].Inventory != storage
                || builtLeft != player.GridSize || builtRight != storage.GridSize)
            {
                DiscardUI();
                views[0] = new View { Inventory = player, Name = "Рюкзак" };
                views[1] = new View { Inventory = storage, Name = storageName };
            }
            Show();
        }

        /// <summary>How dark the game behind the window gets.</summary>
        protected virtual Color DimColor => UIKit.Dim;

        /// <summary>Where the middle of the panel is, counted from the middle of the screen.</summary>
        protected virtual Vector2 PanelOffset => Vector2.zero;

        /// <summary>The line under the grids while the pointer is over nothing.</summary>
        protected virtual string Hint => DefaultHint;

        /// <summary>Font size of that line.</summary>
        protected virtual float HintSize => 22f;

        /// <summary>The least width the storage gets in the panel, so that small storages do not make a tiny window.</summary>
        protected virtual float MinStorageWidth => 0f;

        /// <summary>The rectangle of the storage's grid, for a window that draws over it; null before the window is built.</summary>
        protected RectTransform StorageGrid => views[1] != null ? views[1].Grid : null;

        /// <summary>True while the storage cannot be touched: what is in it does not show yet.</summary>
        protected virtual bool StorageBusy => false;

        /// <summary>False while the "?" of the things not found yet are not to be drawn either.</summary>
        protected virtual bool ShowSealed => true;

        /// <summary>Draws the stacks again, for a window whose own state changes what is shown.</summary>
        protected void Redraw() => Rebuild();

        /// <summary>Called for every stack that was just drawn, for a window that wants to add something to it.</summary>
        protected virtual void OnWidgetBuilt(GridInventory inventory, GridInventory.Stack stack, RectTransform widget) { }

        private GridInventory Player => views[0].Inventory;
        private GridInventory Storage => views[1].Inventory;
        private bool IsSplitting => splitStack != null;

        protected override void Build(Canvas canvas)
        {
            var dim = UIKit.Panel(canvas.transform, "Dim", DimColor);
            UIKit.Stretch(dim.rectTransform);

            var left = views[0].Inventory.GridSize;
            var right = views[1].Inventory.GridSize;
            builtLeft = left;
            builtRight = right;
            float leftWidth = left.x * Cell, rightWidth = Mathf.Max(right.x * Cell, MinStorageWidth);
            float columnHeight = 5f * (SlotTitle + SlotSize) + 4f * SlotGap;
            float gridsHeight = Mathf.Max(Mathf.Max(left.y, right.y) * Cell, columnHeight);
            const float margin = 36f, gap = 60f, top = 110f, bottom = 120f;

            float width = margin * 2f + leftWidth + ColumnGap + SlotSize + gap + rightWidth;
            float height = top + gridsHeight + bottom;
            panel = UIKit.Panel(dim.transform, "Panel", UIKit.PanelColor).rectTransform;
            // A shifted panel with a big backpack would leave the screen: it moves back as far as needed.
            var offset = PanelOffset;
            float room = Mathf.Max(0f, (ScreenWidth - width) * 0.5f - 12f);
            offset.x = Mathf.Clamp(offset.x, -room, room);
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), offset, new Vector2(width, height));

            BuildView(views[0], new Vector2(margin, -top), left);
            BuildColumn(new Vector2(margin + leftWidth + ColumnGap, -top));
            BuildView(views[1], new Vector2(margin + leftWidth + ColumnGap + SlotSize + gap, -top), right);

            infoLabel = UIKit.Label(panel, string.Empty, HintSize, TextAlignmentOptions.Left, UIKit.Muted);
            UIKit.Place(infoLabel.rectTransform, new Vector2(0f, 0f), new Vector2(margin, 16f), new Vector2(width - margin * 2f - 280f, 90f));
            infoLabel.rectTransform.pivot = new Vector2(0f, 0f);

            var close = UIKit.MakeButton(panel, "Закрыть (Esc)", Close, 26f);
            var closeRect = (RectTransform)close.transform;
            UIKit.Place(closeRect, new Vector2(1f, 0f), new Vector2(-margin, 24f), new Vector2(250f, 56f));
            closeRect.pivot = new Vector2(1f, 0f);

            BuildSplitPanel();

            ghost = UIKit.Rect("Ghost", panel);
            ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
            ghost.pivot = new Vector2(0f, 1f);
            var group = ghost.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0.85f;
            group.blocksRaycasts = false;
            ghost.gameObject.SetActive(false);
        }

        private void BuildView(View view, Vector2 topLeft, Vector2Int size)
        {
            view.Widgets.Clear();   // what was drawn before went with the old UI
            float gridWidth = size.x * Cell;
            bool showWeight = view.Inventory.MaxWeight < UnlimitedWeight || gridWidth >= WeightLabelMinWidth;
            var title = UIKit.Label(panel, view.Name, showWeight ? 34f : 30f, TextAlignmentOptions.Left, UIKit.Accent);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), topLeft + new Vector2(0f, 60f),
                new Vector2(showWeight ? gridWidth - 200f : Mathf.Max(gridWidth, 260f), 44f));
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.text = view.Name;
            if (showWeight)
            {
                view.WeightLabel = UIKit.Label(panel, string.Empty, 24f, TextAlignmentOptions.Right, UIKit.Muted);
                UIKit.Place(view.WeightLabel.rectTransform, new Vector2(0f, 1f), topLeft + new Vector2(gridWidth, 60f), new Vector2(260f, 44f));
                view.WeightLabel.rectTransform.pivot = new Vector2(1f, 1f);
            }
            else
            {
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.overflowMode = TextOverflowModes.Overflow;
            }

            var grid = UIKit.Panel(panel, "Grid", new Color(0f, 0f, 0f, 0.35f)).rectTransform;
            UIKit.Place(grid, new Vector2(0f, 1f), topLeft, new Vector2(size.x * Cell, size.y * Cell));
            grid.pivot = new Vector2(0f, 1f);
            view.Grid = grid;

            // Faint cell lines so the grid reads as cells.
            for (int x = 1; x < size.x; x++) AddLine(grid, new Vector2(x * Cell, 0f), new Vector2(1f, size.y * Cell));
            for (int y = 1; y < size.y; y++) AddLine(grid, new Vector2(0f, -y * Cell), new Vector2(size.x * Cell, 1f));

            view.Items = UIKit.Rect("Items", grid);
            view.Items.anchorMin = view.Items.anchorMax = view.Items.pivot = new Vector2(0f, 1f);
            view.Items.anchoredPosition = Vector2.zero;

            view.Highlight = UIKit.Panel(grid, "Highlight", ValidColor);
            view.Highlight.raycastTarget = false;
            view.Highlight.rectTransform.anchorMin = view.Highlight.rectTransform.anchorMax = view.Highlight.rectTransform.pivot = new Vector2(0f, 1f);
            view.Highlight.enabled = false;
        }

        // The hands, then what is worn: head, backpack, feet.
        private void BuildColumn(Vector2 topLeft)
        {
            slots.Clear();
            var kinds = new List<SlotView> { new SlotView { Hand = Hand.Right }, new SlotView { Hand = Hand.Left } };
            foreach (var worn in PlayerEquipment.Slots) kinds.Add(new SlotView { Slot = worn });
            float y = topLeft.y;
            foreach (var kind in kinds)
            {
                var title = UIKit.Label(panel, SlotTitleOf(kind), 18f, TextAlignmentOptions.Center, UIKit.Muted);
                UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(topLeft.x - 10f, y), new Vector2(SlotSize + 20f, SlotTitle));
                title.rectTransform.pivot = new Vector2(0f, 1f);
                title.textWrappingMode = TextWrappingModes.NoWrap;

                var frame = UIKit.Panel(panel, "Slot", SlotIdle);
                UIKit.Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(topLeft.x, y - SlotTitle), new Vector2(SlotSize, SlotSize));
                frame.rectTransform.pivot = new Vector2(0f, 1f);
                var back = UIKit.Panel(frame.rectTransform, "Background", ItemColor);
                UIKit.Stretch(back.rectTransform, 2f);
                back.raycastTarget = false;
                var icon = UIKit.Panel(frame.rectTransform, "Icon", Color.white);
                UIKit.Stretch(icon.rectTransform, 8f);
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                kind.Rect = frame.rectTransform;
                kind.Frame = frame;
                kind.Icon = icon;
                slots.Add(kind);
                y -= SlotTitle + SlotSize + SlotGap;
            }
        }

        private void BuildSplitPanel()
        {
            splitPanel = UIKit.Panel(panel, "Split", new Color(0.05f, 0.06f, 0.08f, 0.98f)).rectTransform;
            UIKit.Place(splitPanel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 230f));

            splitTitle = UIKit.Label(splitPanel, string.Empty, 26f, TextAlignmentOptions.Center, UIKit.Accent);
            UIKit.Place(splitTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(520f, 40f));
            splitTitle.rectTransform.pivot = new Vector2(0.5f, 1f);

            var minus = UIKit.MakeButton(splitPanel, "−", () => ChangeSplit(-1), 32f);
            UIKit.Place((RectTransform)minus.transform, new Vector2(0.5f, 1f), new Vector2(-110f, -96f), new Vector2(64f, 52f));
            splitValue = UIKit.Label(splitPanel, string.Empty, 34f, TextAlignmentOptions.Center);
            UIKit.Place(splitValue.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(120f, 52f));
            var plus = UIKit.MakeButton(splitPanel, "+", () => ChangeSplit(1), 32f);
            UIKit.Place((RectTransform)plus.transform, new Vector2(0.5f, 1f), new Vector2(110f, -96f), new Vector2(64f, 52f));

            var move = UIKit.MakeButton(splitPanel, "Перенести", MoveSplit, 24f);
            UIKit.Place((RectTransform)move.transform, new Vector2(0.5f, 0f), new Vector2(-180f, 24f), new Vector2(170f, 50f));
            var apart = UIKit.MakeButton(splitPanel, "Отделить", SplitApart, 24f);
            UIKit.Place((RectTransform)apart.transform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(170f, 50f));
            var cancel = UIKit.MakeButton(splitPanel, "Отмена", CloseSplit, 24f);
            UIKit.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(180f, 24f), new Vector2(170f, 50f));

            splitPanel.gameObject.SetActive(false);
        }

        private static void AddLine(RectTransform parent, Vector2 position, Vector2 size)
        {
            var line = UIKit.Panel(parent, "Line", new Color(1f, 1f, 1f, 0.07f));
            line.raycastTarget = false;
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = line.rectTransform.pivot = new Vector2(0f, 1f);
            line.rectTransform.anchoredPosition = position;
            line.rectTransform.sizeDelta = size;
        }

        protected override void OnOpened()
        {
            foreach (var view in views) view.Inventory.Changed += Rebuild;
            equipment = Player.GetComponent<PlayerEquipment>();
            if (equipment != null) equipment.Changed += Rebuild;
            infoLabel.text = Hint;
            messageUntil = 0f;
            Rebuild();
        }

        protected override void OnClosed()
        {
            CancelDrag();
            CloseSplit();
            foreach (var view in views) view.Inventory.Changed -= Rebuild;
            if (equipment != null) equipment.Changed -= Rebuild;
            equipment = null;
        }

        // Esc closes the split panel first, then the window.
        protected override void OnCancel()
        {
            if (IsSplitting) CloseSplit();
            else Close();
        }

        private const string DefaultHint = "Мышь — перетаскивать, R — повернуть, Ctrl+клик — разделить. ПКМ или Shift+клик — на другую сторону. " +
                                           "Руки и одежда — перетащить в слот или из слота (тяжёлое берётся в обе руки), двойной клик — надеть.";

        // ---- input ----

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();

            if (IsSplitting)
            {
                UpdateSplit(mouse, keyboard);
                return;
            }

            if (draggedItem != null)
            {
                if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Rotate();
                UpdateDrag(screen);
                if (mouse.leftButton.wasReleasedThisFrame) EndDrag(screen);
                return;
            }

            bool shift = keyboard != null && keyboard.shiftKey.isPressed;
            bool ctrl = keyboard != null && keyboard.ctrlKey.isPressed;

            var slot = SlotUnder(screen);
            if (slot != null)
            {
                var item = ItemIn(slot);
                Hover(SlotInfo(slot, item));
                if (item == null) return;
                if (mouse.rightButton.wasPressedThisFrame || (shift && mouse.leftButton.wasPressedThisFrame)) PutAway(slot);
                else if (mouse.leftButton.wasPressedThisFrame) BeginSlotDrag(slot, item, screen);
                return;
            }

            var view = ViewUnder(screen, out var stack);
            Hover(stack != null ? Describe(stack) : Hint);
            // What is not found yet cannot be taken.
            if (stack == null || stack.Sealed) return;

            if (mouse.rightButton.wasPressedThisFrame || (shift && mouse.leftButton.wasPressedThisFrame)) QuickTransfer(view, stack);
            else if (ctrl && mouse.leftButton.wasPressedThisFrame) OpenSplit(view, stack);
            else if (mouse.leftButton.wasPressedThisFrame)
            {
                if (IsDoubleClick(stack)) Wear(view, stack);
                else BeginDrag(view, stack, screen);
            }
        }

        // The time is the real one: the stash window stops the game.
        private bool IsDoubleClick(GridInventory.Stack stack)
        {
            bool twice = ReferenceEquals(stack, lastClicked) && Time.unscaledTime - lastClickTime <= DoubleClickSeconds;
            lastClicked = twice ? null : stack;
            lastClickTime = Time.unscaledTime;
            return twice;
        }

        private void Wear(View view, GridInventory.Stack stack)
        {
            if (!stack.Item.Wearable || equipment == null) return;
            if (!equipment.WearFrom(view.Inventory, stack)) Say("Не надеть: будет слишком тяжело");
        }

        // A refusal or a result: stays for a while.
        private void Say(string text)
        {
            infoLabel.text = text;
            messageUntil = Time.unscaledTime + MessageSeconds;
        }

        // What the pointer is over: shown unless a message is still up.
        private void Hover(string text)
        {
            if (Time.unscaledTime >= messageUntil) infoLabel.text = text;
        }

        private View ViewUnder(Vector2 screen, out GridInventory.Stack stack)
        {
            stack = null;
            foreach (var view in views)
            {
                if (view == views[1] && StorageBusy) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(view.Grid, screen, null)) continue;
                if (ToLocal(view, screen, out var local)) stack = view.Inventory.StackAt(CellOf(local));
                return view;
            }
            return null;
        }

        private SlotView SlotUnder(Vector2 screen)
        {
            foreach (var slot in slots)
            {
                // Without equipment there are the hands only.
                if (!slot.IsHands && equipment == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(slot.Rect, screen, null)) return slot;
            }
            return null;
        }

        private ItemDefinition ItemIn(SlotView slot) =>
            slot.IsHands ? Player.InHand(slot.Hand.Value) : equipment != null ? equipment[slot.Slot] : null;

        private static string SlotTitleOf(SlotView slot) =>
            slot.Hand == Hand.Right ? "Правая рука" : slot.Hand == Hand.Left ? "Левая рука" : PlayerEquipment.SlotTitle(slot.Slot);

        private string SlotInfo(SlotView slot, ItemDefinition item)
        {
            string title = SlotTitleOf(slot);
            if (item == null)
                return slot.IsHands ? $"{title}: пусто. Перетащите сюда то, что можно держать" : $"{title}: пусто. Перетащите сюда вещь или двойной клик по ней — надеть";
            string both = slot.IsHands && Player.HoldsTwoHanded ? ", в обеих руках" : string.Empty;
            return $"{title}: {item.DisplayName}{both}. ПКМ или Shift+клик — убрать на другую сторону, перетащите — положить куда нужно";
        }

        private static bool ToLocal(View view, Vector2 screen, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(view.Grid, screen, null, out local);

        private static Vector2Int CellOf(Vector2 local) =>
            new Vector2Int(Mathf.FloorToInt(local.x / Cell), Mathf.FloorToInt(-local.y / Cell));

        private static Vector2Int OriginFor(Vector2 topLeft) =>
            new Vector2Int(Mathf.RoundToInt(topLeft.x / Cell), Mathf.RoundToInt(-topLeft.y / Cell));

        private static Vector2 TopLeftOf(Vector2Int cell) => new Vector2(cell.x * Cell, -cell.y * Cell);

        private static Vector2 Footprint(ItemDefinition item, bool rotated)
        {
            var size = item.GridSize;
            return (rotated ? new Vector2(size.y, size.x) : new Vector2(size.x, size.y)) * Cell;
        }

        private View Other(View view) => view == views[0] ? views[1] : views[0];

        // ---- moving ----

        private void QuickTransfer(View from, GridInventory.Stack stack)
        {
            var to = Other(from);
            if (to == views[1] && stack.Item.IsPerson)
            {
                Say(PersonRefusal);
                return;
            }
            if (to == views[1] && StorageBusy) return;
            var item = stack.Item;
            int count = stack.Count;
            int left = to.Inventory.Add(item, count);
            if (left >= count) { Say("Не помещается: нет места или слишком тяжело"); return; }
            from.Inventory.Reduce(stack, count - left);
        }

        // Right click or Shift+click on the hands or a slot: the thing goes to the storage.
        private void PutAway(SlotView slot)
        {
            if (StorageBusy) return;
            var item = ItemIn(slot);
            if (item == null) return;
            if (item.IsPerson)
            {
                Say(PersonRefusal);
                return;
            }
            if (slot.IsHands)
            {
                if (Storage.Add(item, 1) == 0) Player.TakeHeld(slot.Hand.Value, out _);
                else Say("Не помещается: нет места или слишком тяжело");
                return;
            }
            if (!equipment.TakeOffTo(Storage, slot.Slot)) Say("Не помещается: нет места или слишком тяжело");
        }

        private void BeginDrag(View view, GridInventory.Stack stack, Vector2 screen)
        {
            if (!ToLocal(view, screen, out var local)) return;
            dragFrom = view;
            dragged = stack;
            draggedItem = stack.Item;
            dragRotated = stack.Rotated;
            grabOffset = local - TopLeftOf(stack.Origin);
            BuildGhost(stack.Count);
            Rebuild();
            UpdateDrag(screen);
        }

        // Out of the hands or a slot: one thing, held by its middle.
        private void BeginSlotDrag(SlotView slot, ItemDefinition item, Vector2 screen)
        {
            draggedSlot = slot;
            draggedItem = item;
            dragRotated = false;
            grabOffset = Footprint(item, false) * 0.5f;
            grabOffset.y = -grabOffset.y;
            BuildGhost(1);
            Rebuild();
            UpdateDrag(screen);
        }

        private void Rotate()
        {
            if (!draggedItem.CanRotate) return;
            dragRotated = !dragRotated;
            BuildGhost(dragged != null ? dragged.Count : 1);
            // Keep the item under the cursor: hold it by its centre after turning.
            grabOffset = Footprint(draggedItem, dragRotated) * 0.5f;
            grabOffset.y = -grabOffset.y;
        }

        private void UpdateDrag(Vector2 screen)
        {
            // The ghost follows the cursor in the coordinates of the panel.
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, screen, null, out var inPanel))
            {
                ghost.anchoredPosition = inPanel - new Vector2(grabOffset.x, grabOffset.y);
            }

            foreach (var view in views) view.Highlight.enabled = false;
            var over = SlotUnder(screen);
            foreach (var slot in slots) slot.Frame.color = slot != over || slot == draggedSlot ? SlotIdle : FitsSlot(slot) ? ValidColor : InvalidColor;
            if (over != null)
            {
                if (over != draggedSlot) Hover(!FitsSlot(over) ? "Сюда это не годится"
                    : !over.IsHands ? "Отпустите, чтобы надеть"
                    : draggedSlot != null ? "Отпустите, чтобы переложить в другую руку"
                    : draggedItem.TwoHanded ? "Отпустите, чтобы взять в обе руки" : "Отпустите, чтобы взять в эту руку");
                return;
            }

            var target = ViewUnder(screen, out _);
            if (target == null || !ToLocal(target, screen, out var local)) return;

            var origin = OriginFor(local - grabOffset);
            var kind = dragged != null && target == dragFrom
                ? target.Inventory.Evaluate(dragged, origin, dragRotated)
                : target.Inventory.EvaluateItem(draggedItem, origin, dragRotated);
            var highlight = target.Highlight;
            highlight.enabled = true;
            highlight.color = kind == GridInventory.MoveKind.Move ? ValidColor
                : kind == GridInventory.MoveKind.Merge ? MergeColor : InvalidColor;
            highlight.rectTransform.sizeDelta = Footprint(draggedItem, dragRotated);
            highlight.rectTransform.anchoredPosition = TopLeftOf(origin);
        }

        // What is dragged can go into this slot: something to hold into a hand (or the other hand's thing), a thing
        // worn there onto its slot.
        private bool FitsSlot(SlotView slot)
        {
            if (draggedItem == null || slot == draggedSlot) return false;
            if (draggedSlot != null) return draggedSlot.IsHands && slot.IsHands && !Player.HoldsTwoHanded;
            return slot.IsHands ? draggedItem.Holdable : draggedItem.Wearable && draggedItem.EquipSlot == slot.Slot && equipment != null;
        }

        private void EndDrag(Vector2 screen)
        {
            var from = dragFrom;
            var stack = dragged;
            var fromSlot = draggedSlot;
            var item = draggedItem;
            bool rotated = dragRotated;
            var overSlot = SlotUnder(screen);
            var target = overSlot == null ? ViewUnder(screen, out _) : null;
            Vector2 local = default;
            bool onGrid = target != null && ToLocal(target, screen, out local);
            var origin = onGrid ? OriginFor(local - grabOffset) : default;
            bool fits = overSlot != null && FitsSlot(overSlot);
            CancelDrag();

            if (fromSlot != null)
            {
                // From one hand onto the other: the hands trade what they hold.
                if (fits) Player.SwapHands();
                else if (onGrid) DropFromSlot(fromSlot, item, target, origin, rotated);
                return;
            }
            if (overSlot != null)
            {
                if (fits) DropOnSlot(overSlot, from, stack);
                else Say("Сюда это не годится");
                return;
            }
            if (!onGrid) return; // dropped outside of the grids: it stays where it was

            if (target == views[1] && from != target && stack.Item.IsPerson)
            {
                Say(PersonRefusal);
                return;
            }
            if (target == from)
            {
                from.Inventory.TryMove(stack, origin, rotated);
                return;
            }

            int placed = target.Inventory.PlaceItem(stack.Item, stack.Count, origin, rotated);
            if (placed > 0) from.Inventory.Reduce(stack, placed);
            else Say("Сюда не положить: место занято или слишком тяжело");
        }

        // A stack dropped on the hands (one piece is held) or on a slot of what is worn (it is put on).
        private void DropOnSlot(SlotView slot, View from, GridInventory.Stack stack)
        {
            if (!slot.IsHands)
            {
                if (!equipment.WearFrom(from.Inventory, stack)) Say("Не надеть: будет слишком тяжело");
                return;
            }
            var hand = slot.Hand.Value;
            if (from == views[0])
            {
                if (!Player.Hold(stack, hand)) Say("Не взять в руки: то, что в них, некуда положить");
                return;
            }
            // From the storage: what the hands held goes into the backpack, else on the floor.
            if (Player.HoldNew(stack.Item, hand)) from.Inventory.Reduce(stack, 1);
            else Say("Не взять в руки: слишком тяжело");
        }

        // The thing in the hands or a slot dropped on a grid.
        private void DropFromSlot(SlotView slot, ItemDefinition item, View target, Vector2Int origin, bool rotated)
        {
            if (target == views[1])
            {
                if (StorageBusy) return;
                if (item.IsPerson)
                {
                    Say(PersonRefusal);
                    return;
                }
                if (slot.IsHands)
                {
                    if (Storage.PlaceItem(item, 1, origin, rotated) == 1) Player.TakeHeld(slot.Hand.Value, out _);
                    else Say("Сюда не положить: место занято или слишком тяжело");
                }
                else if (!equipment.TakeOffTo(Storage, slot.Slot, origin, rotated)) Say("Не помещается: нет места или слишком тяжело");
                return;
            }

            if (slot.IsHands)
            {
                if (!Player.PlaceHeld(slot.Hand.Value, origin, rotated)) Say("Сюда не положить: место занято");
            }
            else if (!equipment.TakeOffToGrid(slot.Slot, origin, rotated)) Say("Сюда не положить: место занято");
        }

        private void CancelDrag()
        {
            if (draggedItem == null) return;
            dragged = null;
            dragFrom = null;
            draggedSlot = null;
            draggedItem = null;
            if (ghost != null) ghost.gameObject.SetActive(false);
            foreach (var view in views) view.Highlight.enabled = false;
            foreach (var slot in slots) slot.Frame.color = SlotIdle;
            Rebuild();
        }

        // ---- splitting ----

        private void OpenSplit(View view, GridInventory.Stack stack)
        {
            if (stack.Count < 2)
            {
                Say("Здесь одна штука — делить нечего");
                return;
            }
            splitFrom = view;
            splitStack = stack;
            splitAmount = Mathf.Max(1, stack.Count / 2);
            splitPanel.gameObject.SetActive(true);
            splitPanel.SetAsLastSibling();
            RefreshSplit();
        }

        private void CloseSplit()
        {
            splitStack = null;
            splitFrom = null;
            if (splitPanel != null) splitPanel.gameObject.SetActive(false);
        }

        private void ChangeSplit(int step)
        {
            if (!IsSplitting) return;
            splitAmount = Mathf.Clamp(splitAmount + step, 1, splitStack.Count - 1);
            RefreshSplit();
        }

        private void RefreshSplit()
        {
            splitTitle.text = $"{splitStack.Item.DisplayName} ×{splitStack.Count}: сколько?";
            splitValue.text = splitAmount.ToString();
        }

        // Arrows and the wheel change the number, Enter moves them to the other side.
        private void UpdateSplit(Mouse mouse, Keyboard keyboard)
        {
            if (splitStack == null || !Holds(splitFrom.Inventory, splitStack))
            {
                CloseSplit();
                return;
            }
            float wheel = mouse.scroll.ReadValue().y;
            if (wheel > 0.01f) ChangeSplit(1);
            else if (wheel < -0.01f) ChangeSplit(-1);
            if (keyboard == null) return;
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) ChangeSplit(1);
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) ChangeSplit(-1);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) MoveSplit();
        }

        private static bool Holds(GridInventory inventory, GridInventory.Stack stack)
        {
            foreach (var other in inventory.Stacks)
                if (ReferenceEquals(other, stack)) return true;
            return false;
        }

        // So many of the stack go to the other side.
        private void MoveSplit()
        {
            if (!IsSplitting) return;
            var from = splitFrom;
            var stack = splitStack;
            int amount = splitAmount;
            CloseSplit();

            var to = Other(from);
            if (to == views[1] && StorageBusy) return;
            int left = to.Inventory.Add(stack.Item, amount);
            int moved = amount - left;
            if (moved <= 0) { Say("Не помещается: нет места или слишком тяжело"); return; }
            from.Inventory.Reduce(stack, moved);
            if (left > 0) Say($"Поместилось только {moved}");
        }

        // So many of the stack become a stack of their own beside it.
        private void SplitApart()
        {
            if (!IsSplitting) return;
            var from = splitFrom;
            var stack = splitStack;
            int amount = splitAmount;
            CloseSplit();
            if (from.Inventory.Split(stack, amount) == null) Say("Не разделить: нет свободного места");
        }

        // ---- drawing ----

        private void BuildGhost(int count)
        {
            for (int i = ghost.childCount - 1; i >= 0; i--) Destroy(ghost.GetChild(i).gameObject);
            ghost.sizeDelta = Footprint(draggedItem, dragRotated);
            CreateWidget(ghost, draggedItem, count, dragRotated, Vector2.zero);
            ghost.gameObject.SetActive(true);
        }

        private void Rebuild()
        {
            if (!IsOpen) return;
            // A backpack was put on while the window is open: the player's grid has another size now.
            if (views[0].Inventory.GridSize != builtLeft || views[1].Inventory.GridSize != builtRight)
            {
                CancelDrag();
                CloseSplit();
                RebuildUI();
                infoLabel.text = Hint;
            }
            foreach (var view in views)
            {
                foreach (var widget in view.Widgets) Destroy(widget);
                view.Widgets.Clear();

                foreach (var stack in view.Inventory.Stacks)
                {
                    if (stack == dragged || (stack.Sealed && !ShowSealed)) continue;
                    var widget = stack.Sealed
                        ? CreateSealedWidget(view.Items, stack)
                        : CreateWidget(view.Items, stack.Item, stack.Count, stack.Rotated, TopLeftOf(stack.Origin));
                    view.Widgets.Add(widget.gameObject);
                    OnWidgetBuilt(view.Inventory, stack, widget);
                }
                view.Highlight.transform.SetAsLastSibling();
                if (view.WeightLabel == null) continue;
                // A storage without a practical weight limit shows only what it holds.
                bool limited = view.Inventory.MaxWeight < UnlimitedWeight;
                view.WeightLabel.text = limited
                    ? $"{view.Inventory.TotalWeight:0.#} / {view.Inventory.MaxWeight:0.#} кг"
                    : $"{view.Inventory.TotalWeight:0.#} кг";
            }

            bool both = Player.HoldsTwoHanded;
            foreach (var slot in slots)
            {
                bool shown = slot.IsHands || equipment != null;
                slot.Rect.gameObject.SetActive(shown);
                // What is dragged out of a hand leaves it; a two-handed thing leaves both.
                bool dragging = slot == draggedSlot || (both && slot.IsHands && draggedSlot != null && draggedSlot.IsHands);
                var item = shown && !dragging ? ItemIn(slot) : null;
                slot.Icon.enabled = item != null;
                slot.Icon.sprite = item != null ? item.Icon : null;
                // A two-handed thing shows faded in the left hand: it is the same thing.
                slot.Icon.color = both && slot.Hand == Hand.Left ? SecondHandIcon : Color.white;
            }
            if (splitPanel != null && IsSplitting) splitPanel.SetAsLastSibling();
            if (ghost != null) ghost.SetAsLastSibling();
        }

        private static RectTransform CreateWidget(Transform parent, ItemDefinition item, int count, bool rotated, Vector2 topLeft)
        {
            var root = UIKit.Rect("Stack", parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = topLeft;
            root.sizeDelta = Footprint(item, rotated);

            var frame = UIKit.Panel(root, "Frame", new Color(0f, 0f, 0f, 0.6f));
            UIKit.Stretch(frame.rectTransform);
            frame.raycastTarget = false;

            var back = UIKit.Panel(root, "Background", ItemColor);
            UIKit.Stretch(back.rectTransform, 2f);
            back.raycastTarget = false;

            var icon = UIKit.Panel(root, "Icon", Color.white);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(item.GridSize.x, item.GridSize.y) * Cell - Vector2.one * 10f;
            icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotated ? -90f : 0f);
            icon.sprite = item.Icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            if (count > 1)
            {
                var label = UIKit.Label(root, count.ToString(), 20f, TextAlignmentOptions.BottomRight);
                UIKit.Stretch(label.rectTransform, 5f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }
            return root;
        }

        // A thing that is not found yet: its place and its size show, what it is does not.
        private static RectTransform CreateSealedWidget(Transform parent, GridInventory.Stack stack)
        {
            var root = UIKit.Rect("Hidden", parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = TopLeftOf(stack.Origin);
            root.sizeDelta = Footprint(stack.Item, stack.Rotated);

            var frame = UIKit.Panel(root, "Frame", new Color(0f, 0f, 0f, 0.6f));
            UIKit.Stretch(frame.rectTransform);
            frame.raycastTarget = false;

            var back = UIKit.Panel(root, "Background", SealedColor);
            UIKit.Stretch(back.rectTransform, 2f);
            back.raycastTarget = false;

            var mark = UIKit.Label(root, "?", 34f, TextAlignmentOptions.Center, UIKit.Muted);
            UIKit.Stretch(mark.rectTransform);
            return root;
        }

        private static string Describe(GridInventory.Stack stack)
        {
            if (stack.Sealed) return "Ещё не найдено";

            var item = stack.Item;
            string count = stack.Count > 1 ? $" ×{stack.Count}" : string.Empty;
            string wear = item.Wearable ? ". Двойной клик — надеть" : string.Empty;
            string split = stack.Count > 1 ? ". Ctrl+клик — разделить" : string.Empty;
            return $"{item.DisplayName}{count} — {item.Weight * stack.Count:0.##} кг ({item.Weight:0.##} кг/шт), {item.GridSize.x}×{item.GridSize.y}{wear}{split}";
        }
    }
}
