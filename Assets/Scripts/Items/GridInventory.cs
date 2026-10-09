using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Core;
using UnityEngine;

namespace ProjectMayham.Items
{
    /// <summary>
    /// Cell-based inventory: every stack of items occupies a rectangle of cells (<see cref="ItemDefinition.GridSize"/>,
    /// optionally turned by 90 degrees) in a fixed grid. New items go to the first free spot; stacks of the same item
    /// merge. Replaces the slot logic of <see cref="Inventory"/> but keeps its weight rules, so the player, stats and
    /// pickups work with it unchanged. The slot/hotbar members of the base class are empty here.
    /// </summary>
    public class GridInventory : Inventory
    {
        /// <summary>What dropping a stack at a given place would do.</summary>
        public enum MoveKind { Invalid, Move, Merge }

        /// <summary>One stack of items lying in the grid.</summary>
        public class Stack
        {
            public ItemDefinition Item { get; internal set; }
            public int Count { get; internal set; }
            /// <summary>Top-left cell, with (0, 0) at the top-left corner of the grid.</summary>
            public Vector2Int Origin { get; internal set; }
            public bool Rotated { get; internal set; }
            /// <summary>Cells covered, taking the rotation into account.</summary>
            public Vector2Int Size => Oriented(Item, Rotated);
        }

        [Header("Grid")]
        [Tooltip("Columns and rows of the grid. Changing it at runtime is not supported.")]
        [SerializeField] private Vector2Int gridSize = new Vector2Int(8, 5);

        private readonly List<Stack> stacks = new List<Stack>();
        private Stack[,] cells;
        private readonly List<Stack> selection = new List<Stack>();
        private float weight;
        private ItemDefinition held;

        /// <summary>Raised when the item in the hands changes.</summary>
        public event Action HeldChanged;

        /// <summary>Raised whenever the set of selected stacks changes.</summary>
        public event Action SelectedStackChanged;

        public Vector2Int GridSize => new Vector2Int(Cells.GetLength(0), Cells.GetLength(1));
        public IReadOnlyList<Stack> Stacks => stacks;
        /// <summary>The selected stack that was added to the selection last, or null.</summary>
        public Stack SelectedStack => selection.Count > 0 ? selection[selection.Count - 1] : null;
        /// <summary>Everything G drops: the stacks selected in the window (Shift+click adds more),
        /// or just the stack picked up last.</summary>
        public IReadOnlyList<Stack> SelectedStacks => selection;
        public bool IsSelected(Stack stack) => selection.Contains(stack);

        /// <summary>The item in the hands (one piece, outside of the grid), or null.</summary>
        public ItemDefinition HeldItem => held;

        // What is carried in the hands weighs as much as what is in the grid.
        public override float TotalWeight => weight + (held != null ? held.Weight : 0f);
        // The slot members of Inventory do not apply to a grid.
        public override int SlotCount => 0;
        public override int Selected => -1;
        public override Slot this[int index] => default;
        public override void Select(int index) { }
        public override void Cycle(int step) { }

        private Stack[,] Cells => cells ??= new Stack[Mathf.Max(1, gridSize.x), Mathf.Max(1, gridSize.y)];

        private static Vector2Int Oriented(ItemDefinition item, bool rotated)
        {
            var size = item.GridSize;
            return rotated ? new Vector2Int(size.y, size.x) : size;
        }

        /// <summary>The stack covering a cell, or null.</summary>
        public Stack StackAt(Vector2Int cell)
        {
            var grid = GridSize;
            if (cell.x < 0 || cell.y < 0 || cell.x >= grid.x || cell.y >= grid.y) return null;
            return Cells[cell.x, cell.y];
        }

        /// <summary>Makes this stack the only selected one (null clears the selection).</summary>
        public void SelectStack(Stack stack)
        {
            if (stack == null) { ClearSelection(); return; }
            if (selection.Count == 1 && selection[0] == stack) return;
            selection.Clear();
            selection.Add(stack);
            SelectedStackChanged?.Invoke();
        }

        /// <summary>Replaces the selection with these stacks (nothing happens when it is the same set).</summary>
        public void SetSelection(IReadOnlyCollection<Stack> chosen)
        {
            bool same = chosen.Count == selection.Count && chosen.All(selection.Contains);
            if (same) return;

            selection.Clear();
            foreach (var stack in chosen)
            {
                if (stacks.Contains(stack) && !selection.Contains(stack)) selection.Add(stack);
            }
            SelectedStackChanged?.Invoke();
        }

        /// <summary>Adds the stack to the selection, or removes it when it is already selected.</summary>
        public void ToggleSelection(Stack stack)
        {
            if (stack == null || !stacks.Contains(stack)) return;
            if (!selection.Remove(stack)) selection.Add(stack);
            SelectedStackChanged?.Invoke();
        }

        public void ClearSelection()
        {
            if (selection.Count == 0) return;
            selection.Clear();
            SelectedStackChanged?.Invoke();
        }

        public override bool HasRoomFor(ItemDefinition item)
        {
            if (item == null) return false;
            foreach (var stack in stacks)
            {
                if (stack.Item == item && stack.Count < item.MaxStack) return true;
            }
            return FindSpot(item, out _, out _);
        }

        /// <summary>Fills existing stacks first, then free spots. Returns how many items did not fit.</summary>
        public override int Add(ItemDefinition item, int count)
        {
            if (item == null || count <= 0) return count;

            int accepted = Mathf.Min(count, CountByWeight(item));
            int left = accepted;
            Stack last = null;

            foreach (var stack in stacks)
            {
                if (left <= 0) break;
                if (stack.Item != item || stack.Count >= item.MaxStack) continue;
                int moved = Mathf.Min(left, item.MaxStack - stack.Count);
                stack.Count += moved;
                left -= moved;
                last = stack;
            }
            while (left > 0 && FindSpot(item, out var origin, out bool rotated))
            {
                int moved = Mathf.Min(left, item.MaxStack);
                var stack = new Stack { Item = item, Count = moved, Origin = origin, Rotated = rotated };
                stacks.Add(stack);
                Place(stack);
                left -= moved;
                last = stack;
            }

            int added = accepted - left;
            if (added > 0)
            {
                Recalculate();
                NotifyChanged();
                SelectStack(last);
            }
            return count - added;
        }

        /// <summary>Takes one selected stack out of the inventory; call again to take the next one.</summary>
        public override bool TakeSelected(out ItemDefinition item, out int count)
        {
            item = null;
            count = 0;
            var stack = SelectedStack;
            if (stack == null) return false;

            item = stack.Item;
            count = stack.Count;
            Remove(stack);
            return true;
        }

        /// <summary>
        /// Takes one piece out of the stack and puts it into the hands. Whatever was in the hands goes back to
        /// the grid first; if there is no room for it nothing changes.
        /// </summary>
        public bool Hold(Stack stack)
        {
            if (stack == null || !stacks.Contains(stack) || !stack.Item.Holdable) return false;

            var item = stack.Item;
            if (held != null && !Unhold()) return false;

            // The stack may have been merged away while the old item was put back.
            if (!stacks.Contains(stack)) return false;
            stack.Count -= 1;
            if (stack.Count <= 0) Remove(stack);
            else { Recalculate(); NotifyChanged(); }

            held = item;
            HeldChanged?.Invoke();
            NotifyChanged();
            return true;
        }

        /// <summary>Puts the item in the hands back into the grid. Returns false when there is no room.</summary>
        public bool Unhold()
        {
            if (held == null) return true;

            var item = held;
            held = null; // not counted while it looks for a place
            if (Add(item, 1) > 0)
            {
                held = item;
                return false;
            }
            HeldChanged?.Invoke();
            return true;
        }

        /// <summary>Empties the hands without putting anything back (to drop the item).</summary>
        public bool TakeHeld(out ItemDefinition item)
        {
            item = held;
            if (held == null) return false;
            held = null;
            HeldChanged?.Invoke();
            NotifyChanged();
            return true;
        }

        /// <summary>What dropping the item in the hands at <paramref name="origin"/> would do.</summary>
        public MoveKind EvaluateHeld(Vector2Int origin, bool rotated)
        {
            if (held == null) return MoveKind.Invalid;
            return EvaluateCells(new Stack { Item = held, Count = 1, Origin = origin, Rotated = rotated }, origin, rotated, false);
        }

        /// <summary>Puts the item in the hands into the grid at a chosen place (or on a matching stack).</summary>
        public bool PlaceHeld(Vector2Int origin, bool rotated)
        {
            var item = held;
            var temp = item != null ? new Stack { Item = item, Count = 1, Origin = origin, Rotated = rotated } : null;
            var kind = EvaluateHeld(origin, rotated);
            if (kind == MoveKind.Invalid) return false;

            held = null;
            if (kind == MoveKind.Move)
            {
                stacks.Add(temp);
                Place(temp);
                SelectStack(temp);
            }
            else
            {
                var target = FindOverlapped(temp, origin, rotated);
                target.Count += 1;
                SelectStack(target);
            }
            Recalculate();
            HeldChanged?.Invoke();
            NotifyChanged();
            return true;
        }

        /// <summary>Splits <paramref name="amount"/> items off a stack into a new stack in the first free spot.</summary>
        public Stack Split(Stack stack, int amount)
        {
            if (stack == null || !stacks.Contains(stack)) return null;
            if (amount < 1 || amount >= stack.Count) return null;
            if (!FindSpot(stack.Item, out var origin, out bool rotated)) return null;

            stack.Count -= amount;
            var piece = new Stack { Item = stack.Item, Count = amount, Origin = origin, Rotated = rotated };
            stacks.Add(piece);
            Place(piece);
            Recalculate();
            NotifyChanged();
            SelectStack(piece);
            return piece;
        }

        /// <summary>True when a free spot exists for a stack of this item (used before splitting).</summary>
        public bool HasFreeSpotFor(ItemDefinition item) => item != null && FindSpot(item, out _, out _);

        /// <summary>Removes a whole stack (to drop it, for example).</summary>
        public bool Remove(Stack stack)
        {
            if (stack == null || !stacks.Remove(stack)) return false;

            Clear(stack);
            bool wasSelected = selection.Remove(stack);
            Recalculate();
            NotifyChanged();
            if (wasSelected) SelectedStackChanged?.Invoke();
            return true;
        }

        // ---- counting, trading and moving items between inventories ----

        /// <summary>How many items of this kind lie in the grid (the hands are not counted).</summary>
        public int CountOf(ItemDefinition item)
        {
            int total = 0;
            foreach (var stack in stacks)
            {
                if (stack.Item == item) total += stack.Count;
            }
            return total;
        }

        /// <summary>Takes up to <paramref name="count"/> items of this kind out of the grid (smallest stacks first). Returns how many were taken.</summary>
        public int RemoveItems(ItemDefinition item, int count)
        {
            if (item == null || count <= 0) return 0;

            var matching = stacks.Where(s => s.Item == item).OrderBy(s => s.Count).ToList();
            int left = count;
            foreach (var stack in matching)
            {
                if (left <= 0) break;
                int taken = Mathf.Min(left, stack.Count);
                left -= taken;
                Reduce(stack, taken);
            }
            return count - left;
        }

        /// <summary>Takes <paramref name="amount"/> items out of a stack; the stack disappears when it is empty.</summary>
        public void Reduce(Stack stack, int amount)
        {
            if (stack == null || amount <= 0 || !stacks.Contains(stack)) return;
            if (amount >= stack.Count)
            {
                Remove(stack);
                return;
            }
            stack.Count -= amount;
            Recalculate();
            NotifyChanged();
        }

        /// <summary>True when the grid could take this many items as a whole (room and weight), without placing them.</summary>
        public bool CanFit(ItemDefinition item, int count)
        {
            if (item == null || count <= 0) return false;
            if (CountByWeight(item) < count) return false;

            // Try on a copy of the occupancy so nothing changes.
            var grid = GridSize;
            var occupied = new bool[grid.x, grid.y];
            for (int x = 0; x < grid.x; x++)
            {
                for (int y = 0; y < grid.y; y++) occupied[x, y] = Cells[x, y] != null;
            }

            int left = count;
            foreach (var stack in stacks)
            {
                if (stack.Item != item) continue;
                left -= item.MaxStack - stack.Count;
                if (left <= 0) return true;
            }
            while (left > 0)
            {
                if (!FindFreeSpot(item, occupied, out var origin, out bool rotated)) return false;
                var size = Oriented(item, rotated);
                for (int x = origin.x; x < origin.x + size.x; x++)
                {
                    for (int y = origin.y; y < origin.y + size.y; y++) occupied[x, y] = true;
                }
                left -= item.MaxStack;
            }
            return true;
        }

        /// <summary>What putting items of this kind at <paramref name="origin"/> would do.</summary>
        public MoveKind EvaluateItem(ItemDefinition item, Vector2Int origin, bool rotated)
        {
            if (item == null) return MoveKind.Invalid;
            return EvaluateCells(new Stack { Item = item, Count = 1, Origin = origin, Rotated = rotated }, origin, rotated, false);
        }

        /// <summary>
        /// Puts items that come from outside (another inventory) at a chosen place: on free cells, or onto a stack
        /// of the same item. Respects the max stack and the weight limit. Returns how many were placed.
        /// </summary>
        public int PlaceItem(ItemDefinition item, int count, Vector2Int origin, bool rotated)
        {
            var kind = EvaluateItem(item, origin, rotated);
            if (kind == MoveKind.Invalid || count <= 0) return 0;

            int amount = Mathf.Min(count, CountByWeight(item));
            if (amount <= 0) return 0;

            Stack target;
            if (kind == MoveKind.Move)
            {
                amount = Mathf.Min(amount, item.MaxStack);
                target = new Stack { Item = item, Count = amount, Origin = origin, Rotated = rotated };
                stacks.Add(target);
                Place(target);
            }
            else
            {
                var probe = new Stack { Item = item, Count = 1, Origin = origin, Rotated = rotated };
                target = FindOverlapped(probe, origin, rotated);
                amount = Mathf.Min(amount, item.MaxStack - target.Count);
                target.Count += amount;
            }

            Recalculate();
            NotifyChanged();
            SelectStack(target);
            return amount;
        }

        /// <summary>Empties the grid and the hands.</summary>
        public void Clear()
        {
            stacks.Clear();
            selection.Clear();
            held = null;
            cells = null;
            weight = 0f;
            NotifyChanged();
            SelectedStackChanged?.Invoke();
            HeldChanged?.Invoke();
        }

        /// <summary>Copies the contents into a save-friendly form.</summary>
        public InventoryData Export()
        {
            var data = new InventoryData { held = held != null ? held.Id : null };
            foreach (var stack in stacks)
            {
                data.stacks.Add(new StackData
                {
                    item = stack.Item.Id, count = stack.Count, x = stack.Origin.x, y = stack.Origin.y, rotated = stack.Rotated
                });
            }
            return data;
        }

        /// <summary>Replaces the contents with saved ones. Stacks that no longer fit or whose item is gone are skipped.</summary>
        public void Import(InventoryData data)
        {
            stacks.Clear();
            selection.Clear();
            held = null;
            cells = null;
            weight = 0f;

            if (data != null)
            {
                foreach (var saved in data.stacks)
                {
                    var item = ItemDatabase.Find(saved.item);
                    if (item == null || saved.count <= 0) continue;

                    var origin = new Vector2Int(saved.x, saved.y);
                    var probe = new Stack { Item = item, Count = saved.count, Origin = origin, Rotated = saved.rotated };
                    if (EvaluateCells(probe, origin, saved.rotated, false) != MoveKind.Move)
                    {
                        // The layout is no longer valid (the grid changed): look for another place.
                        if (!FindSpot(item, out origin, out bool rotated)) continue;
                        probe.Origin = origin;
                        probe.Rotated = rotated;
                    }
                    stacks.Add(probe);
                    Place(probe);
                }
                Recalculate();

                var heldItem = ItemDatabase.Find(data.held);
                if (heldItem != null) held = heldItem;
            }

            NotifyChanged();
            SelectedStackChanged?.Invoke();
            HeldChanged?.Invoke();
        }

        /// <summary>Tells what putting the stack at <paramref name="origin"/> would do.</summary>
        public MoveKind Evaluate(Stack stack, Vector2Int origin, bool rotated) => EvaluateCells(stack, origin, rotated, true);

        private MoveKind EvaluateCells(Stack stack, Vector2Int origin, bool rotated, bool mustBeInGrid)
        {
            if (stack == null || (mustBeInGrid && !stacks.Contains(stack))) return MoveKind.Invalid;
            if (rotated != stack.Rotated && !stack.Item.CanRotate) return MoveKind.Invalid;

            var size = Oriented(stack.Item, rotated);
            var grid = GridSize;
            if (origin.x < 0 || origin.y < 0 || origin.x + size.x > grid.x || origin.y + size.y > grid.y) return MoveKind.Invalid;

            Stack other = null;
            for (int x = origin.x; x < origin.x + size.x; x++)
            {
                for (int y = origin.y; y < origin.y + size.y; y++)
                {
                    var occupant = Cells[x, y];
                    if (occupant == null || occupant == stack) continue;
                    if (other != null && other != occupant) return MoveKind.Invalid;
                    other = occupant;
                }
            }

            if (other == null) return MoveKind.Move;
            // Dropping on top of a stack of the same item with room left merges into it.
            return other.Item == stack.Item && other.Count < stack.Item.MaxStack ? MoveKind.Merge : MoveKind.Invalid;
        }

        /// <summary>Moves (and turns) a stack, or merges it into the stack it is dropped on.</summary>
        public bool TryMove(Stack stack, Vector2Int origin, bool rotated)
        {
            switch (Evaluate(stack, origin, rotated))
            {
                case MoveKind.Move:
                    Clear(stack);
                    stack.Origin = origin;
                    stack.Rotated = rotated;
                    Place(stack);
                    NotifyChanged();
                    return true;

                case MoveKind.Merge:
                    var target = FindOverlapped(stack, origin, rotated);
                    int moved = Mathf.Min(stack.Count, stack.Item.MaxStack - target.Count);
                    target.Count += moved;
                    stack.Count -= moved;
                    if (stack.Count <= 0)
                    {
                        Remove(stack);
                        SelectStack(target);
                    }
                    else NotifyChanged();
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>True when the whole group can be shifted by <paramref name="delta"/> cells: everything stays
        /// inside the grid and only lands on free cells or on cells the group itself is leaving.</summary>
        public bool CanMoveGroup(IReadOnlyList<Stack> group, Vector2Int delta)
        {
            var grid = GridSize;
            foreach (var stack in group)
            {
                if (!stacks.Contains(stack)) return false;
                var size = stack.Size;
                var origin = stack.Origin + delta;
                if (origin.x < 0 || origin.y < 0 || origin.x + size.x > grid.x || origin.y + size.y > grid.y) return false;

                for (int x = origin.x; x < origin.x + size.x; x++)
                {
                    for (int y = origin.y; y < origin.y + size.y; y++)
                    {
                        var occupant = Cells[x, y];
                        if (occupant != null && !group.Contains(occupant)) return false;
                    }
                }
            }
            return true;
        }

        /// <summary>Shifts a group of stacks together, keeping their layout.</summary>
        public bool TryMoveGroup(IReadOnlyList<Stack> group, Vector2Int delta)
        {
            if (delta == Vector2Int.zero || !CanMoveGroup(group, delta)) return false;

            foreach (var stack in group) Clear(stack);
            foreach (var stack in group)
            {
                stack.Origin += delta;
                Place(stack);
            }
            NotifyChanged();
            return true;
        }

        /// <summary>Turns a stack by 90 degrees where it lies, if the cells are free.</summary>
        public bool TryRotate(Stack stack)
        {
            return stack != null && stack.Item.CanRotate && Evaluate(stack, stack.Origin, !stack.Rotated) == MoveKind.Move
                && TryMove(stack, stack.Origin, !stack.Rotated);
        }

        private Stack FindOverlapped(Stack stack, Vector2Int origin, bool rotated)
        {
            var size = Oriented(stack.Item, rotated);
            for (int x = origin.x; x < origin.x + size.x; x++)
            {
                for (int y = origin.y; y < origin.y + size.y; y++)
                {
                    if (Cells[x, y] != null && Cells[x, y] != stack) return Cells[x, y];
                }
            }
            return null;
        }

        private bool FindSpot(ItemDefinition item, out Vector2Int origin, out bool rotated) =>
            FindFreeSpot(item, null, out origin, out rotated);

        // With an occupancy map the search runs on that map instead of the real cells.
        private bool FindFreeSpot(ItemDefinition item, bool[,] occupied, out Vector2Int origin, out bool rotated)
        {
            var grid = GridSize;
            for (int turn = 0; turn < 2; turn++)
            {
                rotated = turn == 1;
                if (rotated && (!item.CanRotate || item.GridSize.x == item.GridSize.y)) break;

                var size = Oriented(item, rotated);
                for (int y = 0; y + size.y <= grid.y; y++)
                {
                    for (int x = 0; x + size.x <= grid.x; x++)
                    {
                        if (!IsFree(new Vector2Int(x, y), size, occupied)) continue;
                        origin = new Vector2Int(x, y);
                        return true;
                    }
                }
            }
            origin = default;
            rotated = false;
            return false;
        }

        private bool IsFree(Vector2Int origin, Vector2Int size, bool[,] occupied = null)
        {
            for (int x = origin.x; x < origin.x + size.x; x++)
            {
                for (int y = origin.y; y < origin.y + size.y; y++)
                {
                    if (occupied != null ? occupied[x, y] : Cells[x, y] != null) return false;
                }
            }
            return true;
        }

        private void Place(Stack stack)
        {
            var size = stack.Size;
            for (int x = stack.Origin.x; x < stack.Origin.x + size.x; x++)
            {
                for (int y = stack.Origin.y; y < stack.Origin.y + size.y; y++) Cells[x, y] = stack;
            }
        }

        private void Clear(Stack stack)
        {
            var size = stack.Size;
            for (int x = stack.Origin.x; x < stack.Origin.x + size.x; x++)
            {
                for (int y = stack.Origin.y; y < stack.Origin.y + size.y; y++)
                {
                    if (Cells[x, y] == stack) Cells[x, y] = null;
                }
            }
        }

        private void Recalculate()
        {
            float sum = 0f;
            foreach (var stack in stacks) sum += stack.Item.Weight * stack.Count;
            weight = sum;
        }
    }
}
