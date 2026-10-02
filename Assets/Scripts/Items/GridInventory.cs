using System;
using System.Collections.Generic;
using System.Linq;
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

        public override float TotalWeight => weight;
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

        /// <summary>Tells what putting the stack at <paramref name="origin"/> would do.</summary>
        public MoveKind Evaluate(Stack stack, Vector2Int origin, bool rotated)
        {
            if (stack == null || !stacks.Contains(stack)) return MoveKind.Invalid;
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

        private bool FindSpot(ItemDefinition item, out Vector2Int origin, out bool rotated)
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
                        if (!IsFree(new Vector2Int(x, y), size)) continue;
                        origin = new Vector2Int(x, y);
                        return true;
                    }
                }
            }
            origin = default;
            rotated = false;
            return false;
        }

        private bool IsFree(Vector2Int origin, Vector2Int size)
        {
            for (int x = origin.x; x < origin.x + size.x; x++)
            {
                for (int y = origin.y; y < origin.y + size.y; y++)
                {
                    if (Cells[x, y] != null) return false;
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
