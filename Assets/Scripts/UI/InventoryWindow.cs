using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Interaction;
using ProjectMayham.Items;
using ProjectMayham.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    /// <summary>
    /// Window of a <see cref="GridInventory"/>, opened with I. Drag a stack with the left mouse button to move it,
    /// press R while dragging to turn it, drop it on a stack of the same item to merge, or release it outside of
    /// the window to throw it on the floor. Shift+click adds a stack to the selection or removes it; a selected
    /// group is moved or thrown away together, and G drops it as well. Dragging a frame over empty cells selects
    /// everything the frame touches (with Shift it is added to the selection). Ctrl+click splits a stack. There are
    /// two hands, the right one and the left one below it; the active one is lit (Q or a click on a hand changes
    /// it). Right click takes one piece of a holdable item in the active hand, dropping it on a hand takes it in that
    /// one; a two-handed thing takes both. Dragging a thing out of a hand puts it back, onto the other hand moves it
    /// there. Next to the hands are the slots of what is worn (<see cref="PlayerEquipment"/>): right
    /// click or dropping a thing on its slot puts it on, right click on a slot or dragging out of it takes it off.
    /// The backpack sets the size of the grid, so the grid and the window are laid out from code for whatever
    /// size it has. Esc closes the window (or the split panel). Also keeps the always-visible weight label
    /// up to date. The pointer and keys are read directly from the devices, so no EventSystem is needed.
    /// </summary>
    public class InventoryWindow : MonoBehaviour
    {
        private static readonly Color ItemColor = new Color(0.17f, 0.21f, 0.25f, 0.96f);
        private static readonly Color SelectedColor = new Color(1f, 0.82f, 0.35f, 1f);
        private static readonly Color ValidColor = new Color(0.3f, 0.9f, 0.4f, 0.4f);
        private static readonly Color MergeColor = new Color(0.35f, 0.7f, 1f, 0.45f);
        private static readonly Color InvalidColor = new Color(0.95f, 0.3f, 0.3f, 0.4f);
        private static readonly Color OverloadedColor = new Color(1f, 0.7f, 0.25f, 1f);
        private static readonly Color HandsIdle = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color ActiveHandIdle = new Color(0.42f, 0.33f, 0.1f, 0.8f);
        private static readonly Color SecondHandIcon = new Color(1f, 1f, 1f, 0.4f);

        [Header("References")]
        [SerializeField] private GridInventory inventory;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private RectTransform window;
        [Tooltip("Top-left pivot. Cell (0, 0) is at its top-left corner.")]
        [SerializeField] private RectTransform grid;
        [SerializeField] private RectTransform itemsLayer;
        [Tooltip("Template of the cell highlight shown while dragging; copies are made for groups.")]
        [SerializeField] private Image highlight;
        [Tooltip("Top-left pivot. Holds copies of the stacks that are being dragged.")]
        [SerializeField] private RectTransform ghost;
        [SerializeField] private TMP_Text weightLabel;
        [SerializeField] private TMP_Text infoLabel;
        [Tooltip("Small weight readout that stays on screen while the window is closed.")]
        [SerializeField] private TMP_Text weightHud;

        [Header("Hands")]
        [Tooltip("Slot for the item held in the right hand; the left hand gets a copy of it.")]
        [SerializeField] private RectTransform handsSlot;
        [Tooltip("Frame behind the slot; it lights up when a holdable item is dragged over it.")]
        [SerializeField] private Image handsFrame;
        [SerializeField] private Image handsIcon;

        [Header("Split panel")]
        [SerializeField] private RectTransform splitPanel;
        [SerializeField] private TMP_Text splitValue;
        [SerializeField] private RectTransform splitMinus;
        [SerializeField] private RectTransform splitPlus;
        [SerializeField] private RectTransform splitOk;
        [SerializeField] private RectTransform splitCancel;

        [Header("Settings")]
        [SerializeField] private float cellSize = 64f;
        [SerializeField] private Key toggleKey = Key.I;

        private readonly List<RectTransform> widgets = new List<RectTransform>();
        private readonly List<Image> highlights = new List<Image>();
        private readonly List<GridInventory.Stack> dragSet = new List<GridInventory.Stack>();
        private GridInventory.Stack dragged;
        private bool draggedRotated;
        private Hand? draggingHand;     // dragging out of a hand
        private Vector2 grabOffset;
        private GridInventory.Stack splitTarget;
        private int splitAmount;

        // selection frame
        private const float MarqueeThreshold = 6f;

        // layout: the slots of what is worn, the gaps between the columns, the room above the grid and below it
        private const float SlotSize = 96f;
        private const float Gap = 24f;
        private const float LabelHeight = 26f;
        private const float SlotSpacing = 12f;
        private const float BottomRoom = 90f;

        private class SlotView
        {
            public RectTransform Label;
            public RectTransform Rect;
            public Image Frame;
            public Image Icon;
        }

        private readonly Dictionary<EquipSlot, SlotView> slotViews = new Dictionary<EquipSlot, SlotView>();

        private class HandView
        {
            public Hand Hand;
            public RectTransform Label;
            public TMP_Text Title;
            public RectTransform Rect;
            public Image Frame;
            public Image Icon;
        }

        private readonly List<HandView> handViews = new List<HandView>();
        private Color titleColor = Color.white;
        private PlayerEquipment equipment;
        private RectTransform cellsRoot;
        private RectTransform handsLabel;
        private Vector2Int laidOut;
        private EquipSlot draggingSlot = EquipSlot.None;
        private RectTransform marqueeRoot;
        private bool marqueeActive;
        private bool marqueeAdditive;
        private bool marqueeMoved;
        private Vector2 marqueeStart;
        private Vector2 marqueeStartScreen;
        private readonly List<GridInventory.Stack> marqueeBase = new List<GridInventory.Stack>();

        public bool IsOpen => window != null && window.gameObject.activeSelf;
        private bool IsGroupDrag => dragSet.Count > 1;
        private bool IsSplitting => splitTarget != null;

        private void OnEnable()
        {
            if (inventory == null) return;
            inventory.Changed += OnInventoryChanged;
            inventory.SelectedStackChanged += OnInventoryChanged;
            inventory.HeldChanged += OnInventoryChanged;
            window.gameObject.SetActive(false);
            splitPanel.gameObject.SetActive(false);
            ghost.gameObject.SetActive(false);
            highlights.Clear();
            highlights.Add(highlight);
            highlight.enabled = false;
            RefreshWeight();
        }

        private void OnDisable()
        {
            if (inventory == null) return;
            inventory.Changed -= OnInventoryChanged;
            inventory.SelectedStackChanged -= OnInventoryChanged;
            inventory.HeldChanged -= OnInventoryChanged;
            if (equipment != null) equipment.Changed -= OnInventoryChanged;
            equipment = null;
        }

        // Found on first use: the player may get the component only when the scene starts.
        private PlayerEquipment Equipment
        {
            get
            {
                if (equipment == null && inventory != null && inventory.TryGetComponent(out equipment)) equipment.Changed += OnInventoryChanged;
                return equipment;
            }
        }

        private void OnInventoryChanged()
        {
            RefreshWeight();
            if (IsOpen) Rebuild();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (ModalState.IsOpen) { if (IsOpen) SetOpen(false); return; } // another window has the keyboard
                if (keyboard[toggleKey].wasPressedThisFrame) SetOpen(!IsOpen);
                else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (IsSplitting) CloseSplit();
                    else SetOpen(false);
                }
            }
            if (!IsOpen) return;

            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();

            if (IsSplitting)
            {
                UpdateSplit(screen, mouse, keyboard);
                return;
            }

            if (marqueeActive)
            {
                UpdateMarquee(screen);
                if (mouse.leftButton.wasReleasedThisFrame) EndMarquee(screen);
                return;
            }

            if (dragged == null)
            {
                UpdateHover(screen);
                bool shift = keyboard != null && keyboard.shiftKey.isPressed;
                bool ctrl = keyboard != null && keyboard.ctrlKey.isPressed;
                if (mouse.leftButton.wasPressedThisFrame) OnPress(screen, shift, ctrl);
                else if (mouse.rightButton.wasPressedThisFrame) OnRightPress(screen);
                else if (keyboard != null && keyboard.fKey.wasPressedThisFrame) UseUnderCursor(screen);
                return;
            }

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Rotate();
            UpdateDrag(screen);
            if (mouse.leftButton.wasReleasedThisFrame) EndDrag(screen);
        }

        private void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            if (!open)
            {
                CancelDrag();
                CloseSplit();
                marqueeActive = false;
                if (marqueeRoot != null) marqueeRoot.gameObject.SetActive(false);
            }
            window.gameObject.SetActive(open);
            if (open) Rebuild();
        }

        // ---- pointer helpers ----

        private bool ToGridLocal(Vector2 screen, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(grid, screen, null, out local);

        private Vector2Int CellOf(Vector2 local) =>
            new Vector2Int(Mathf.FloorToInt(local.x / cellSize), Mathf.FloorToInt(-local.y / cellSize));

        private GridInventory.Stack StackUnder(Vector2 screen)
        {
            if (!Hit(grid, screen)) return null;
            return ToGridLocal(screen, out var local) ? inventory.StackAt(CellOf(local)) : null;
        }

        private static bool Hit(RectTransform rect, Vector2 screen) =>
            RectTransformUtility.RectangleContainsScreenPoint(rect, screen, null);

        // ---- hovering ----

        private void UpdateHover(Vector2 screen)
        {
            var slot = SlotUnder(screen);
            if (slot != EquipSlot.None)
            {
                var worn = Equipment != null ? Equipment[slot] : null;
                infoLabel.text = worn != null
                    ? $"{PlayerEquipment.SlotTitle(slot)}: {worn.DisplayName}{GridNote(worn)}. ПКМ или перетащите в сетку — снять, за окно — выбросить"
                    : $"{PlayerEquipment.SlotTitle(slot)}: пусто. Перетащите сюда вещь или ПКМ по ней в сетке — надеть";
                return;
            }

            var selected = inventory.SelectedStacks;
            var hand = HandUnder(screen);
            if (hand.HasValue)
            {
                infoLabel.text = HandInfo(hand.Value);
                return;
            }

            var stack = StackUnder(screen);
            if (stack != null) infoLabel.text = Describe(stack) + HintFor(stack);
            else if (selected.Count > 1) infoLabel.text = DescribeSelection();
            else infoLabel.text = "Мышь — перетаскивать, R — повернуть, Shift+клик или рамка — выделить несколько, Ctrl+клик — разделить, ПКМ — взять в руку или надеть, Q — сменить руку";
        }

        private static string HintFor(GridInventory.Stack stack)
        {
            string hint = string.Empty;
            if (stack.Item is ConsumableDefinition consumable) hint += $". F — {consumable.Verb.ToLowerInvariant()} (3 с)";
            if (stack.Item.Holdable) hint += stack.Item.TwoHanded ? ". ПКМ — взять в обе руки" : ". ПКМ — взять в руку";
            if (stack.Item.Wearable) hint += $"{GridNote(stack.Item)}. ПКМ — надеть";
            if (stack.Count > 1) hint += ". Ctrl+клик — разделить";
            return hint;
        }

        private string HandInfo(Hand hand)
        {
            var item = inventory.InHand(hand);
            string title = HandTitle(hand) + (hand == inventory.ActiveHand && !inventory.HoldsTwoHanded ? " (активная)" : string.Empty);
            if (item == null) return $"{title}: пусто. Перетащите сюда предмет — взять в эту руку, клик — сделать её активной, Q — сменить руку";
            string both = inventory.HoldsTwoHanded ? ", в обеих руках" : string.Empty;
            return $"{title}: {item.DisplayName}{both}. ПКМ или перетащите в сетку — убрать, на другую руку — переложить, H — убрать всё, G — выбросить";
        }

        private static string HandTitle(Hand hand) => hand == Hand.Right ? "Правая рука" : "Левая рука";

        // A backpack tells how big the grid is with it on.
        private static string GridNote(ItemDefinition item) =>
            item.EquipSlot == EquipSlot.Back ? $" (сетка {item.BackpackGrid.x}×{item.BackpackGrid.y})" : string.Empty;

        private static string Describe(GridInventory.Stack stack)
        {
            var item = stack.Item;
            string count = stack.Count > 1 ? $" ×{stack.Count}" : string.Empty;
            return $"{item.DisplayName}{count} — {item.Weight * stack.Count:0.##} кг ({item.Weight:0.##} кг/шт), {item.GridSize.x}×{item.GridSize.y}";
        }

        private string DescribeSelection()
        {
            var selected = inventory.SelectedStacks;
            float weight = selected.Sum(s => s.Item.Weight * s.Count);
            return $"Выделено: {selected.Count} шт., {weight:0.##} кг. Перетащите группу, G или выход за окно — выбросить";
        }

        // ---- clicking and dragging ----

        private void OnPress(Vector2 screen, bool shift, bool ctrl)
        {
            // Pressing a worn thing drags it out of its slot.
            var slot = SlotUnder(screen);
            if (slot != EquipSlot.None)
            {
                if (!shift && !ctrl && Equipment != null && Equipment[slot] != null)
                {
                    BeginSlotDrag(slot);
                    UpdateDrag(screen);
                }
                return;
            }

            // Pressing a hand makes it the active one, and drags out what it holds.
            var hand = HandUnder(screen);
            if (hand.HasValue)
            {
                if (shift || ctrl) return;
                inventory.SetActiveHand(hand.Value);
                if (inventory.InHand(hand.Value) == null) return;
                BeginHeldDrag(hand.Value);
                UpdateDrag(screen);
                return;
            }

            var stack = StackUnder(screen);
            if (ctrl && stack != null)
            {
                OpenSplit(stack);
                return;
            }

            if (stack == null)
            {
                // Pressing empty cells starts a selection frame; a click without dragging just drops the selection.
                if (Hit(grid, screen) && ToGridLocal(screen, out var start))
                {
                    BeginMarquee(start, screen, shift);
                    return;
                }
                if (!shift && Hit(window, screen)) inventory.ClearSelection();
                return;
            }
            if (shift)
            {
                inventory.ToggleSelection(stack);
                return;
            }
            if (!ToGridLocal(screen, out var local)) return;

            // Pressing a stack that is part of a group drags the whole group; any other stack becomes the only selection.
            if (!inventory.IsSelected(stack)) inventory.SelectStack(stack);
            dragSet.Clear();
            dragSet.AddRange(inventory.SelectedStacks);
            dragged = stack;
            draggedRotated = stack.Rotated;
            draggingHand = null;
            grabOffset = local - TopLeftOf(stack.Origin);

            BuildGhost();
            ghost.gameObject.SetActive(true);
            Rebuild(); // hides the widgets of what is being dragged
            UpdateDrag(screen);
        }

        private void BeginHeldDrag(Hand hand)
        {
            // A stand-in stack (not in the grid) lets the held item share the code of the other drags.
            dragged = new GridInventory.Stack { Item = inventory.InHand(hand), Count = 1 };
            dragSet.Clear();
            dragSet.Add(dragged);
            draggedRotated = false;
            draggingHand = hand;
            grabOffset = FootprintOf(dragged.Item, false) * 0.5f;
            grabOffset.y = -grabOffset.y;

            BuildGhost();
            ghost.gameObject.SetActive(true);
            RefreshHands();
        }

        private void BeginSlotDrag(EquipSlot slot)
        {
            dragged = new GridInventory.Stack { Item = Equipment[slot], Count = 1 };
            dragSet.Clear();
            dragSet.Add(dragged);
            draggedRotated = false;
            draggingHand = null;
            draggingSlot = slot;
            grabOffset = FootprintOf(dragged.Item, false) * 0.5f;
            grabOffset.y = -grabOffset.y;

            BuildGhost();
            ghost.gameObject.SetActive(true);
            RefreshSlots();
        }

        private void OnRightPress(Vector2 screen)
        {
            var slot = SlotUnder(screen);
            if (slot != EquipSlot.None)
            {
                if (Equipment != null) Equipment.TakeOffToGrid(slot);
                return;
            }

            var hand = HandUnder(screen);
            if (hand.HasValue)
            {
                if (inventory.InHand(hand.Value) != null && !inventory.Unhold(hand.Value)) infoLabel.text = "Нет места в инвентаре";
                return;
            }

            var stack = StackUnder(screen);
            if (stack == null) return;
            if (stack.Item.Wearable)
            {
                if (Equipment != null) Equipment.WearFromGrid(stack);
                return;
            }
            if (!stack.Item.Holdable) infoLabel.text = $"{stack.Item.DisplayName} нельзя взять в руки";
            else if (!inventory.Hold(stack)) infoLabel.text = "Некуда убрать то, что в руках";
        }

        // F over food, drink or medicine uses it from the backpack (the progress shows at the bottom of the screen).
        private void UseUnderCursor(Vector2 screen)
        {
            var stack = StackUnder(screen);
            if (stack == null || stack.Sealed || !(stack.Item is ConsumableDefinition consumable)) return;
            if (!inventory.TryGetComponent<PlayerConsumer>(out var consumer)) return;

            if (consumer.TryUseFromInventory(consumable)) infoLabel.text = $"{consumable.Verb}: {consumable.DisplayName}… ({consumer.InventoryUseSeconds:0.#} с)";
            else if (consumer.Message.Length > 0) infoLabel.text = consumer.Message;
        }

        private void Rotate()
        {
            if (IsGroupDrag || !dragged.Item.CanRotate) return;
            draggedRotated = !draggedRotated;
            BuildGhost();
            // Keep the item under the cursor: grab it by its centre after turning.
            grabOffset = FootprintOf(dragged.Item, draggedRotated) * 0.5f;
            grabOffset.y = -grabOffset.y;
        }

        private void UpdateDrag(Vector2 screen)
        {
            if (!ToGridLocal(screen, out var local)) return;

            Vector2 topLeft = local - grabOffset;
            ghost.anchoredPosition = topLeft;

            bool overWindow = Hit(window, screen);
            var overHand = HandUnder(screen);
            var slot = SlotUnder(screen);
            var origin = OriginFor(topLeft);
            ShowHighlights(overWindow && !overHand.HasValue && slot == EquipSlot.None, origin);

            // A hand lights up green when what is dragged would be taken in it.
            bool canHold = overHand.HasValue && CanDropOnHand(overHand.Value);
            foreach (var view in handViews)
            {
                view.Frame.color = overHand != view.Hand || draggingHand == view.Hand ? IdleFor(view.Hand) : canHold ? ValidColor : InvalidColor;
            }
            // A slot of what is worn lights up green for a thing that goes on there, red for any other.
            foreach (var pair in slotViews)
            {
                pair.Value.Frame.color = pair.Key != slot || pair.Key == draggingSlot ? HandsIdle : CanWear(slot) ? ValidColor : InvalidColor;
            }
            if (slot != EquipSlot.None && slot != draggingSlot)
            {
                infoLabel.text = CanWear(slot) ? "Отпустите, чтобы надеть" : $"Сюда надевается только то, что носят: {PlayerEquipment.SlotTitle(slot).ToLowerInvariant()}";
                return;
            }

            if (!overWindow)
            {
                infoLabel.text = IsGroupDrag
                    ? $"Отпустите, чтобы выбросить выделенное ({dragSet.Count} шт.)"
                    : $"Отпустите, чтобы выбросить: {Describe(dragged)}";
            }
            else if (overHand.HasValue)
            {
                infoLabel.text = !canHold ? (draggingHand == overHand ? Describe(dragged) : "Сюда кладётся только предмет, который берётся в руки")
                    : draggingHand.HasValue ? "Отпустите, чтобы переложить в другую руку"
                    : dragged.Item.TwoHanded ? "Отпустите, чтобы взять в обе руки"
                    : $"Отпустите, чтобы взять: {HandTitle(overHand.Value).ToLowerInvariant()}";
            }
            else infoLabel.text = IsGroupDrag ? DescribeSelection() : Describe(dragged);
        }

        private void ShowHighlights(bool show, Vector2Int origin)
        {
            int used = 0;
            if (show && dragged != null)
            {
                if (IsGroupDrag)
                {
                    var delta = origin - dragged.Origin;
                    var color = inventory.CanMoveGroup(dragSet, delta) ? ValidColor : InvalidColor;
                    foreach (var stack in dragSet)
                    {
                        PlaceHighlight(used++, stack.Origin + delta, stack.Size, color);
                    }
                }
                else
                {
                    var kind = draggingHand.HasValue ? inventory.EvaluateHeld(draggingHand.Value, origin, draggedRotated)
                        : draggingSlot != EquipSlot.None ? inventory.EvaluateItem(dragged.Item, origin, draggedRotated)
                        : inventory.Evaluate(dragged, origin, draggedRotated);
                    var color = kind == GridInventory.MoveKind.Move ? ValidColor
                        : kind == GridInventory.MoveKind.Merge ? MergeColor : InvalidColor;
                    var size = FootprintOf(dragged.Item, draggedRotated) / cellSize;
                    PlaceHighlight(used++, origin, new Vector2Int(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y)), color);
                }
            }
            for (int i = used; i < highlights.Count; i++) highlights[i].enabled = false;
        }

        private void PlaceHighlight(int index, Vector2Int origin, Vector2Int size, Color color)
        {
            while (highlights.Count <= index)
            {
                var copy = Instantiate(highlight.gameObject, highlight.transform.parent);
                copy.name = "Highlight";
                highlights.Add(copy.GetComponent<Image>());
            }
            var image = highlights[index];
            image.enabled = true;
            image.color = color;
            image.rectTransform.sizeDelta = new Vector2(size.x, size.y) * cellSize;
            image.rectTransform.anchoredPosition = TopLeftOf(origin);
        }

        private void EndDrag(Vector2 screen)
        {
            var group = new List<GridInventory.Stack>(dragSet);
            var stack = dragged;
            bool rotated = draggedRotated;
            bool wasGroup = IsGroupDrag;
            var fromHand = draggingHand;
            var fromSlot = draggingSlot;
            bool overWindow = Hit(window, screen);
            var overHand = HandUnder(screen);
            bool canHold = overHand.HasValue && CanDropOnHand(overHand.Value);
            var slot = SlotUnder(screen);
            bool canWear = slot != EquipSlot.None && CanWear(slot);
            CancelDrag();

            if (fromSlot != EquipSlot.None)
            {
                EndSlotDrag(fromSlot, screen, rotated, overWindow, overHand.HasValue || slot != EquipSlot.None);
                return;
            }
            if (fromHand.HasValue)
            {
                if (slot == EquipSlot.None) EndHeldDrag(fromHand.Value, screen, rotated, overWindow, overHand, canHold);
                return;
            }
            if (!overWindow)
            {
                Throw(group);
                return;
            }
            if (slot != EquipSlot.None)
            {
                if (canWear && Equipment != null) Equipment.WearFromGrid(stack);
                return;
            }
            if (overHand.HasValue)
            {
                if (canHold && !inventory.Hold(stack, overHand.Value)) infoLabel.text = "Некуда убрать то, что в руках";
                return;
            }
            if (!ToGridLocal(screen, out var local)) return;

            var origin = OriginFor(local - grabOffset);
            if (wasGroup)
            {
                var delta = origin - stack.Origin;
                // Letting go on the spot (a plain click) leaves just the stack that was pressed selected.
                if (delta == Vector2Int.zero) inventory.SelectStack(stack);
                else inventory.TryMoveGroup(group, delta);
            }
            else inventory.TryMove(stack, origin, rotated);
            Rebuild();
        }

        private void EndSlotDrag(EquipSlot slot, Vector2 screen, bool rotated, bool overWindow, bool overAnotherSlot)
        {
            if (Equipment == null || Equipment[slot] == null) return;
            if (!overWindow)
            {
                Equipment.TakeOffToFloor(slot);
                return;
            }
            // Let go on a slot (its own one included): it stays on.
            if (overAnotherSlot) return;

            Vector2Int? origin = null;
            if (Hit(grid, screen) && ToGridLocal(screen, out var local)) origin = OriginFor(local - grabOffset);
            Equipment.TakeOffToGrid(slot, origin, rotated);
        }

        private void EndHeldDrag(Hand hand, Vector2 screen, bool rotated, bool overWindow, Hand? overHand, bool canHold)
        {
            var item = inventory.InHand(hand);
            if (item == null) return;
            if (overHand.HasValue)
            {
                // Onto the other hand: the hands trade what they hold.
                if (canHold) inventory.SwapHands();
                return;
            }

            if (!overWindow)
            {
                if (item.WorldPrefab != null && inventory.TakeHeld(hand, out var thrown)) interactor.DropToWorld(thrown, 1);
                return;
            }
            if (ToGridLocal(screen, out var local)) inventory.PlaceHeld(hand, OriginFor(local - grabOffset), rotated);
        }

        private void Throw(List<GridInventory.Stack> group)
        {
            int index = 0;
            foreach (var stack in group)
            {
                var item = stack.Item;
                int count = stack.Count;
                if (item.WorldPrefab == null || !inventory.Remove(stack)) continue;

                // A small ring keeps several thrown stacks from lying exactly on top of each other.
                float angle = index * Mathf.PI * 2f / group.Count;
                var spread = group.Count > 1 ? new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.35f : Vector2.zero;
                interactor.DropToWorld(item, count, spread);
                index++;
            }
        }

        private void CancelDrag()
        {
            if (dragged == null) return;
            dragged = null;
            draggingHand = null;
            draggingSlot = EquipSlot.None;
            dragSet.Clear();
            ghost.gameObject.SetActive(false);
            foreach (var view in handViews) view.Frame.color = IdleFor(view.Hand);
            foreach (var view in slotViews.Values) view.Frame.color = HandsIdle;
            ShowHighlights(false, Vector2Int.zero);
            Rebuild();
        }

        // ---- selection frame ----

        private void BeginMarquee(Vector2 startLocal, Vector2 startScreen, bool additive)
        {
            EnsureMarquee();
            marqueeActive = true;
            marqueeMoved = false;
            marqueeAdditive = additive;
            marqueeStart = startLocal;
            marqueeStartScreen = startScreen;
            marqueeBase.Clear();
            if (additive) marqueeBase.AddRange(inventory.SelectedStacks);
        }

        private void UpdateMarquee(Vector2 screen)
        {
            if (!ToGridLocal(screen, out var current)) return;
            if (!marqueeMoved && (screen - marqueeStartScreen).sqrMagnitude < MarqueeThreshold * MarqueeThreshold) return;
            marqueeMoved = true;

            // The frame cannot leave the grid.
            var bounds = grid.rect;
            Vector2 a = ClampToGrid(marqueeStart, bounds), b = ClampToGrid(current, bounds);
            Vector2 min = Vector2.Min(a, b), max = Vector2.Max(a, b);

            marqueeRoot.gameObject.SetActive(true);
            marqueeRoot.anchoredPosition = new Vector2(min.x, max.y);
            marqueeRoot.sizeDelta = max - min;

            // Everything the frame touches is selected right away, so the result is visible while dragging.
            var hit = new List<GridInventory.Stack>(marqueeBase);
            foreach (var stack in inventory.Stacks)
            {
                if (hit.Contains(stack) || !Overlaps(stack, min, max)) continue;
                hit.Add(stack);
            }
            inventory.SetSelection(hit);
            infoLabel.text = hit.Count > 0 ? $"Выделено рамкой: {hit.Count}" : "Тяните рамку, чтобы выделить предметы";
        }

        private void EndMarquee(Vector2 screen)
        {
            if (!marqueeMoved && !marqueeAdditive) inventory.ClearSelection(); // a plain click on empty cells
            marqueeActive = false;
            marqueeMoved = false;
            marqueeBase.Clear();
            marqueeRoot.gameObject.SetActive(false);
        }

        private Vector2 ClampToGrid(Vector2 local, Rect bounds)
        {
            // The grid has its pivot in the top-left corner: x grows to the right, y is negative downwards.
            return new Vector2(Mathf.Clamp(local.x, 0f, bounds.width), Mathf.Clamp(local.y, -bounds.height, 0f));
        }

        private bool Overlaps(GridInventory.Stack stack, Vector2 min, Vector2 max)
        {
            var size = stack.Size;
            float left = stack.Origin.x * cellSize, right = (stack.Origin.x + size.x) * cellSize;
            float top = -stack.Origin.y * cellSize, bottom = -(stack.Origin.y + size.y) * cellSize;
            return left < max.x && right > min.x && bottom < max.y && top > min.y;
        }

        private void EnsureMarquee()
        {
            if (marqueeRoot != null) return;

            marqueeRoot = NewRect("SelectionFrame", grid);
            marqueeRoot.anchorMin = marqueeRoot.anchorMax = marqueeRoot.pivot = new Vector2(0f, 1f);

            var fill = NewRect("Fill", marqueeRoot).gameObject.AddComponent<Image>();
            Stretch(fill.rectTransform, 0f);
            fill.color = new Color(0.35f, 0.7f, 1f, 0.16f);
            fill.raycastTarget = false;

            // Four thin lines make the border.
            var edge = new Color(0.55f, 0.82f, 1f, 0.95f);
            AddEdge(marqueeRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 2f), edge);   // top
            AddEdge(marqueeRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f), edge);   // bottom
            AddEdge(marqueeRoot, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2f, 0f), edge);   // left
            AddEdge(marqueeRoot, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(2f, 0f), edge);   // right
            marqueeRoot.gameObject.SetActive(false);
        }

        private static void AddEdge(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 thickness, Color color)
        {
            var rect = NewRect("Edge", parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2((anchorMin.x + anchorMax.x) * 0.5f, (anchorMin.y + anchorMax.y) * 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = thickness;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        // ---- splitting a stack ----

        private void OpenSplit(GridInventory.Stack stack)
        {
            if (stack.Count < 2)
            {
                infoLabel.text = "В этой стопке один предмет, делить нечего";
                return;
            }
            if (!inventory.HasFreeSpotFor(stack.Item))
            {
                infoLabel.text = "Нет свободного места для новой стопки";
                return;
            }

            splitTarget = stack;
            splitAmount = Mathf.Max(1, stack.Count / 2);
            inventory.SelectStack(stack);
            splitPanel.gameObject.SetActive(true);
            splitPanel.transform.SetAsLastSibling();
            RefreshSplit();
        }

        private void CloseSplit()
        {
            splitTarget = null;
            if (splitPanel != null) splitPanel.gameObject.SetActive(false);
        }

        private void ChangeSplit(int step)
        {
            splitAmount = Mathf.Clamp(splitAmount + step, 1, splitTarget.Count - 1);
            RefreshSplit();
        }

        private void RefreshSplit()
        {
            splitValue.text = splitAmount.ToString();
            infoLabel.text = $"Отделить {splitAmount} из {splitTarget.Count} (останется {splitTarget.Count - splitAmount}). Колесо мыши или +/− — количество, Enter — подтвердить";
        }

        private void ConfirmSplit()
        {
            var target = splitTarget;
            int amount = splitAmount;
            CloseSplit();
            if (inventory.Split(target, amount) == null) infoLabel.text = "Не удалось разделить: нет места";
        }

        private void UpdateSplit(Vector2 screen, Mouse mouse, Keyboard keyboard)
        {
            // The stack may have been changed or removed while the panel was open.
            if (!inventory.Stacks.Contains(splitTarget) || splitTarget.Count < 2)
            {
                CloseSplit();
                return;
            }
            if (splitAmount > splitTarget.Count - 1) ChangeSplit(0);

            float wheel = mouse.scroll.ReadValue().y;
            if (wheel > 0.1f) ChangeSplit(1);
            else if (wheel < -0.1f) ChangeSplit(-1);

            if (keyboard != null)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    ConfirmSplit();
                    return;
                }
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) ChangeSplit(1);
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) ChangeSplit(-1);
            }

            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (Hit(splitPlus, screen)) ChangeSplit(1);
            else if (Hit(splitMinus, screen)) ChangeSplit(-1);
            else if (Hit(splitOk, screen)) ConfirmSplit();
            else if (Hit(splitCancel, screen)) CloseSplit();
        }

        // ---- drawing ----

        private void BuildGhost()
        {
            for (int i = ghost.childCount - 1; i >= 0; i--) Destroy(ghost.GetChild(i).gameObject);
            ghost.sizeDelta = FootprintOf(dragged.Item, draggedRotated);
            if (!ghost.TryGetComponent<CanvasGroup>(out var canvasGroup)) canvasGroup = ghost.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0.85f;
            canvasGroup.blocksRaycasts = false;

            foreach (var stack in dragSet)
            {
                // Relative to the stack the cursor holds, so the whole group moves as one.
                var offset = TopLeftOf(stack.Origin) - TopLeftOf(dragged.Origin);
                bool rotated = stack == dragged ? draggedRotated : stack.Rotated;
                CreateWidget(ghost, stack, rotated, offset);
            }
        }

        private void Rebuild()
        {
            Layout();
            foreach (var widget in widgets) Destroy(widget.gameObject);
            widgets.Clear();

            foreach (var stack in inventory.Stacks)
            {
                if (dragSet.Contains(stack)) continue;
                widgets.Add(CreateWidget(itemsLayer, stack, stack.Rotated, TopLeftOf(stack.Origin)));
            }
            foreach (var image in highlights) image.transform.SetAsLastSibling();
            if (marqueeRoot != null) marqueeRoot.SetAsLastSibling();
            ghost.transform.SetAsLastSibling();
            if (IsSplitting) splitPanel.transform.SetAsLastSibling();
            RefreshWeight();
            RefreshHands();
            RefreshSlots();
        }

        private void RefreshSlots()
        {
            foreach (var pair in slotViews)
            {
                if (pair.Value.Icon == null) continue;
                var worn = Equipment != null ? Equipment[pair.Key] : null;
                bool shown = worn != null && draggingSlot != pair.Key;
                pair.Value.Icon.enabled = shown;
                pair.Value.Icon.sprite = shown ? worn.Icon : null;
            }
        }

        private EquipSlot SlotUnder(Vector2 screen)
        {
            foreach (var pair in slotViews)
            {
                if (Hit(pair.Value.Rect, screen)) return pair.Key;
            }
            return EquipSlot.None;
        }

        // What is being dragged out of the grid goes on in that slot.
        private bool CanWear(EquipSlot slot) =>
            dragged != null && !IsGroupDrag && !draggingHand.HasValue && draggingSlot == EquipSlot.None && dragged.Item.EquipSlot == slot;

        private Hand? HandUnder(Vector2 screen)
        {
            foreach (var view in handViews)
            {
                if (Hit(view.Rect, screen)) return view.Hand;
            }
            return null;
        }

        // What is dragged can be taken in that hand: a holdable thing out of the grid, or the other hand's thing.
        private bool CanDropOnHand(Hand hand)
        {
            if (dragged == null || IsGroupDrag || draggingSlot != EquipSlot.None || !dragged.Item.Holdable) return false;
            return !draggingHand.HasValue || (draggingHand.Value != hand && !inventory.HoldsTwoHanded);
        }

        private Color IdleFor(Hand hand) => hand == inventory.ActiveHand && !inventory.HoldsTwoHanded ? ActiveHandIdle : HandsIdle;

        /// <summary>
        /// Sizes the grid and the window for the grid the inventory has now (the backpack decides it) and puts the
        /// slots of what is worn in a column next to the hands. Does nothing while the size stays the same.
        /// </summary>
        private void Layout()
        {
            var size = inventory.GridSize;
            if (size == laidOut && slotViews.Count > 0) return;
            laidOut = size;

            if (cellsRoot == null) cellsRoot = grid.Find("Cells") as RectTransform;
            if (handsLabel == null) handsLabel = window.Find("HandsLabel") as RectTransform;

            grid.sizeDelta = new Vector2(size.x, size.y) * cellSize;
            if (cellsRoot != null && cellsRoot.childCount > 0)
            {
                // The cells of the scene are the template: as many as the grid has, the rest hidden.
                var template = cellsRoot.GetChild(0);
                for (int i = cellsRoot.childCount; i < size.x * size.y; i++) Instantiate(template, cellsRoot);
                for (int i = 0; i < cellsRoot.childCount; i++)
                {
                    var cell = (RectTransform)cellsRoot.GetChild(i);
                    bool used = i < size.x * size.y;
                    cell.gameObject.SetActive(used);
                    if (!used) continue;
                    int x = i % size.x, y = i / size.x;
                    cell.name = $"Cell {x},{y}";
                    cell.anchoredPosition = new Vector2(x * cellSize + 1f, -(y * cellSize + 1f));
                }
            }

            // The hands right of the grid, what is worn right of the hands.
            float column = grid.anchoredPosition.x + size.x * cellSize + Gap;
            float top = handsLabel != null ? handsLabel.anchoredPosition.y : handsSlot.anchoredPosition.y + LabelHeight;
            if (handsLabel != null) handsLabel.anchoredPosition = new Vector2(column, top);
            handsSlot.anchoredPosition = new Vector2(column, top - LabelHeight);

            BuildHands();
            BuildSlots();
            // The left hand under the right one.
            float leftTop = top - LabelHeight - handsSlot.sizeDelta.y - SlotSpacing;
            var left = handViews[1];
            if (left.Label != left.Rect) left.Label.anchoredPosition = new Vector2(column, leftTop);
            left.Rect.anchoredPosition = new Vector2(column, leftTop - LabelHeight);

            float slotsX = column + handsSlot.sizeDelta.x + Gap;
            float y0 = top;
            float lowest = -left.Rect.anchoredPosition.y + handsSlot.sizeDelta.y;
            foreach (var slot in PlayerEquipment.Slots)
            {
                var view = slotViews[slot];
                view.Label.anchoredPosition = new Vector2(slotsX, y0);
                view.Rect.anchoredPosition = new Vector2(slotsX, y0 - LabelHeight);
                lowest = Mathf.Max(lowest, -(y0 - LabelHeight) + SlotSize);
                y0 -= LabelHeight + SlotSize + SlotSpacing;
            }

            float gridBottom = -grid.anchoredPosition.y + size.y * cellSize;
            window.sizeDelta = new Vector2(slotsX + SlotSize + Gap, Mathf.Max(gridBottom, lowest) + BottomRoom);
            splitPanel.anchoredPosition = grid.anchoredPosition + new Vector2(size.x * cellSize * 0.5f, -size.y * cellSize * 0.5f);
        }

        // The right hand is the slot of the scene, the left one a copy of it with its own label. Made once.
        private void BuildHands()
        {
            if (handViews.Count > 0) return;

            TMP_Text rightTitle = null;
            if (handsLabel != null && handsLabel.TryGetComponent(out rightTitle))
            {
                titleColor = rightTitle.color;
                rightTitle.text = HandTitle(Hand.Right);
            }
            handViews.Add(new HandView { Hand = Hand.Right, Label = handsLabel != null ? handsLabel : handsSlot, Title = rightTitle, Rect = handsSlot, Frame = handsFrame, Icon = handsIcon });

            RectTransform label = null;
            TMP_Text leftTitle = null;
            if (handsLabel != null)
            {
                label = Instantiate(handsLabel, window);
                label.name = "LeftHandLabel";
                label.SetSiblingIndex(grid.GetSiblingIndex());
                if (label.TryGetComponent(out leftTitle)) leftTitle.text = HandTitle(Hand.Left);
            }
            var rect = Instantiate(handsSlot, window);
            rect.name = "LeftHandSlot";
            rect.SetSiblingIndex(grid.GetSiblingIndex());
            handViews.Add(new HandView
            {
                Hand = Hand.Left, Label = label != null ? label : rect, Title = leftTitle, Rect = rect,
                Frame = Counterpart(handsFrame, handsSlot, rect), Icon = Counterpart(handsIcon, handsSlot, rect),
            });
        }

        // The image of a copy that stands where the given image stands in the original.
        private static Image Counterpart(Image image, RectTransform original, RectTransform copy)
        {
            if (image == null) return copy.GetComponent<Image>();
            var path = new List<string>();
            for (var t = image.transform; t != null && t != original; t = t.parent) path.Insert(0, t.name);
            var found = path.Count == 0 ? copy : copy.Find(string.Join("/", path));
            return found != null ? found.GetComponent<Image>() : copy.GetComponent<Image>();
        }

        // Copies of the hands slot and its label, one for every slot of what is worn. Made once.
        private void BuildSlots()
        {
            if (slotViews.Count > 0) return;
            foreach (var slot in PlayerEquipment.Slots)
            {
                RectTransform label = null;
                if (handsLabel != null)
                {
                    label = Instantiate(handsLabel, window);
                    label.name = $"{slot}Label";
                    label.sizeDelta = new Vector2(SlotSize, label.sizeDelta.y);
                    if (label.TryGetComponent<TMP_Text>(out var text)) text.text = PlayerEquipment.SlotTitle(slot);
                    // Before the grid, so that what is dragged over them is drawn on top.
                    label.SetSiblingIndex(grid.GetSiblingIndex());
                }

                var rect = Instantiate(handsSlot, window);
                rect.name = $"{slot}Slot";
                rect.sizeDelta = new Vector2(SlotSize, SlotSize);
                rect.SetSiblingIndex(grid.GetSiblingIndex());
                var icon = rect.Find("Icon") != null ? rect.Find("Icon").GetComponent<Image>() : null;
                if (icon != null)
                {
                    icon.rectTransform.sizeDelta = Vector2.one * (SlotSize - 16f);
                    icon.preserveAspect = true;
                    icon.enabled = false;
                }
                var frame = rect.GetComponent<Image>();
                frame.color = HandsIdle;
                slotViews[slot] = new SlotView { Label = label != null ? label : rect, Rect = rect, Frame = frame, Icon = icon };
            }
        }

        private void RefreshHands()
        {
            bool both = inventory.HoldsTwoHanded;
            foreach (var view in handViews)
            {
                var item = inventory.InHand(view.Hand);
                // What is dragged out of a hand leaves it; a two-handed thing leaves both.
                bool dragging = draggingHand.HasValue && (draggingHand.Value == view.Hand || both);
                if (view.Icon != null)
                {
                    view.Icon.enabled = item != null && !dragging;
                    view.Icon.sprite = item != null ? item.Icon : null;
                    // A two-handed thing shows faded in the left hand: it is the same thing.
                    view.Icon.color = both && view.Hand == Hand.Left ? SecondHandIcon : Color.white;
                }
                if (view.Title != null) view.Title.color = view.Hand == inventory.ActiveHand && !both ? SelectedColor : titleColor;
                if (dragged == null && view.Frame != null) view.Frame.color = IdleFor(view.Hand);
            }
        }

        private RectTransform CreateWidget(Transform parent, GridInventory.Stack stack, bool rotated, Vector2 topLeft)
        {
            var item = stack.Item;
            bool selected = inventory.IsSelected(stack);

            var root = NewRect("Stack", parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = topLeft;
            root.sizeDelta = FootprintOf(item, rotated);

            var frame = NewRect("Frame", root).gameObject.AddComponent<Image>();
            Stretch(frame.rectTransform, 0f);
            frame.color = selected ? SelectedColor : new Color(0f, 0f, 0f, 0.6f);
            frame.raycastTarget = false;

            var back = NewRect("Background", root).gameObject.AddComponent<Image>();
            Stretch(back.rectTransform, selected ? 3f : 2f);
            back.color = ItemColor;
            back.raycastTarget = false;

            var icon = NewRect("Icon", root).gameObject.AddComponent<Image>();
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(item.GridSize.x, item.GridSize.y) * cellSize - Vector2.one * 12f;
            icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotated ? -90f : 0f);
            icon.sprite = item.Icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            if (stack.Count > 1)
            {
                var count = NewRect("Count", root).gameObject.AddComponent<TextMeshProUGUI>();
                Stretch(count.rectTransform, 6f);
                count.text = stack.Count.ToString();
                count.fontSize = 22f;
                count.alignment = TextAlignmentOptions.BottomRight;
                count.color = Color.white;
                count.raycastTarget = false;
                count.textWrappingMode = TextWrappingModes.NoWrap;
            }
            return root;
        }

        private Vector2Int OriginFor(Vector2 topLeft) =>
            new Vector2Int(Mathf.RoundToInt(topLeft.x / cellSize), Mathf.RoundToInt(-topLeft.y / cellSize));

        private Vector2 TopLeftOf(Vector2Int cell) => new Vector2(cell.x * cellSize, -cell.y * cellSize);

        private Vector2 FootprintOf(ItemDefinition item, bool rotated)
        {
            var size = item.GridSize;
            return (rotated ? new Vector2(size.y, size.x) : new Vector2(size.x, size.y)) * cellSize;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private void RefreshWeight()
        {
            string text = $"Вес {inventory.TotalWeight:0.#} / {inventory.MaxWeight:0.#} кг";
            var color = inventory.IsOverloaded ? OverloadedColor : Color.white;
            if (weightLabel != null) { weightLabel.text = text; weightLabel.color = color; }
            if (weightHud != null) { weightHud.text = text; weightHud.color = color; }
        }
    }
}
