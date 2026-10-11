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
    /// Dropping on a stack of the same item merges them. A sealed stack (a thing in a container that is not found
    /// yet) is drawn as "?" and cannot be touched. The pointer is read straight from the devices.
    /// </summary>
    public class TransferWindow : ModalWindow
    {
        private const float Cell = 56f;
        // A storage that can hold more than this has no weight limit worth showing.
        private const float UnlimitedWeight = 10000f;
        // A grid narrower than this has room for the name of its storage only, not for the weight next to it.
        private const float WeightLabelMinWidth = 400f;

        private static readonly Color ItemColor = new Color(0.17f, 0.21f, 0.25f, 0.96f);
        private static readonly Color SealedColor = new Color(0.11f, 0.12f, 0.15f, 0.96f);
        private static readonly Color ValidColor = new Color(0.3f, 0.9f, 0.4f, 0.4f);
        private static readonly Color MergeColor = new Color(0.35f, 0.7f, 1f, 0.45f);
        private static readonly Color InvalidColor = new Color(0.95f, 0.3f, 0.3f, 0.4f);

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

        private readonly View[] views = new View[2];
        private RectTransform panel;
        private RectTransform ghost;
        private TMP_Text infoLabel;

        private View dragFrom;
        private GridInventory.Stack dragged;
        private bool dragRotated;
        private Vector2 grabOffset;

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
            if (views[0] == null || views[0].Inventory != player || views[1].Inventory != storage)
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
        protected virtual float HintSize => 24f;

        /// <summary>The least width the storage gets in the panel, so that small storages do not make a tiny window.</summary>
        protected virtual float MinStorageWidth => 0f;

        /// <summary>Called for every stack that was just drawn, for a window that wants to add something to it.</summary>
        protected virtual void OnWidgetBuilt(GridInventory inventory, GridInventory.Stack stack, RectTransform widget) { }

        protected override void Build(Canvas canvas)
        {
            var dim = UIKit.Panel(canvas.transform, "Dim", DimColor);
            UIKit.Stretch(dim.rectTransform);

            var left = views[0].Inventory.GridSize;
            var right = views[1].Inventory.GridSize;
            float leftWidth = left.x * Cell, rightWidth = Mathf.Max(right.x * Cell, MinStorageWidth);
            float gridsHeight = Mathf.Max(left.y, right.y) * Cell;
            const float margin = 36f, gap = 70f, top = 110f, bottom = 120f;

            float width = margin * 2f + leftWidth + gap + rightWidth;
            float height = top + gridsHeight + bottom;
            panel = UIKit.Panel(dim.transform, "Panel", UIKit.PanelColor).rectTransform;
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), PanelOffset, new Vector2(width, height));

            BuildView(views[0], new Vector2(margin, -top), left);
            BuildView(views[1], new Vector2(margin + leftWidth + gap, -top), right);

            infoLabel = UIKit.Label(panel, string.Empty, HintSize, TextAlignmentOptions.Left, UIKit.Muted);
            UIKit.Place(infoLabel.rectTransform, new Vector2(0f, 0f), new Vector2(margin, 20f), new Vector2(width - margin * 2f - 280f, 80f));
            infoLabel.rectTransform.pivot = new Vector2(0f, 0f);

            var close = UIKit.MakeButton(panel, "Закрыть (Esc)", Close, 26f);
            var closeRect = (RectTransform)close.transform;
            UIKit.Place(closeRect, new Vector2(1f, 0f), new Vector2(-margin, 24f), new Vector2(250f, 56f));
            closeRect.pivot = new Vector2(1f, 0f);

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
            infoLabel.text = Hint;
            Rebuild();
        }

        protected override void OnClosed()
        {
            CancelDrag();
            foreach (var view in views) view.Inventory.Changed -= Rebuild;
        }

        private const string DefaultHint = "Перетаскивайте предметы мышью, R — повернуть. Правый клик или Shift+клик — перенести на другую сторону.";

        // ---- input ----

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();

            if (dragged != null)
            {
                if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Rotate();
                UpdateDrag(screen);
                if (mouse.leftButton.wasReleasedThisFrame) EndDrag(screen);
                return;
            }

            var view = ViewUnder(screen, out var stack);
            infoLabel.text = stack != null ? Describe(stack) : Hint;
            // What is not found yet cannot be taken.
            if (stack == null || stack.Sealed) return;

            bool shift = keyboard != null && keyboard.shiftKey.isPressed;
            if (mouse.rightButton.wasPressedThisFrame || (shift && mouse.leftButton.wasPressedThisFrame)) QuickTransfer(view, stack);
            else if (mouse.leftButton.wasPressedThisFrame) BeginDrag(view, stack, screen);
        }

        private View ViewUnder(Vector2 screen, out GridInventory.Stack stack)
        {
            stack = null;
            foreach (var view in views)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(view.Grid, screen, null)) continue;
                if (ToLocal(view, screen, out var local)) stack = view.Inventory.StackAt(CellOf(local));
                return view;
            }
            return null;
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
            var item = stack.Item;
            int count = stack.Count;
            int left = to.Inventory.Add(item, count);
            if (left >= count) { infoLabel.text = "Не помещается: нет места или слишком тяжело"; return; }
            from.Inventory.Reduce(stack, count - left);
        }

        private void BeginDrag(View view, GridInventory.Stack stack, Vector2 screen)
        {
            if (!ToLocal(view, screen, out var local)) return;
            dragFrom = view;
            dragged = stack;
            dragRotated = stack.Rotated;
            grabOffset = local - TopLeftOf(stack.Origin);
            BuildGhost();
            Rebuild();
            UpdateDrag(screen);
        }

        private void Rotate()
        {
            if (!dragged.Item.CanRotate) return;
            dragRotated = !dragRotated;
            BuildGhost();
            // Keep the item under the cursor: hold it by its centre after turning.
            grabOffset = Footprint(dragged.Item, dragRotated) * 0.5f;
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
            var target = ViewUnder(screen, out _);
            if (target == null || !ToLocal(target, screen, out var local)) return;

            var origin = OriginFor(local - grabOffset);
            var kind = target == dragFrom
                ? target.Inventory.Evaluate(dragged, origin, dragRotated)
                : target.Inventory.EvaluateItem(dragged.Item, origin, dragRotated);
            var highlight = target.Highlight;
            highlight.enabled = true;
            highlight.color = kind == GridInventory.MoveKind.Move ? ValidColor
                : kind == GridInventory.MoveKind.Merge ? MergeColor : InvalidColor;
            highlight.rectTransform.sizeDelta = Footprint(dragged.Item, dragRotated);
            highlight.rectTransform.anchoredPosition = TopLeftOf(origin);
        }

        private void EndDrag(Vector2 screen)
        {
            var from = dragFrom;
            var stack = dragged;
            bool rotated = dragRotated;
            var target = ViewUnder(screen, out _);
            Vector2 local = default;
            bool onGrid = target != null && ToLocal(target, screen, out local);
            var origin = onGrid ? OriginFor(local - grabOffset) : default;
            CancelDrag();

            if (!onGrid) return; // dropped outside of the grids: it stays where it was

            if (target == from)
            {
                from.Inventory.TryMove(stack, origin, rotated);
                return;
            }

            int placed = target.Inventory.PlaceItem(stack.Item, stack.Count, origin, rotated);
            if (placed > 0) from.Inventory.Reduce(stack, placed);
            else infoLabel.text = "Сюда не положить: место занято или слишком тяжело";
        }

        private void CancelDrag()
        {
            if (dragged == null) return;
            dragged = null;
            dragFrom = null;
            if (ghost != null) ghost.gameObject.SetActive(false);
            foreach (var view in views) view.Highlight.enabled = false;
            Rebuild();
        }

        // ---- drawing ----

        private void BuildGhost()
        {
            for (int i = ghost.childCount - 1; i >= 0; i--) Destroy(ghost.GetChild(i).gameObject);
            ghost.sizeDelta = Footprint(dragged.Item, dragRotated);
            CreateWidget(ghost, dragged.Item, dragged.Count, dragRotated, Vector2.zero);
            ghost.gameObject.SetActive(true);
        }

        private void Rebuild()
        {
            if (!IsOpen) return;
            foreach (var view in views)
            {
                foreach (var widget in view.Widgets) Destroy(widget);
                view.Widgets.Clear();

                foreach (var stack in view.Inventory.Stacks)
                {
                    if (stack == dragged) continue;
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
            return $"{item.DisplayName}{count} — {item.Weight * stack.Count:0.##} кг ({item.Weight:0.##} кг/шт), {item.GridSize.x}×{item.GridSize.y}";
        }
    }
}
