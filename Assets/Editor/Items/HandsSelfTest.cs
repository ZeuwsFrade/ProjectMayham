using System;
using System.Collections.Generic;
using ProjectMayham.Core;
using ProjectMayham.Items;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Checks of the two hands that run in the editor without Play mode: Mayham > Items > Check Hands. Which things
    /// take both hands, taking things in the active hand or a chosen one, freeing the hands (into the grid, else
    /// outside), the weight, and saving. The inventories and the items exist only in memory.
    /// </summary>
    public static class HandsSelfTest
    {
        private static readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();

        [MenuItem("Mayham/Items/Check Hands")]
        public static void RunMenu() => Debug.Log(Run());

        /// <summary>"PASS: N checks", or "FAIL" with one line per failed check.</summary>
        public static string Run()
        {
            if (EditorApplication.isPlaying) return "FAIL: leave Play mode first";

            var t = new QuestSelfTest.Checks();
            try
            {
                Group(t, "two-handed", () => TwoHandedTests(t));
                Group(t, "hold", () => HoldTests(t));
                Group(t, "free", () => FreeTests(t));
                Group(t, "save", () => SaveTests(t));
            }
            finally
            {
                foreach (var item in temporary)
                {
                    if (item != null) UnityEngine.Object.DestroyImmediate(item);
                }
                temporary.Clear();
            }

            return t.Failures.Count == 0
                ? $"PASS: {t.Count} checks"
                : $"FAIL: {t.Failures.Count} of {t.Count} checks\n - " + string.Join("\n - ", t.Failures);
        }

        private static void Group(QuestSelfTest.Checks t, string name, Action run)
        {
            t.Group = name;
            try
            {
                run();
            }
            catch (Exception e)
            {
                t.Count++;
                t.Failures.Add($"[{name}] exception: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private static ItemDefinition Thing(string name, float weight, Vector2Int size,
            ItemDefinition.HandUse hands = ItemDefinition.HandUse.ByWeight, int maxStack = 5, bool person = false)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.name = name;
            item.hideFlags = HideFlags.HideAndDontSave;
            temporary.Add(item);
            var so = new SerializedObject(item);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("gridSize").vector2IntValue = size;
            so.FindProperty("weight").floatValue = weight;
            so.FindProperty("maxStack").intValue = maxStack;
            so.FindProperty("holdable").boolValue = true;
            so.FindProperty("hands").enumValueIndex = (int)hands;
            so.FindProperty("isPerson").boolValue = person;
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static GridInventory Grid(Vector2Int size, float maxWeight = 1000f)
        {
            var go = new GameObject("hands self-test") { hideFlags = HideFlags.HideAndDontSave };
            temporary.Add(go);
            var grid = go.AddComponent<GridInventory>();
            grid.Configure(size, maxWeight);
            return grid;
        }

        private static GridInventory.Stack StackOf(GridInventory grid, ItemDefinition item)
        {
            foreach (var stack in grid.Stacks)
            {
                if (stack.Item == item) return stack;
            }
            return null;
        }

        // ---- which things take both hands ----

        private static void TwoHandedTests(QuestSelfTest.Checks t)
        {
            t.Ok(!Thing("Light", 2.5f, Vector2Int.one).TwoHanded, "a light thing takes one hand by weight");
            t.Ok(Thing("Heavy", ItemDefinition.TwoHandedWeight, Vector2Int.one).TwoHanded, "from the threshold on it takes both");
            t.Ok(!Thing("HeavyOne", 8f, Vector2Int.one, ItemDefinition.HandUse.One).TwoHanded, "set to one hand, a heavy thing takes one");
            t.Ok(Thing("LightTwo", 0.5f, Vector2Int.one, ItemDefinition.HandUse.Two).TwoHanded, "set to two hands, a light thing takes both");
            t.Ok(Thing("Child", 1f, Vector2Int.one, ItemDefinition.HandUse.One, 1, true).TwoHanded, "a person always takes both");

            var sledge = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Weapons/Sledgehammer.asset");
            var pipe = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Weapons/Pipe.asset");
            t.Ok(sledge != null && sledge.TwoHanded, "the sledgehammer takes both hands");
            t.Ok(pipe != null && !pipe.TwoHanded, "the pipe takes one");
        }

        // ---- taking things in the hands ----

        private static void HoldTests(QuestSelfTest.Checks t)
        {
            var bar = Thing("Bar", 0.1f, Vector2Int.one);
            var pipe = Thing("Pipe", 2.5f, new Vector2Int(1, 3), maxStack: 1);
            var sledge = Thing("Sledge", 6f, new Vector2Int(2, 3), maxStack: 1);

            var grid = Grid(new Vector2Int(8, 5));
            grid.Add(bar, 3);
            grid.Add(pipe, 1);
            grid.Add(sledge, 1);
            t.Eq(grid.ActiveHand, Hand.Right, "the right hand is the active one at first");

            t.Ok(grid.Hold(StackOf(grid, pipe)), "a pipe is taken");
            t.Ok(grid.InHand(Hand.Right) == pipe && grid.InHand(Hand.Left) == null, "into the active right hand");
            t.Ok(grid.HeldItem == pipe, "and it is the thing in use");

            grid.SwitchHand();
            t.Eq(grid.ActiveHand, Hand.Left, "Q makes the left hand the active one");
            t.Ok(grid.HeldItem == null, "which is empty");
            t.Ok(grid.Hold(StackOf(grid, bar)), "a bar is taken");
            t.Ok(grid.InHand(Hand.Left) == bar && grid.InHand(Hand.Right) == pipe, "into the left hand, the pipe stays in the right");
            t.Eq(grid.CountOf(bar), 2, "one bar left the grid");
            t.Ok(Mathf.Approximately(grid.TotalWeight, 0.1f * 3 + 2.5f + 6f), "both hands weigh with the grid");

            t.Ok(grid.Hold(StackOf(grid, bar), Hand.Right), "a bar dropped on the right hand is taken there");
            t.Ok(grid.InHand(Hand.Right) == bar && grid.CountOf(pipe) == 1, "and the pipe goes back into the grid");
            t.Eq(grid.HeldCount(bar), 2, "two bars in the hands");

            t.Ok(grid.TakeFromHands(bar) && grid.HeldCount(bar) == 1, "one bar is taken out of the hands");
            grid.SwapHands();
            t.Ok(grid.InHand(Hand.Right) == null && grid.InHand(Hand.Left) == bar, "the hands trade what they hold");

            // Both hands for the sledgehammer.
            grid.Hold(StackOf(grid, pipe), Hand.Right);
            t.Ok(grid.Hold(StackOf(grid, sledge)), "the sledgehammer is taken");
            t.Ok(grid.HoldsTwoHanded && grid.InHand(Hand.Right) == sledge && grid.InHand(Hand.Left) == sledge, "into both hands");
            t.Ok(grid.CountOf(pipe) == 1 && grid.CountOf(bar) == 2, "the pipe and the bar go back into the grid");
            t.Ok(grid.HeldItem == sledge, "it is the thing in use whichever hand is the active one");
            t.Ok(Mathf.Approximately(grid.TotalWeight, 0.1f * 2 + 2.5f + 6f), "it weighs once");
            grid.SwitchHand();
            t.Ok(grid.HeldItem == sledge, "Q changes nothing then");
            grid.SwapHands();
            t.Ok(grid.InHand(Hand.Right) == sledge, "nor does trading hands");

            t.Ok(grid.Hold(StackOf(grid, bar), Hand.Left), "a bar taken in the left hand");
            t.Ok(!grid.HoldsTwoHanded && grid.InHand(Hand.Left) == bar && grid.InHand(Hand.Right) == null, "frees both hands of the sledgehammer");
            t.Eq(grid.CountOf(sledge), 1, "which goes back into the grid");

            // Out of a hand into the grid.
            var place = new Vector2Int(7, 4);
            t.Ok(grid.StackAt(place) == null, "the corner is free");
            t.Ok(grid.PlaceHeld(Hand.Left, place, false), "the bar is put in the corner");
            t.Ok(grid.InHand(Hand.Left) == null && grid.StackAt(place) != null && grid.StackAt(place).Item == bar, "and lies there");

            t.Ok(grid.Hold(StackOf(grid, pipe), Hand.Right) && grid.Hold(StackOf(grid, bar), Hand.Left), "both hands busy again");
            t.Ok(grid.Unhold(Hand.Left) && grid.InHand(Hand.Left) == null && grid.InHand(Hand.Right) == pipe, "right click puts away one hand");
            grid.Hold(StackOf(grid, bar), Hand.Left);
            t.Ok(grid.PutAwayHands() && grid.HandsEmpty, "H puts away both");
            t.Ok(grid.CountOf(bar) == 2 && grid.CountOf(pipe) == 1, "into the grid (one bar was taken out of the hands before)");
        }

        // ---- when the grid has no room ----

        private static void FreeTests(QuestSelfTest.Checks t)
        {
            var mug = Thing("Mug", 0.3f, Vector2Int.one, maxStack: 1);
            var box = Thing("Box", 7f, new Vector2Int(2, 2), maxStack: 1);

            // A full grid, a mug in each hand.
            var full = Grid(new Vector2Int(1, 1));
            full.HoldNew(mug, Hand.Right);
            full.HoldNew(mug, Hand.Left);
            full.Add(mug, 1);
            t.Eq(full.HeldCount(mug), 2, "a mug in each hand");
            t.Ok(!full.HoldNew(box), "without a floor a box cannot be taken: the mugs can go nowhere");
            t.Eq(full.HeldCount(mug), 2, "and nothing changes");

            var floor = new List<ItemDefinition>();
            full.DropOutside = item => { floor.Add(item); return true; };
            t.Ok(full.HoldNew(box), "with a floor the box is taken");
            t.Ok(full.HoldsTwoHanded && full.HeldItem == box, "in both hands");
            t.Eq(floor.Count, 2, "and the two mugs fall on the floor");
            t.Eq(full.CountOf(mug), 1, "the grid keeps its own mug");

            // Room for one of the two: one goes into the grid, the other on the floor.
            var one = Grid(new Vector2Int(1, 1));
            one.HoldNew(mug, Hand.Right);
            one.HoldNew(mug, Hand.Left);
            var fallen = new List<ItemDefinition>();
            one.DropOutside = item => { fallen.Add(item); return true; };
            t.Ok(one.HoldNew(box), "a box is taken with room for one mug");
            t.Ok(one.CountOf(mug) == 1 && fallen.Count == 1, "one mug goes into the grid, the other on the floor");

            // Too heavy: nothing moves.
            var weak = Grid(new Vector2Int(4, 4), 5f);
            weak.HoldNew(mug, Hand.Right);
            t.Ok(!weak.HoldNew(box), "a box heavier than the limit is not taken");
            t.Ok(weak.InHand(Hand.Right) == mug && weak.CountOf(mug) == 0, "and the mug stays in the hand");

            // H with a full grid: what finds no room falls.
            var dropped = new List<ItemDefinition>();
            var busy = Grid(new Vector2Int(1, 1));
            busy.Add(mug, 1);
            busy.HoldNew(mug, Hand.Left);
            busy.DropOutside = item => { dropped.Add(item); return true; };
            t.Ok(!busy.Unhold() && busy.InHand(Hand.Left) == mug, "without room the hands cannot be emptied into the grid");
            t.Ok(busy.PutAwayHands() && busy.HandsEmpty && dropped.Count == 1, "H drops what finds no room");
        }

        // ---- the save file ----

        private static void SaveTests(QuestSelfTest.Checks t)
        {
            // The items of the save are looked up by id in the database.
            var pipe = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Weapons/Pipe.asset");
            var mug = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Mug.asset");
            var sledge = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Weapons/Sledgehammer.asset");
            if (pipe == null || mug == null || sledge == null)
            {
                t.Ok(false, "the pipe, the mug and the sledgehammer exist");
                return;
            }

            var grid = Grid(new Vector2Int(8, 5));
            grid.HoldNew(pipe, Hand.Right);
            grid.HoldNew(mug, Hand.Left);
            grid.SwitchHand();
            var data = grid.Export();
            t.Ok(data.held == pipe.Id && data.heldLeft == mug.Id && data.activeHand == (int)Hand.Left, "both hands and the active one are written");

            var copy = Grid(new Vector2Int(8, 5));
            copy.Import(data);
            t.Ok(copy.InHand(Hand.Right) == pipe && copy.InHand(Hand.Left) == mug && copy.ActiveHand == Hand.Left, "and read back");

            var old = new InventoryData { held = mug.Id };
            var fromOld = Grid(new Vector2Int(8, 5));
            fromOld.Import(old);
            t.Ok(fromOld.InHand(Hand.Right) == mug && fromOld.InHand(Hand.Left) == null && fromOld.ActiveHand == Hand.Right,
                "an old save's thing in the hands goes into the right hand");

            var odd = new InventoryData { held = mug.Id, heldLeft = sledge.Id };
            var fromOdd = Grid(new Vector2Int(8, 5));
            fromOdd.Import(odd);
            t.Ok(fromOdd.HoldsTwoHanded == false && fromOdd.InHand(Hand.Right) == mug && fromOdd.CountOf(sledge) == 1,
                "a two-handed thing saved in a busy left hand goes into the grid");

            var counted = new SaveData();
            counted.player = new InventoryData { held = mug.Id, heldLeft = mug.Id };
            t.Eq(Quests.QuestLog.Owned(counted, mug), 2, "the quests count a mug in each hand");
        }
    }
}
