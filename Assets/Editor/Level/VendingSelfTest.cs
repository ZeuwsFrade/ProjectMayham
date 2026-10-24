using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Items;
using ProjectMayham.Level;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Checks of the vending machines without Play mode (Mayham > Level > Check Vending Machines): what is rolled
    /// inside, buying a thing out, shaking and jamming, paying with coins, and that the machine prefab is set up.
    /// </summary>
    public static class VendingSelfTest
    {
        [MenuItem("Mayham/Level/Check Vending Machines")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            var failures = new List<string>();
            int checks = 0;
            void Ok(bool condition, string what)
            {
                checks++;
                if (!condition) failures.Add(what);
            }

            var table = AssetDatabase.LoadAssetAtPath<LootTable>(VendingSetup.TablePath);
            var coin = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Coin.asset");
            var bar = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Consumables/Bar.asset");
            var water = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Consumables/Water.asset");
            Ok(table != null && coin != null && bar != null && water != null, "the table, the coin, a bar and water exist (Set Up Vending Machines)");
            if (failures.Count > 0) return Result(failures, checks);

            // What is inside: nothing now and then, else three to six things of the table.
            var allowed = new HashSet<ItemDefinition>(table.Entries.Select(e => e.item));
            int empty = 0;
            bool counts = true, items = true;
            for (int i = 0; i < 400; i++)
            {
                var stock = new VendingStock();
                stock.Fill(table, 3, 6, 0.15f, new System.Random(i));
                if (stock.IsEmpty) empty++;
                else if (stock.Total < 3 || stock.Total > 6) counts = false;
                if (stock.Slots.Any(s => !allowed.Contains(s.Item))) items = false;
            }
            Ok(counts, "a machine that is not empty holds three to six things");
            Ok(items, "only things of its table");
            Ok(empty > 30 && empty < 100, $"about one machine in seven is empty ({empty} of 400)");

            // Buying takes one out.
            var two = new VendingStock();
            two.Add(bar, 2);
            Ok(two.Take(bar) && two.Total == 1, "a bought thing is taken out");
            Ok(!two.Take(water), "a thing that is not inside cannot be bought");

            // Shaking: luck gives a thing, failures jam the machine.
            var lucky = new VendingStock();
            lucky.Add(bar, 1);
            var result = lucky.Shake(0.1, 0.0, 0.3f, 3, out var dropped);
            Ok(result == VendingStock.ShakeResult.Dropped && dropped == bar && lucky.IsEmpty, "a lucky shake drops a thing");
            Ok(lucky.Shake(0.1, 0.0, 0.3f, 3, out _) == VendingStock.ShakeResult.Empty, "an empty machine gives nothing");
            var stubborn = new VendingStock();
            stubborn.Add(water, 3);
            Ok(stubborn.Shake(0.9, 0.0, 0.3f, 3, out _) == VendingStock.ShakeResult.Nothing, "an unlucky shake gives nothing");
            Ok(stubborn.Shake(0.9, 0.0, 0.3f, 3, out _) == VendingStock.ShakeResult.Nothing, "a second one neither");
            Ok(stubborn.Shake(0.9, 0.0, 0.3f, 3, out _) == VendingStock.ShakeResult.Jammed && stubborn.Jammed, "the third failure jams the machine");
            Ok(stubborn.Shake(0.0, 0.0, 0.3f, 3, out _) == VendingStock.ShakeResult.AlreadyJammed && stubborn.Total == 3, "a jammed machine gives nothing, not even with luck");
            var mixed = new VendingStock();
            mixed.Add(bar, 1);
            mixed.Add(water, 3);
            mixed.Shake(0.0, 0.99, 0.3f, 3, out var last);
            Ok(last == water && mixed.Total == 3, "the thing shaken loose is picked among all inside");

            // Paying with coins.
            var go = new GameObject("vending self-test") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var backpack = go.AddComponent<GridInventory>();
                Ok(!VendingStock.PayCoin(backpack, coin) && VendingStock.Coins(backpack, coin) == 0, "no coin, no purchase");
                backpack.Add(coin, 2);
                Ok(VendingStock.PayCoin(backpack, coin) && VendingStock.Coins(backpack, coin) == 1, "a purchase takes one coin");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            // Every machine in every room is the prefab with the component.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VendingSetup.PrefabPath);
            var machine = prefab != null ? prefab.GetComponent<VendingMachine>() : null;
            Ok(machine != null, "the vending machine prefab sells");
            if (machine != null)
            {
                var so = new SerializedObject(machine);
                Ok(so.FindProperty("stock").objectReferenceValue == table && so.FindProperty("coin").objectReferenceValue == coin,
                    "with the vending table and the coin");
                Ok(so.FindProperty("dropNoise").floatValue < so.FindProperty("shakeNoise").floatValue, "a thing coming out is quieter than a shake");
            }
            return Result(failures, checks);
        }

        private static string Result(List<string> failures, int checks) =>
            failures.Count == 0 ? $"PASS: {checks} checks" : $"FAIL: {failures.Count} of {checks} checks\n - " + string.Join("\n - ", failures);
    }
}
