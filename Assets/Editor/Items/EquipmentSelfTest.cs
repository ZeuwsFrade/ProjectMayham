using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Items;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Checks of what the player wears that run in the editor without Play mode: Mayham > Items > Check Equipment.
    /// The inventories and the items of the checks exist only in memory; the save file is not touched.
    /// </summary>
    public static class EquipmentSelfTest
    {
        private static readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();

        [MenuItem("Mayham/Items/Check Equipment")]
        public static void RunMenu() => Debug.Log(Run());

        /// <summary>"PASS: N checks", or "FAIL" with one line per failed check.</summary>
        public static string Run()
        {
            if (EditorApplication.isPlaying) return "FAIL: leave Play mode first";

            var t = new QuestSelfTest.Checks();
            // Things that fall out of a test player have no floor to fall on: the warnings about it are expected.
            var filter = Debug.unityLogger.filterLogType;
            Debug.unityLogger.filterLogType = LogType.Error;
            try
            {
                Group(t, "resize", () => ResizeTests(t));
                Group(t, "wear", () => WearTests(t));
                Group(t, "stash", () => StashTests(t));
                Group(t, "save", () => SaveTests(t));
                Group(t, "assets", () => AssetTests(t));
            }
            finally
            {
                Debug.unityLogger.filterLogType = filter;
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

        private static GameObject MakeObject(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            temporary.Add(go);
            return go;
        }

        private static ItemDefinition Wearable(string name, EquipSlot slot, Vector2Int size, Vector2Int backpack, float weight = 1f)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.name = name;
            item.hideFlags = HideFlags.HideAndDontSave;
            temporary.Add(item);
            var so = new SerializedObject(item);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("gridSize").vector2IntValue = size;
            so.FindProperty("weight").floatValue = weight;
            so.FindProperty("equipSlot").enumValueIndex = (int)slot;
            so.FindProperty("backpackGrid").vector2IntValue = backpack;
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        // A player as far as wearing goes: a grid with the usual weight limits and the equipment, no backpack on.
        private static (GridInventory grid, PlayerEquipment worn) Player()
        {
            var go = MakeObject("self-test player");
            var grid = go.AddComponent<GridInventory>();
            var worn = go.AddComponent<PlayerEquipment>();
            worn.Clear();
            return (grid, worn);
        }

        private static string Layout(GridInventory grid) =>
            string.Join(";", grid.Stacks.OrderBy(s => s.Origin.y).ThenBy(s => s.Origin.x).Select(s => $"{s.Item.Id}x{s.Count}@{s.Origin.x},{s.Origin.y}{(s.Rotated ? "r" : "")}"));

        // ---- a grid that changes its size ----

        private static void ResizeTests(QuestSelfTest.Checks t)
        {
            var papers = Item("Papers");
            var books = Item("Books");
            var mop = Item("Mop");

            var grid = MakeObject("self-test grid").AddComponent<GridInventory>();
            var spilled = new List<(ItemDefinition item, int count)>();
            grid.Resize(new Vector2Int(4, 2), spilled);
            t.Eq(grid.GridSize, new Vector2Int(4, 2), "a grid takes the size it is given");

            grid.PlaceItem(books, 1, new Vector2Int(2, 0), false);
            grid.PlaceItem(papers, 5, new Vector2Int(0, 1), false);
            string before = Layout(grid);
            grid.Resize(new Vector2Int(8, 5), spilled);
            t.Eq(grid.GridSize, new Vector2Int(8, 5), "it grows");
            t.Eq(Layout(grid), before, "and everything keeps its place");
            t.Eq(spilled.Count, 0, "nothing falls out when it grows");

            grid.PlaceItem(mop, 1, new Vector2Int(7, 0), false);
            grid.Resize(new Vector2Int(4, 3), spilled);
            t.Ok(grid.Stacks.Any(s => s.Item == books && s.Origin == new Vector2Int(2, 0)), "what still fits where it was stays there");
            t.Ok(grid.Stacks.Any(s => s.Item == papers && s.Origin == new Vector2Int(0, 1)), "the papers too");
            var moved = grid.Stacks.FirstOrDefault(s => s.Item == mop);
            t.Ok(moved != null && moved.Origin.x + moved.Size.x <= 4 && moved.Origin.y + moved.Size.y <= 3, "what no longer fits its place goes to a free one");
            t.Eq(spilled.Count, 0, "nothing falls out while there is room");

            grid.Resize(new Vector2Int(2, 1), spilled);
            t.Eq(grid.GridSize, new Vector2Int(2, 1), "it shrinks");
            t.Ok(spilled.Count >= 2, $"what finds no room is handed back ({spilled.Count} stacks)");
            int kept = grid.Stacks.Sum(s => s.Count) + spilled.Sum(s => s.count);
            t.Eq(kept, 1 + 5 + 1, "nothing is lost: kept and handed back make what there was");
            t.Ok(Mathf.Approximately(grid.TotalWeight, grid.Stacks.Sum(s => s.Item.Weight * s.Count)), "the weight counts only what is left");

            grid.ExtraWeight = 3f;
            t.Ok(Mathf.Approximately(grid.TotalWeight, grid.Stacks.Sum(s => s.Item.Weight * s.Count) + 3f), "what is worn weighs too");
            grid.Resize(new Vector2Int(8, 5), spilled);
            grid.ExtraWeight = grid.MaxWeight;
            t.Eq(grid.Add(papers, 1), 1, "and leaves no room for more when it is as heavy as the limit");
        }

        // ---- putting on and taking off ----

        private static void WearTests(QuestSelfTest.Checks t)
        {
            var papers = Item("Papers");
            var books = Item("Books");
            var small = Wearable("TestSmallPack", EquipSlot.Back, new Vector2Int(2, 2), new Vector2Int(6, 3), 1f);
            var big = Wearable("TestBigPack", EquipSlot.Back, new Vector2Int(2, 3), new Vector2Int(8, 5), 2f);
            var hat = Wearable("TestHat", EquipSlot.Head, new Vector2Int(1, 1), Vector2Int.one, 0.5f);
            var boots = Wearable("TestBoots", EquipSlot.Feet, new Vector2Int(2, 1), Vector2Int.one, 0.5f);

            var (grid, worn) = Player();
            t.Eq(grid.GridSize, worn.Pockets, "without a backpack there are only the pockets");
            t.Eq(worn.Pockets, new Vector2Int(4, 2), "the pockets are 4x2");

            var spilled = new List<(ItemDefinition item, int count)>();
            t.Ok(worn.Put(big, spilled) == null, "nothing was worn before the first backpack");
            t.Eq(grid.GridSize, new Vector2Int(8, 5), "the backpack gives its grid");
            t.Ok(Mathf.Approximately(grid.ExtraWeight, 2f), "and weighs what it weighs");

            grid.PlaceItem(books, 2, new Vector2Int(6, 3), false);
            grid.PlaceItem(papers, 10, new Vector2Int(0, 0), false);
            grid.Add(small, 1);
            var smallStack = grid.Stacks.First(s => s.Item == small);
            t.Ok(worn.WearFromGrid(smallStack), "a backpack in the grid can be put on");
            t.Ok(worn[EquipSlot.Back] == small, "it is worn now");
            t.Eq(grid.GridSize, new Vector2Int(6, 3), "the grid is the one of the smaller backpack");
            t.Ok(grid.Stacks.All(s => s.Item != small), "it is not in the grid any more");
            t.Ok(grid.Stacks.Any(s => s.Item == papers && s.Origin == Vector2Int.zero), "what fits keeps its place");
            t.Ok(grid.Stacks.Any(s => s.Item == books), "the books found another place");
            t.Ok(Mathf.Approximately(grid.ExtraWeight, 1f), "the weight of what is worn follows");

            // What was worn before goes into the grid; with no room it falls out (no floor here, so it is just gone).
            int before = grid.Stacks.Count(s => s.Item == big);
            t.Eq(before, 1, "the backpack that was taken off lies in the grid");

            var fill = new List<(ItemDefinition item, int count)>();
            worn.Put(big, fill);
            grid.Clear();
            // A sheet in every cell of the 8x5 grid: light, so the weight limit plays no part.
            for (int x = 0; x < 8; x++)
                for (int y = 0; y < 5; y++) grid.PlaceItem(papers, 1, new Vector2Int(x, y), false);
            t.Eq(grid.Stacks.Count, 40, "the big backpack is full");
            var falling = new List<(ItemDefinition item, int count)>();
            var old = worn.Put(small, falling);
            t.Ok(old == big, "putting on another backpack hands back the one that was worn");
            t.Eq(grid.Stacks.Count, 18, "the smaller grid is full");
            t.Eq(falling.Sum(f => f.count), 22, "and what it cannot take falls out");

            var gone = new List<(ItemDefinition item, int count)>();
            t.Ok(worn.Remove(EquipSlot.Back, gone) == small, "the backpack comes off");
            t.Eq(grid.GridSize, worn.Pockets, "only the pockets are left");
            t.Ok(worn[EquipSlot.Back] == null, "nothing is worn on the back");

            t.Ok(worn.WearIfFree(hat), "a hat goes on a free head");
            t.Ok(!worn.WearIfFree(Wearable("TestHat2", EquipSlot.Head, Vector2Int.one, Vector2Int.one)), "but not on a head that has one");
            t.Ok(worn.WearIfFree(boots), "boots go on");
            t.Eq(grid.GridSize, worn.Pockets, "neither changes the grid");
            t.Ok(Mathf.Approximately(grid.ExtraWeight, 1f), "both weigh");
            t.Ok(!worn.WearIfFree(papers), "a thing that is not worn cannot be put on");

            grid.Clear();
            t.Ok(worn.TakeOffToGrid(EquipSlot.Head), "a hat comes off into the grid");
            t.Eq(grid.CountOf(hat), 1, "and lies there");
            t.Ok(worn[EquipSlot.Head] == null, "the head is free");
            t.Ok(!worn.TakeOffToGrid(EquipSlot.Head), "nothing comes off a free head");

            worn.Clear();
            t.Ok(worn[EquipSlot.Feet] == null && worn[EquipSlot.Back] == null, "after death nothing is worn");
            t.Eq(grid.GridSize, worn.Pockets, "and the grid is the pockets");
            t.Ok(Mathf.Approximately(grid.ExtraWeight, 0f), "and weighs nothing extra");

            // Straight out of a container: the new thing leaves it, what was worn takes its place there.
            var (grid3, worn3) = Player();
            worn3.Put(small, new List<(ItemDefinition item, int count)>());
            var chest = MakeObject("self-test chest").AddComponent<GridInventory>();
            chest.Configure(new Vector2Int(4, 4), 100000f);
            chest.PlaceItem(big, 1, new Vector2Int(1, 1), false);
            t.Ok(worn3.WearFrom(chest, chest.Stacks.First(s => s.Item == big)), "a backpack can be put on straight out of a container");
            t.Ok(worn3[EquipSlot.Back] == big, "it is worn then");
            t.Eq(grid3.GridSize, new Vector2Int(8, 5), "with its grid");
            t.Eq(chest.CountOf(big), 0, "and is no longer in the container");
            t.Ok(chest.Stacks.Any(s => s.Item == small && s.Origin == new Vector2Int(1, 1)), "the backpack that was worn lies where the new one lay");

            // A container with no room for what was worn: it goes into the player's grid.
            var (grid4, worn4) = Player();
            worn4.Put(big, new List<(ItemDefinition item, int count)>());
            var box = MakeObject("self-test box").AddComponent<GridInventory>();
            box.Configure(new Vector2Int(2, 2), 100000f);
            box.PlaceItem(small, 1, Vector2Int.zero, false);
            t.Ok(worn4.WearFrom(box, box.Stacks[0]), "a backpack comes out of a box just its size");
            t.Eq(grid4.CountOf(big), 1, "the bigger one that was worn goes into the player's grid, the box has no room for it");

            // Too heavy to carry, or not a thing to wear: nothing happens.
            var anvil = Wearable("TestAnvil", EquipSlot.Head, Vector2Int.one, Vector2Int.one, 30f);
            box.Clear();
            box.PlaceItem(anvil, 1, Vector2Int.zero, false);
            t.Ok(!worn4.WearFrom(box, box.Stacks[0]), "a thing heavier than the player can carry is not put on");
            t.Eq(box.CountOf(anvil), 1, "and stays where it was");
            box.Clear();
            box.PlaceItem(papers, 3, Vector2Int.zero, false);
            t.Ok(!worn4.WearFrom(box, box.Stacks[0]), "papers are not worn");
        }

        // ---- the stash window: taking off into it, swapping out of it ----

        private static void StashTests(QuestSelfTest.Checks t)
        {
            var papers = Item("Papers");
            var books = Item("Books");
            var small = Wearable("TestSmallPack", EquipSlot.Back, new Vector2Int(2, 2), new Vector2Int(6, 3), 1f);
            var big = Wearable("TestBigPack", EquipSlot.Back, new Vector2Int(2, 3), new Vector2Int(8, 5), 2f);
            var hat = Wearable("TestHat", EquipSlot.Head, new Vector2Int(1, 1), Vector2Int.one, 0.5f);
            var none = new List<(ItemDefinition item, int count)>();

            var stash = MakeObject("self-test stash").AddComponent<GridInventory>();
            stash.Configure(new Vector2Int(10, 10), 100000f);

            // A hat goes into the stash where it is dropped.
            var (grid, worn) = Player();
            worn.Put(hat, none);
            t.Ok(worn.TakeOffTo(stash, EquipSlot.Head, new Vector2Int(3, 4)), "a hat comes off into the stash");
            t.Ok(worn[EquipSlot.Head] == null, "the head is free then");
            t.Ok(stash.Stacks.Any(s => s.Item == hat && s.Origin == new Vector2Int(3, 4)), "and the hat lies where it was dropped");
            t.Ok(!worn.TakeOffTo(stash, EquipSlot.Head), "nothing comes off a free head");

            // A place that is taken: it goes wherever it fits.
            worn.Put(hat, none);
            t.Ok(worn.TakeOffTo(stash, EquipSlot.Head, new Vector2Int(3, 4)), "a hat dropped on a taken place still comes off");
            t.Eq(stash.CountOf(hat), 2, "and finds a place of its own");

            // The backpack with things in it: they stay with the player as far as the pockets hold, the rest goes
            // into the stash, nothing onto the floor.
            stash.Clear();
            worn.Put(big, none);
            grid.Add(books, 6);
            grid.Add(papers, 4);
            int booksBefore = grid.CountOf(books), papersBefore = grid.CountOf(papers);
            t.Ok(worn.TakeOffTo(stash, EquipSlot.Back, Vector2Int.zero), "a backpack full of things comes off into the stash");
            t.Ok(worn[EquipSlot.Back] == null && grid.GridSize == worn.Pockets, "the player has the pockets left");
            t.Ok(stash.Stacks.Any(s => s.Item == big && s.Origin == Vector2Int.zero), "the backpack lies where it was dropped");
            t.Eq(grid.CountOf(books) + stash.CountOf(books), booksBefore, "no book is lost: the pockets and the stash hold them all");
            t.Eq(grid.CountOf(papers) + stash.CountOf(papers), papersBefore, "no paper either");

            // No room in a box: nothing changes.
            var box = MakeObject("self-test box").AddComponent<GridInventory>();
            box.Configure(new Vector2Int(1, 1), 100000f);
            box.Add(papers, 1);
            worn.Put(hat, none);
            t.Ok(!worn.TakeOffTo(box, EquipSlot.Head), "a hat does not come off into a full box");
            t.Ok(worn[EquipSlot.Head] == hat, "it stays on");

            // A smaller backpack out of the stash: what it does not hold goes into the stash, not on the floor.
            var (grid2, worn2) = Player();
            worn2.Put(big, none);
            stash.Clear();
            stash.PlaceItem(small, 1, Vector2Int.zero, false);
            var mug = Item("Mug");
            grid2.Add(mug, 30);
            int carried = grid2.CountOf(mug);
            t.Ok(carried > 18, $"the big backpack holds more mugs than the small one has cells ({carried})");
            t.Ok(worn2.WearFrom(stash, stash.Stacks[0]), "a smaller backpack is put on out of the stash");
            t.Ok(worn2[EquipSlot.Back] == small && stash.CountOf(big) == 1, "the big one goes into the stash in its place");
            t.Ok(stash.CountOf(mug) > 0, "what the small one does not hold goes into the stash");
            t.Eq(grid2.CountOf(mug) + stash.CountOf(mug), carried, "and nothing is lost");
        }

        // ---- the save file ----

        private static void SaveTests(QuestSelfTest.Checks t)
        {
            var fresh = new SaveData();
            t.Eq(fresh.version, SaveData.CurrentVersion, "a new game has the current version");
            t.Eq(fresh.player.back, SaveData.StartBackpack, "and starts with the briefcase");

            // A save of version 1: no equipment in it, the inventory was 8x5.
            const string old = "{\"version\":1,\"money\":40,\"player\":{\"stacks\":[{\"item\":\"Papers\",\"count\":3,\"x\":7,\"y\":4,\"rotated\":false}],\"held\":\"\"},"
                + "\"traders\":[{\"trader\":\"Tyler\",\"stock\":[{\"item\":\"Papers\",\"count\":30}]}]}";
            var loaded = JsonUtility.FromJson<SaveData>(old);
            t.Eq(loaded.version, 1, "an old save reads as version 1");
            loaded.Upgrade();
            t.Eq(loaded.version, SaveData.CurrentVersion, "it is brought up to date");
            t.Eq(loaded.player.back, SaveData.OldInventoryBackpack, "the player gets the backpack as big as the old inventory");
            t.Eq(loaded.player.stacks.Count, 1, "and keeps what they carried");
            var tyler = loaded.traders.Find(x => x.trader == "Tyler");
            t.Ok(tyler.stock.Any(s => s.item == SaveData.StartBackpack) && tyler.stock.Any(s => s.item == SaveData.OldInventoryBackpack), "Tyler gets the backpacks he sells now");
            loaded.Upgrade();
            t.Eq(tyler.stock.Count(s => s.item == SaveData.StartBackpack), 1, "a second upgrade adds nothing");

            t.Ok(SaveData.TylerWeapons.All(w => tyler.stock.Any(s => s.item == w)), "and the weapons too");

            // A save of version 2: equipment already, but Tyler without the weapons; one of them he has sold out.
            const string two = "{\"version\":2,\"player\":{\"stacks\":[],\"held\":\"\",\"back\":\"Backpack\"},"
                + "\"traders\":[{\"trader\":\"Tyler\",\"stock\":[{\"item\":\"Papers\",\"count\":30},{\"item\":\"Pipe\",\"count\":0}]}]}";
            var second = JsonUtility.FromJson<SaveData>(two);
            second.Upgrade();
            var tyler2 = second.traders.Find(x => x.trader == "Tyler");
            t.Eq(second.version, SaveData.CurrentVersion, "a save of version 2 is brought up to date");
            t.Eq(second.player.back, "Backpack", "what is worn stays as it is");
            t.Ok(tyler2.stock.Any(s => s.item == "Sledgehammer" && s.count == 1), "Tyler gets the sledgehammer");
            t.Eq(tyler2.stock.Count(s => s.item == "Pipe"), 1, "a weapon he has in his list already is not added twice");
            t.Ok(tyler2.stock.All(s => s.item != SaveData.StartBackpack), "and the backpacks of version 2 are not added again");

            var current = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData { player = new InventoryData { back = "" } }));
            current.Upgrade();
            t.Eq(current.player.back, string.Empty, "a current save without a backpack stays without one");

            // Writing and reading what is worn.
            var items = new[] { Item("Equipment/Briefcase"), Item("Equipment/Cap") };
            if (items.Any(i => i == null))
            {
                t.Ok(false, "the equipment items exist (Mayham > Items > Create Equipment)");
                return;
            }
            var (grid, worn) = Player();
            var spilled = new List<(ItemDefinition item, int count)>();
            worn.Put(items[0], spilled);
            worn.Put(items[1], spilled);
            var data = grid.Export();
            worn.Export(data);
            t.Eq(data.back, "Briefcase", "the backpack is written by its id");
            t.Eq(data.head, "Cap", "and the hat");
            t.Eq(data.feet, string.Empty, "an empty slot is written empty");

            var (grid2, worn2) = Player();
            worn2.Import(data);
            grid2.Import(data);
            t.Ok(worn2[EquipSlot.Back] == items[0] && worn2[EquipSlot.Head] == items[1], "reading it puts the same things on");
            t.Eq(grid2.GridSize, items[0].BackpackGrid, "with the grid of the backpack");

            data.head = "Papers";
            data.back = "NoSuchItem";
            worn2.Import(data);
            t.Ok(worn2[EquipSlot.Head] == null && worn2[EquipSlot.Back] == null, "an unknown thing or one from another slot is skipped");
            t.Eq(grid2.GridSize, worn2.Pockets, "and the grid is the pockets then");
        }

        // ---- the items of the project ----

        private static void AssetTests(QuestSelfTest.Checks t)
        {
            var expected = new (string id, EquipSlot slot, Vector2Int grid)[]
            {
                ("Briefcase", EquipSlot.Back, new Vector2Int(6, 3)), ("Backpack", EquipSlot.Back, new Vector2Int(8, 5)),
                ("HikingBackpack", EquipSlot.Back, new Vector2Int(10, 6)), ("Cap", EquipSlot.Head, Vector2Int.zero), ("Sneakers", EquipSlot.Feet, Vector2Int.zero),
            };
            foreach (var (id, slot, size) in expected)
            {
                var item = ItemDatabase.Find(id);
                t.Ok(item != null, $"{id} is in the item database");
                if (item == null) continue;
                t.Eq(item.EquipSlot, slot, $"{id} is worn in its slot");
                if (slot == EquipSlot.Back) t.Eq(item.BackpackGrid, size, $"{id} gives its grid");
                t.Ok(item.Icon != null, $"{id} has an icon");
                t.Ok(item.WorldPrefab != null && item.WorldPrefab.GetComponent<ItemPickup>() != null
                     && item.WorldPrefab.GetComponent<ItemPickup>().Item == item, $"{id} lies on the floor as itself");
                t.Ok(item.GridSize.x <= 4 && item.GridSize.y <= 2 || item.CanRotate && item.GridSize.y <= 4 && item.GridSize.x <= 2 || slot == EquipSlot.Back,
                    $"{id} fits into the pockets");
            }
        }
    }
}
