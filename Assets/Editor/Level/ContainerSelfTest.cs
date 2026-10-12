using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.Level;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Checks of the loot containers that run in the editor without Play mode: Mayham > Level > Check Containers.
    /// Everything they make exists only in memory. The check of whole raids is in the second file of the class.
    /// </summary>
    public static partial class ContainerSelfTest
    {
        private static readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();

        [MenuItem("Mayham/Level/Check Containers")]
        public static void RunMenu() => Debug.Log(Run());

        /// <summary>"PASS: N checks", or "FAIL" with one line per failed check.</summary>
        public static string Run()
        {
            if (EditorApplication.isPlaying) return "FAIL: leave Play mode first";

            var t = new QuestSelfTest.Checks();
            try
            {
                Group(t, "inventory", () => InventoryTests(t));
                Group(t, "modal", () => ModalTests(t));
                Group(t, "profile", () => ProfileTests(t));
                Group(t, "container", () => ContainerTests(t));
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

        // ---- what the checks are made of ----

        private static ItemDefinition Item(string name) =>
            AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Data/Items/{name}.asset");

        private static T Make<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            asset.hideFlags = HideFlags.HideAndDontSave;
            temporary.Add(asset);
            return asset;
        }

        private static GameObject MakeObject(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            temporary.Add(go);
            return go;
        }

        private static GridInventory Inventory(int columns, int rows)
        {
            var inventory = MakeObject("self-test inventory").AddComponent<GridInventory>();
            inventory.Configure(new Vector2Int(columns, rows), 100000f);
            return inventory;
        }

        private static LootTable Table(params (ItemDefinition item, int min, int max)[] entries)
        {
            var table = Make<LootTable>("self-test table");
            var so = new SerializedObject(table);
            var list = so.FindProperty("entries");
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("item").objectReferenceValue = entries[i].item;
                entry.FindPropertyRelative("weight").floatValue = 1f;
                entry.FindPropertyRelative("minCount").intValue = entries[i].min;
                entry.FindPropertyRelative("maxCount").intValue = entries[i].max;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        private static LootContainerProfile Profile(string title, int columns, int rows, LootTable loot,
            float emptyChance, int minStacks, int maxStacks, float roomLootChance = 0f, float seconds = 1f)
        {
            var profile = Make<LootContainerProfile>("self-test profile");
            var so = new SerializedObject(profile);
            so.FindProperty("title").stringValue = title;
            so.FindProperty("gridSize").vector2IntValue = new Vector2Int(columns, rows);
            so.FindProperty("secondsPerItem").floatValue = seconds;
            so.FindProperty("loot").objectReferenceValue = loot;
            so.FindProperty("emptyChance").floatValue = emptyChance;
            so.FindProperty("minStacks").intValue = minStacks;
            so.FindProperty("maxStacks").intValue = maxStacks;
            so.FindProperty("roomLootChance").floatValue = roomLootChance;
            so.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private static LootContainer Container(LootContainerProfile profile, string name = "self-test container")
        {
            var container = MakeObject(name).AddComponent<LootContainer>();
            var so = new SerializedObject(container);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.ApplyModifiedPropertiesWithoutUndo();
            return container;
        }

        private static string Contents(GridInventory inventory) =>
            string.Join(";", inventory.Stacks.Select(s => $"{s.Item.Id}x{s.Count}@{s.Origin.x},{s.Origin.y}{(s.Sealed ? "?" : "")}"));

        // ---- sealed stacks and inventories made from code ----

        private static void InventoryTests(QuestSelfTest.Checks t)
        {
            var papers = Item("Papers");
            var books = Item("Books");

            var inventory = MakeObject("self-test inventory").AddComponent<GridInventory>();
            t.Ok(inventory.Configure(new Vector2Int(4, 4), 100000f), "an empty inventory takes a size from code");
            t.Eq(inventory.GridSize, new Vector2Int(4, 4), "and has that size");
            t.Ok(inventory.MaxWeight >= 100000f, "and that weight limit");

            var hidden = inventory.AddSealed(papers, 3);
            t.Ok(hidden != null && hidden.Sealed, "a sealed stack is put in");
            if (hidden == null) return;

            t.Eq(hidden.Count, 3, "with the amount that was asked for");
            t.Eq(inventory.Add(papers, 2), 0, "more of the same item still fits into the grid");
            t.Eq(hidden.Count, 3, "but not into the sealed stack");
            t.Eq(inventory.Stacks.Count, 2, "it becomes a stack of its own");
            t.Eq(inventory.EvaluateItem(papers, hidden.Origin, false), GridInventory.MoveKind.Invalid, "nothing can be dropped onto a sealed stack");
            t.Eq(inventory.PlaceItem(papers, 1, hidden.Origin, false), 0, "and nothing is placed there");
            t.Ok(!inventory.TryMove(hidden, new Vector2Int(3, 3), false), "a sealed stack cannot be moved");
            t.Ok(inventory.Split(hidden, 1) == null, "or split");
            t.Ok(!inventory.Configure(new Vector2Int(2, 2), 5f), "an inventory in use is not set up again");
            t.Eq(inventory.GridSize, new Vector2Int(4, 4), "it keeps its size");

            inventory.Unseal(hidden);
            t.Ok(!hidden.Sealed, "a found stack is no longer sealed");
            t.Eq(inventory.EvaluateItem(papers, hidden.Origin, false), GridInventory.MoveKind.Merge, "and takes more of its item like any stack");

            var full = Inventory(1, 1);
            t.Ok(full.AddSealed(papers, 1) != null, "one sealed sheet fills a grid of one cell");
            t.Ok(!full.CanFit(papers, 1), "the room left in a sealed stack does not count");
            t.Ok(!full.HasRoomFor(papers), "neither for a pickup");
            t.Ok(full.AddSealed(papers, 1) == null, "a second sealed stack finds no cell");

            var roomy = Inventory(4, 4);
            var big = roomy.AddSealed(papers, 50);
            t.Eq(big != null ? big.Count : -1, papers.MaxStack, "a sealed stack holds a full stack at most");
            var second = roomy.AddSealed(papers, 2);
            t.Ok(second != null && second != big, "the same item put in again lies apart");
            t.Ok(Inventory(1, 1).AddSealed(books, 1) == null, "a thing larger than the grid is not put in");
        }

        // ---- windows that do not stop the time ----

        private static void ModalTests(QuestSelfTest.Checks t)
        {
            float scale = Time.timeScale;
            try
            {
                ModalState.Reset();
                ModalState.Push(false);
                t.Ok(ModalState.IsOpen, "a window that does not pause still counts as open");
                t.Ok(!ModalState.IsPaused, "the game is not paused by it");
                t.Eq(Time.timeScale, 1f, "and the time goes on");

                ModalState.Push();
                t.Ok(ModalState.IsPaused, "a pausing window over it pauses the game");
                t.Eq(Time.timeScale, 0f, "and stops the time");
                ModalState.Pop();
                t.Eq(Time.timeScale, 1f, "closing it lets the time go on");
                t.Ok(ModalState.IsOpen, "while the first window is still open");
                ModalState.Pop(false);
                t.Ok(!ModalState.IsOpen, "closing that one leaves nothing open");

                ModalState.Push();
                ModalState.Push(false);
                ModalState.Pop(false);
                t.Eq(Time.timeScale, 0f, "closing a window that does not pause keeps the pause of another");
                ModalState.Pop();
                t.Eq(Time.timeScale, 1f, "until that one is closed too");
            }
            finally
            {
                ModalState.Reset();
                Time.timeScale = scale;
                // Setting the time scale outside Play mode marks the time settings of the project as changed,
                // and they would be written out again although nothing in them is different.
                foreach (var settings in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset"))
                    EditorUtility.ClearDirty(settings);
            }
        }

        // ---- what a kind of container holds ----

        private static void ProfileTests(QuestSelfTest.Checks t)
        {
            var papers = Item("Papers");
            var books = Item("Books");
            var mug = Item("Mug");
            var mop = Item("Mop");
            var table = Table((papers, 1, 3), (books, 1, 1));
            var roomTable = Table((mug, 1, 1));

            var always = Profile("Шкаф", 4, 5, table, 0f, 1, 3);
            bool inRange = true, fromTable = true, countsRight = true;
            for (int seed = 1; seed <= 200; seed++)
            {
                var roll = always.Roll(new System.Random(seed));
                inRange &= roll.Count >= 1 && roll.Count <= 3;
                fromTable &= roll.All(r => r.item == papers || r.item == books);
                countsRight &= roll.All(r => r.item == papers ? r.count >= 1 && r.count <= 3 : r.count == 1);
            }
            t.Ok(inRange, "a container that is never empty holds between the least and the most stacks");
            t.Ok(fromTable, "only things from its table");
            t.Ok(countsRight, "in the amounts of the table");

            string Text(List<(ItemDefinition item, int count)> roll) => string.Join(";", roll.Select(r => $"{r.item.Id}x{r.count}"));
            t.Eq(Text(always.Roll(new System.Random(5))), Text(always.Roll(new System.Random(5))), "the same seed gives the same contents");

            var never = Profile("Шкаф", 4, 5, table, 1f, 1, 3);
            t.Ok(Enumerable.Range(1, 50).All(seed => never.Roll(new System.Random(seed)).Count == 0), "a kind that is always empty holds nothing");

            var mostlyEmpty = Profile("Шкаф", 4, 5, table, 0.7f, 1, 3);
            int empty = Enumerable.Range(1, 4000).Count(seed => mostlyEmpty.Roll(new System.Random(seed)).Count == 0);
            t.Ok(empty > 2640 && empty < 2960, $"about 70% of 4000 containers are empty when the profile says so (got {empty})");

            var withRoom = Profile("Шкаф", 4, 5, table, 0f, 1, 1, 1f);
            var mixed = withRoom.Roll(new System.Random(9), roomTable);
            t.Eq(mixed.Count, 2, "a thing from the room's table comes on top");
            t.Ok(mixed.Count == 2 && mixed[1].item == mug, "and it is from that table");
            t.Eq(withRoom.Roll(new System.Random(9)).Count, 1, "without a room table there is no such thing");

            var bin = Profile("Урна", 2, 2, table, 0f, 1, 1);
            t.Ok(bin.Fits(mug), "a mug fits into a bin");
            t.Ok(!bin.Fits(mop), "a mop does not");
            t.Eq(Profile("Полка", 3, 1, table, 0f, 1, 1).Fits(mop), mop.CanRotate, "a mop lies on a low shelf only turned");
        }

        // ---- searching ----

        private static void ContainerTests(QuestSelfTest.Checks t)
        {
            var papers = Item("Papers");
            var books = Item("Books");
            var mug = Item("Mug");
            var mop = Item("Mop");
            var table = Table((papers, 1, 3), (books, 1, 1));

            var cabinet = Container(Profile("Шкаф", 4, 5, table, 0f, 2, 2));
            t.Eq(cabinet.Inventory != null ? cabinet.Inventory.GridSize : Vector2Int.zero, new Vector2Int(4, 5), "the grid has the size of the profile");
            t.Ok(cabinet.Inventory != null && cabinet.Inventory.MaxWeight >= 10000f, "and no weight limit worth the name");
            if (cabinet.Inventory == null) return;

            t.Eq(cabinet.Fill(new System.Random(3)), 2, "a raid puts in what the profile rolls");
            t.Eq(cabinet.Inventory.Stacks.Count, 2, "as stacks of the grid");
            t.Ok(cabinet.Inventory.Stacks.All(s => s.Sealed), "none of them found yet");
            t.Eq(cabinet.HiddenCount, 2, "both count as hidden");
            t.Eq(cabinet.Prompt, "Обыскать: Шкаф", "the prompt asks to search");
            t.Ok(!cabinet.IsSearched, "it is not searched yet");

            string first = Contents(cabinet.Inventory);
            cabinet.Fill(new System.Random(3));
            t.Eq(Contents(cabinet.Inventory), first, "filling again with the same seed gives the same contents");

            var next = cabinet.Inventory.Stacks.OrderBy(s => s.Origin.y).ThenBy(s => s.Origin.x).First();
            t.Ok(ReferenceEquals(cabinet.Next, next), "the thing nearest to the top-left corner is found first");

            cabinet.Open();
            t.Eq(cabinet.Search(0.4f), 0, "less than the time of one thing finds nothing");
            t.Ok(next.Sealed, "the thing stays hidden");
            t.Ok(Mathf.Abs(cabinet.SearchProgress - 0.4f) < 0.001f, "the progress shows how far the search is");
            t.Eq(cabinet.Search(0.7f), 1, "when the time is up one thing is found");
            t.Ok(!next.Sealed, "it is the one that was next");
            t.Eq(cabinet.HiddenCount, 1, "one is left");
            t.Ok(Mathf.Abs(cabinet.SearchProgress - 0.1f) < 0.001f, "the time over goes to the next thing");
            t.Eq(cabinet.Prompt, "Обыскать: Шкаф", "the prompt still asks to search");
            t.Eq(cabinet.Search(30f), 1, "a long wait finds what is left and no more");
            t.Eq(cabinet.HiddenCount, 0, "nothing is hidden any more");
            t.Eq(cabinet.SearchProgress, 0f, "and no search is going on");
            t.Ok(cabinet.IsSearched, "the container is searched");
            t.Eq(cabinet.Prompt, "Открыть: Шкаф", "the prompt offers to open it");

            cabinet.Inventory.Add(mug, 1);
            t.Ok(cabinet.Inventory.Stacks.Where(s => s.Item == mug).All(s => !s.Sealed), "what the player puts in is not hidden");
            t.Eq(cabinet.HiddenCount, 0, "so nothing is left to find");

            var slow = Container(Profile("Ящик", 4, 4, table, 0f, 2, 2, 0f, 2f));
            slow.Fill(new System.Random(8));
            slow.Open();
            t.Eq(slow.Search(1.9f), 0, "a kind that takes two seconds a thing is not done after 1.9");
            t.Eq(slow.Search(4.2f), 2, "several things can be found in one long frame");

            var nothing = Container(Profile("Шкаф", 3, 3, table, 1f, 1, 1));
            t.Eq(nothing.Fill(new System.Random(1)), 0, "an empty kind gets nothing");
            t.Eq(nothing.Prompt, "Обыскать: Шкаф", "the player cannot tell before looking");
            nothing.Open();
            t.Ok(nothing.IsSearched, "one look is enough for an empty container");
            t.Eq(nothing.Prompt, "Шкаф (пусто)", "after that the prompt says it is empty");
            nothing.Inventory.Add(mug, 1);
            t.Eq(nothing.Prompt, "Открыть: Шкаф", "unless the player left something there");

            var bin = Container(Profile("Урна", 2, 2, table, 1f, 1, 1));
            t.Ok(bin.CanHide(mug, 1), "a mug can be hidden in a bin");
            t.Ok(!bin.CanHide(mop, 1), "a mop cannot");
            t.Ok(!bin.Hide(mop, 1), "and is not");
            t.Eq(bin.Inventory.Stacks.Count, 0, "the bin stays empty");
            t.Ok(bin.Hide(mug, 1), "the mug is hidden");
            t.Eq(bin.HiddenCount, 1, "as a thing to be found");
            t.Ok(bin.CanHide(mug, 3), "three more mugs have room");
            t.Ok(!bin.CanHide(mug, 4), "four do not");
            t.Ok(!bin.Hide(mug, 4), "so they are not hidden");
            t.Eq(bin.HiddenCount, 1, "and nothing of them is left behind");
            t.Ok(bin.Hide(mug, 3), "three are");
            t.Eq(bin.HiddenCount, 4, "each as a stack of its own");

            var plain = MakeObject("Тумба").AddComponent<LootContainer>();
            t.Eq(plain.Title, "Тумба", "a container without a profile is called like its object");
            t.Eq(plain.Fill(new System.Random(1)), 0, "and gets nothing");
            t.Ok(plain.Inventory != null, "but can still be opened");
        }
    }
}
