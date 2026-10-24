using System;
using UnityEngine;

namespace ProjectMayham.Items
{
    /// <summary>One of the player's two hands.</summary>
    public enum Hand
    {
        Right = 0,
        Left = 1,
    }

    /// <summary>
    /// The two hands of the inventory's owner. Each holds one piece of a thing outside of the grid; a two-handed
    /// thing (<see cref="ItemDefinition.TwoHanded"/>) takes both and is kept in the right one. One hand is the active
    /// one: what it holds is <see cref="HeldItem"/>, the thing the mouse buttons use. Taking a thing in a hand that
    /// is busy first puts away what the hand holds (both hands for a two-handed thing): into the grid, else outside
    /// (<see cref="DropOutside"/>); when it can go nowhere, nothing changes.
    /// </summary>
    public partial class GridInventory
    {
        private ItemDefinition right;
        private ItemDefinition left;
        private Hand activeHand = Hand.Right;

        /// <summary>Raised when what the hands hold changes, or the other hand becomes the active one.</summary>
        public event Action HeldChanged;

        /// <summary>
        /// Puts a thing out of the hands somewhere outside of the inventory (the player's: on the floor), for when the
        /// hands are freed and the grid has no room. Returns false when it could not. Without it such a thing stays.
        /// </summary>
        public Func<ItemDefinition, bool> DropOutside { get; set; }

        /// <summary>The hand the mouse buttons use.</summary>
        public Hand ActiveHand => activeHand;

        /// <summary>The thing used now: the one in the active hand, or a two-handed one. Null when that hand is empty.</summary>
        public ItemDefinition HeldItem => InHand(activeHand);

        /// <summary>True while a two-handed thing takes both hands.</summary>
        public bool HoldsTwoHanded => right != null && right.TwoHanded;

        public bool HandsEmpty => right == null && left == null;

        /// <summary>What a hand holds. A two-handed thing is in both.</summary>
        public ItemDefinition InHand(Hand hand) => HoldsTwoHanded || hand == Hand.Right ? right : left;

        /// <summary>How many pieces of the item the hands hold: none, one or two.</summary>
        public int HeldCount(ItemDefinition item) =>
            item == null ? 0 : (right == item ? 1 : 0) + (left == item ? 1 : 0);

        private float HeldWeight => (right != null ? right.Weight : 0f) + (left != null ? left.Weight : 0f);

        public static Hand Other(Hand hand) => hand == Hand.Right ? Hand.Left : Hand.Right;

        /// <summary>The other hand becomes the active one.</summary>
        public void SwitchHand() => SetActiveHand(Other(activeHand));

        public void SetActiveHand(Hand hand)
        {
            if (hand == activeHand) return;
            activeHand = hand;
            HeldChanged?.Invoke();
        }

        /// <summary>The two hands trade what they hold. Nothing happens to a two-handed thing.</summary>
        public void SwapHands()
        {
            if (HoldsTwoHanded || HandsEmpty) return;
            (right, left) = (left, right);
            HeldChanged?.Invoke();
            NotifyChanged();
        }

        /// <summary>Takes one piece out of the stack into the active hand.</summary>
        public bool Hold(Stack stack) => Hold(stack, activeHand);

        /// <summary>
        /// Takes one piece out of the stack into a hand (both, for a two-handed thing). What the hand held is put
        /// away first; false, with nothing changed, when it can go nowhere or the thing is not held in the hands.
        /// </summary>
        public bool Hold(Stack stack, Hand hand)
        {
            if (stack == null || stack.Sealed || !stacks.Contains(stack) || !stack.Item.Holdable) return false;

            var item = stack.Item;
            if (!FreeHandsFor(item, hand)) return false;

            // What was put away went into other stacks; this one is still there unless something odd happened.
            if (!stacks.Contains(stack)) return false;
            stack.Count -= 1;
            if (stack.Count <= 0) Remove(stack);
            else { Recalculate(); NotifyChanged(); }

            Put(item, hand);
            return true;
        }

        /// <summary>Takes a thing from outside straight into the active hand.</summary>
        public bool HoldNew(ItemDefinition item) => HoldNew(item, activeHand);

        /// <summary>
        /// Takes a thing from outside straight into a hand (both, for a two-handed thing), when the weight allows
        /// it. What the hand held is put away first; false, with nothing changed, when it can go nowhere.
        /// </summary>
        public bool HoldNew(ItemDefinition item, Hand hand)
        {
            if (item == null || !FitsByWeight(item) || !FreeHandsFor(item, hand)) return false;
            Put(item, hand);
            return true;
        }

        /// <summary>Puts what a hand holds into the grid. False when there is no room: it stays in the hand.</summary>
        public bool Unhold(Hand hand) => PutAway(hand, false, out _);

        /// <summary>Puts what both hands hold into the grid. False when something found no room and stays.</summary>
        public bool Unhold()
        {
            bool rightDone = Unhold(Hand.Right);
            bool leftDone = Unhold(Hand.Left);
            return rightDone && leftDone;
        }

        /// <summary>Frees both hands: into the grid, else outside. False when something could go nowhere.</summary>
        public bool PutAwayHands()
        {
            bool rightDone = PutAway(Hand.Right, true, out _);
            bool leftDone = PutAway(Hand.Left, true, out _);
            return rightDone && leftDone;
        }

        /// <summary>Empties the active hand without putting the thing anywhere (it is dropped, eaten, handed in).</summary>
        public bool TakeHeld(out ItemDefinition item) => TakeHeld(activeHand, out item);

        /// <summary>Empties a hand (both, for a two-handed thing) without putting the thing anywhere.</summary>
        public bool TakeHeld(Hand hand, out ItemDefinition item)
        {
            item = InHand(hand);
            if (item == null) return false;
            Empty(hand);
            HeldChanged?.Invoke();
            NotifyChanged();
            return true;
        }

        /// <summary>Takes one piece of the item out of the hands, out of the active one first. False when neither holds it.</summary>
        public bool TakeFromHands(ItemDefinition item)
        {
            if (item == null) return false;
            if (InHand(activeHand) == item) return TakeHeld(activeHand, out _);
            var other = Other(activeHand);
            return InHand(other) == item && TakeHeld(other, out _);
        }

        /// <summary>What dropping the thing in the active hand at <paramref name="origin"/> would do.</summary>
        public MoveKind EvaluateHeld(Vector2Int origin, bool rotated) => EvaluateHeld(activeHand, origin, rotated);

        /// <summary>What dropping the thing in a hand at <paramref name="origin"/> would do.</summary>
        public MoveKind EvaluateHeld(Hand hand, Vector2Int origin, bool rotated)
        {
            var item = InHand(hand);
            if (item == null) return MoveKind.Invalid;
            return EvaluateCells(new Stack { Item = item, Count = 1, Origin = origin, Rotated = rotated }, origin, rotated, false);
        }

        /// <summary>Puts the thing in the active hand into the grid at a chosen place (or on a matching stack).</summary>
        public bool PlaceHeld(Vector2Int origin, bool rotated) => PlaceHeld(activeHand, origin, rotated);

        /// <summary>Puts the thing in a hand into the grid at a chosen place (or on a matching stack).</summary>
        public bool PlaceHeld(Hand hand, Vector2Int origin, bool rotated)
        {
            var item = InHand(hand);
            var kind = EvaluateHeld(hand, origin, rotated);
            if (kind == MoveKind.Invalid) return false;

            var temp = new Stack { Item = item, Count = 1, Origin = origin, Rotated = rotated };
            Empty(hand);
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

        private void Put(ItemDefinition item, Hand hand)
        {
            if (item.TwoHanded)
            {
                right = item;
                left = null;
            }
            else if (hand == Hand.Right) right = item;
            else left = item;
            HeldChanged?.Invoke();
            NotifyChanged();
        }

        // The slot a hand's thing is kept in: a two-handed thing is in the right one.
        private void Empty(Hand hand)
        {
            if (HoldsTwoHanded || hand == Hand.Right) right = null;
            else left = null;
        }

        // Makes the hands ready for the thing: both for a two-handed one, else the hand, and a two-handed thing goes
        // away whichever hand is asked for. Nothing changes when something cannot be put away.
        private bool FreeHandsFor(ItemDefinition item, Hand hand)
        {
            if (!item.TwoHanded && !HoldsTwoHanded) return PutAway(hand, true, out _);

            var first = right;
            if (!PutAway(Hand.Right, true, out bool firstInGrid)) return false;
            if (PutAway(Hand.Left, true, out _)) return true;

            // The left hand's thing could go nowhere: the right hand takes back what it held, if it is in the grid.
            if (first != null && firstInGrid && RemoveItems(first, 1) == 1)
            {
                right = first;
                HeldChanged?.Invoke();
                NotifyChanged();
            }
            return false;
        }

        // Puts away what a hand holds: into the grid, else (when allowed) outside. True when the hand is empty then.
        private bool PutAway(Hand hand, bool outside, out bool inGrid)
        {
            inGrid = false;
            var item = InHand(hand);
            if (item == null) return true;

            // Not counted while it looks for a place.
            Empty(hand);
            if (Add(item, 1) == 0) inGrid = true;
            else if (!outside || DropOutside == null || !DropOutside(item))
            {
                if (item.TwoHanded || hand == Hand.Right) right = item;
                else left = item;
                return false;
            }
            HeldChanged?.Invoke();
            NotifyChanged();
            return true;
        }

        // From a save: a two-handed thing goes into the right hand; one that finds the hands busy goes into the grid.
        private void ImportHands(ItemDefinition savedRight, ItemDefinition savedLeft, int savedActive)
        {
            activeHand = savedActive == (int)Hand.Left ? Hand.Left : Hand.Right;
            right = savedRight;
            left = savedLeft;
            if (left != null && left.TwoHanded)
            {
                if (right == null) (right, left) = (left, null);
                else
                {
                    Add(left, 1);
                    left = null;
                }
            }
            if (HoldsTwoHanded && left != null)
            {
                Add(left, 1);
                left = null;
            }
        }
    }
}
