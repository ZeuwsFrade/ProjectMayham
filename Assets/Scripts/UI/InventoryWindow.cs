using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Interaction;
using ProjectMayham.Items;
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
    /// group is moved or thrown away together, and G drops it as well. Esc closes the window. Also keeps the
    /// always-visible weight label up to date. The pointer and keys are read directly from the devices, so no
    /// EventSystem is needed.
    /// </summary>
    public class InventoryWindow : MonoBehaviour
    {
        private static readonly Color ItemColor = new Color(0.17f, 0.21f, 0.25f, 0.96f);
        private static readonly Color SelectedColor = new Color(1f, 0.82f, 0.35f, 1f);
        private static readonly Color ValidColor = new Color(0.3f, 0.9f, 0.4f, 0.4f);
        private static readonly Color MergeColor = new Color(0.35f, 0.7f, 1f, 0.45f);
        private static readonly Color InvalidColor = new Color(0.95f, 0.3f, 0.3f, 0.4f);
        private static readonly Color OverloadedColor = new Color(1f, 0.7f, 0.25f, 1f);

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

        [Header("Settings")]
        [SerializeField] private float cellSize = 64f;
        [SerializeField] private Key toggleKey = Key.I;

        private readonly List<RectTransform> widgets = new List<RectTransform>();
        private readonly List<Image> highlights = new List<Image>();
        private readonly List<GridInventory.Stack> dragSet = new List<GridInventory.Stack>();
        private GridInventory.Stack dragged;
        private bool draggedRotated;
        private Vector2 grabOffset;

        public bool IsOpen => window != null && window.gameObject.activeSelf;
        private bool IsGroupDrag => dragSet.Count > 1;

        private void OnEnable()
        {
            if (inventory == null) return;
            inventory.Changed += OnInventoryChanged;
            inventory.SelectedStackChanged += OnInventoryChanged;
            window.gameObject.SetActive(false);
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
                if (keyboard[toggleKey].wasPressedThisFrame) SetOpen(!IsOpen);
                else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame) SetOpen(false);
            }
            if (!IsOpen) return;

            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();

            if (dragged == null)
            {
                UpdateHover(screen);
                if (mouse.leftButton.wasPressedThisFrame) OnPress(screen, keyboard != null && keyboard.shiftKey.isPressed);
                return;
            }

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Rotate();
            UpdateDrag(screen);
            if (mouse.leftButton.wasReleasedThisFrame) EndDrag(screen);
        }

        private void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            if (!open) CancelDrag();
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
            if (!RectTransformUtility.RectangleContainsScreenPoint(grid, screen, null)) return null;
            return ToGridLocal(screen, out var local) ? inventory.StackAt(CellOf(local)) : null;
        }

        // ---- hovering ----

        private void UpdateHover(Vector2 screen)
        {
            var selected = inventory.SelectedStacks;
            var stack = StackUnder(screen);
            if (stack != null) infoLabel.text = Describe(stack);
            else if (selected.Count > 1) infoLabel.text = DescribeSelection();
            else infoLabel.text = "Перетаскивайте предметы мышью, R — повернуть, Shift+клик — выделить несколько, за пределы окна — выбросить";
        }

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

        private void OnPress(Vector2 screen, bool shift)
        {
            var stack = StackUnder(screen);
            if (shift)
            {
                inventory.ToggleSelection(stack);
                return;
            }

            if (stack == null)
            {
                // A click on empty window space drops the selection.
                if (RectTransformUtility.RectangleContainsScreenPoint(window, screen, null)) inventory.ClearSelection();
                return;
            }
            if (!ToGridLocal(screen, out var local)) return;

            // Pressing a stack that is part of a group drags the whole group; any other stack becomes the only selection.
            if (!inventory.IsSelected(stack)) inventory.SelectStack(stack);
            dragSet.Clear();
            dragSet.AddRange(inventory.SelectedStacks);
            dragged = stack;
            draggedRotated = stack.Rotated;
            grabOffset = local - TopLeftOf(stack.Origin);

            BuildGhost();
            ghost.gameObject.SetActive(true);
            Rebuild(); // hides the widgets of what is being dragged
            UpdateDrag(screen);
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

            bool overWindow = RectTransformUtility.RectangleContainsScreenPoint(window, screen, null);
            var origin = OriginFor(topLeft);
            ShowHighlights(overWindow, origin);

            if (!overWindow)
            {
                infoLabel.text = IsGroupDrag
                    ? $"Отпустите, чтобы выбросить выделенное ({dragSet.Count} шт.)"
                    : $"Отпустите, чтобы выбросить: {Describe(dragged)}";
            }
            else infoLabel.text = IsGroupDrag ? DescribeSelection() : Describe(dragged);
        }

        private void ShowHighlights(bool overWindow, Vector2Int origin)
        {
            int used = 0;
            if (overWindow)
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
                    var kind = inventory.Evaluate(dragged, origin, draggedRotated);
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
            bool overWindow = RectTransformUtility.RectangleContainsScreenPoint(window, screen, null);
            CancelDrag();

            if (!overWindow)
            {
                Throw(group);
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
            dragSet.Clear();
            ghost.gameObject.SetActive(false);
            ShowHighlights(false, Vector2Int.zero);
            Rebuild();
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
                CreateWidget(ghost, stack, rotated, offset, true);
            }
        }

        private void Rebuild()
        {
            foreach (var widget in widgets) Destroy(widget.gameObject);
            widgets.Clear();

            foreach (var stack in inventory.Stacks)
            {
                if (dragSet.Contains(stack)) continue;
                widgets.Add(CreateWidget(itemsLayer, stack, stack.Rotated, TopLeftOf(stack.Origin), false));
            }
            foreach (var image in highlights) image.transform.SetAsLastSibling();
            ghost.transform.SetAsLastSibling();
            RefreshWeight();
        }

        private RectTransform CreateWidget(Transform parent, GridInventory.Stack stack, bool rotated, Vector2 topLeft, bool asGhost)
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
