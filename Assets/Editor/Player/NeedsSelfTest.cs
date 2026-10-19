using System;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Player;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Checks of the needs that run in the editor without Play mode: Mayham > Needs > Run Self-Test.
    /// The formulas, the shipped items and fixtures, and the save of older versions.
    /// </summary>
    public static class NeedsSelfTest
    {
        [MenuItem("Mayham/Needs/Run Self-Test")]
        public static void RunMenu() => Debug.Log(Run());

        /// <summary>Runs every group. "PASS: N checks", or "FAIL" with one line per failed check.</summary>
        public static string Run()
        {
            if (EditorApplication.isPlaying) return "FAIL: leave Play mode first";

            var t = new QuestSelfTest.Checks();
            Group(t, "formulas", () => FormulaTests(t));
            Group(t, "assets", () => AssetTests(t));
            Group(t, "save", () => SaveTests(t));

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

        private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;

        private static void FormulaTests(QuestSelfTest.Checks t)
        {
            // Hunger takes half of the stamina scale at its worst.
            t.Ok(Near(NeedsMath.StaminaCap(100f, 0f), 100f), "a fed player has all the stamina");
            t.Ok(Near(NeedsMath.StaminaCap(100f, 1f), 50f), "a starving player has half of it");
            t.Ok(Near(NeedsMath.StaminaCap(100f, 0.5f), 75f), "half hungry: three quarters");

            // A quarter of the health left: the stamina comes back four times slower.
            t.Ok(Near(NeedsMath.StaminaRegenFactor(1f, 0f), 1f), "full health: full speed");
            t.Ok(Near(NeedsMath.StaminaRegenFactor(0.25f, 0f), 0.25f), "a quarter of the health: four times slower");
            // Thirst doubles the price of every missing percent.
            t.Ok(Near(NeedsMath.StaminaRegenFactor(0.9f, 1f, 0f), 0.8f), "thirsty, 10% missing: 20% slower");
            t.Ok(Near(NeedsMath.StaminaRegenFactor(0.5f, 1f, 0.1f), 0.1f), "it never drops below the floor");
            t.Ok(NeedsMath.StaminaRegenFactor(0.25f, 1f) <= NeedsMath.StaminaRegenFactor(0.25f, 0f), "thirst never helps");

            // Thirst: half the healing speed, twice the stamina price.
            t.Ok(Near(NeedsMath.HealthRegenThirstFactor(0f), 1f) && Near(NeedsMath.HealthRegenThirstFactor(1f), 0.5f), "thirst halves the healing");
            t.Ok(Near(NeedsMath.StaminaPerHealth(2f, 1f), 4f) && Near(NeedsMath.StaminaPerHealth(2f, 0f), 2f), "thirst doubles the stamina price of health");

            // The load.
            t.Ok(Near(NeedsMath.WeightNeedsMultiplier(0f), 1f), "a light load changes nothing");
            t.Ok(NeedsMath.WeightNeedsMultiplier(1f) > NeedsMath.WeightNeedsMultiplier(0.5f) && NeedsMath.WeightNeedsMultiplier(0.5f) > 1f, "a heavier load: hungrier");

            // The call of nature.
            t.Ok(Near(NeedsMath.NatureSpeed(0.6f), 1f) && Near(NeedsMath.NatureSpeed(1f), 0.7f), "the speed falls from the threshold to the worst");
            t.Ok(Near(NeedsMath.SmellRadius(0.5f), 0f), "no smell under the threshold");
            t.Ok(NeedsMath.SmellRadius(0.7f) > 0f && NeedsMath.SmellRadius(1f) > NeedsMath.SmellRadius(0.7f), "the smell grows");
        }

        private static void AssetTests(QuestSelfTest.Checks t)
        {
            foreach (string id in ConsumableFactory.Ids)
            {
                var item = AssetDatabase.LoadAssetAtPath<ConsumableDefinition>($"{ConsumableFactory.ItemsFolder}/{id}.asset");
                t.Ok(item != null, $"{id} exists (Mayham > Needs > 1)");
                if (item == null) continue;

                t.Ok(item.Holdable, $"{id} can be taken in the hands");
                t.Ok(ItemDatabase.Find(id) == item, $"{id} is in the item database");
                t.Ok(item.WorldPrefab != null && item.WorldPrefab.GetComponent<ItemPickup>() != null
                     && item.WorldPrefab.GetComponent<ItemPickup>().Item == item, $"{id} has a pickup that holds it");
                bool does = item.Satiety > 0f || item.Hydration > 0f || item.Heal > 0f || item.Stamina > 0f || item.RegenSeconds > 0f;
                t.Ok(does, $"{id} does something");
                t.Ok(item.Icon != null && item.Weight > 0f, $"{id} has an icon and a weight");
            }

            var coffee = AssetDatabase.LoadAssetAtPath<ConsumableDefinition>($"{ConsumableFactory.ItemsFolder}/Coffee.asset");
            var drip = AssetDatabase.LoadAssetAtPath<ConsumableDefinition>($"{ConsumableFactory.ItemsFolder}/DripCoffee.asset");
            var water = AssetDatabase.LoadAssetAtPath<ConsumableDefinition>($"{ConsumableFactory.ItemsFolder}/Water.asset");
            var bar = AssetDatabase.LoadAssetAtPath<ConsumableDefinition>($"{ConsumableFactory.ItemsFolder}/Bar.asset");
            var bandage = AssetDatabase.LoadAssetAtPath<ConsumableDefinition>($"{ConsumableFactory.ItemsFolder}/Bandage.asset");
            if (coffee != null && drip != null && water != null && bar != null && bandage != null)
            {
                t.Ok(coffee.NatureCall > water.NatureCall * 2f && drip.NatureCall > coffee.NatureCall, "coffee fills the call of nature most");
                t.Ok(coffee.Stamina > 0f && drip.Stamina > coffee.Stamina, "coffee gives stamina at once, the drip more");
                t.Ok(bar.Satiety > 0f && bar.Hydration == 0f, "food takes hunger away");
                t.Ok(water.Hydration > 0f, "water takes thirst away");
                t.Ok(bandage.Heal > 0f && bandage.RegenMultiplier > bar.RegenMultiplier, "bandages heal and speed healing up the most");
            }

            var cooler = AssetDatabase.LoadAssetAtPath<GameObject>(NeedsSetup.CoolerPrefab);
            var tap = AssetDatabase.LoadAssetAtPath<GameObject>(NeedsSetup.TapPrefab);
            t.Ok(cooler != null && cooler.GetComponent<WaterPoint>() != null, "the water cooler gives water (Mayham > Needs > 2)");
            t.Ok(tap != null && tap.GetComponent<WaterPoint>() != null, "the tap gives water");
            foreach (string path in NeedsSetup.ToiletPrefabs)
            {
                var toilet = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                t.Ok(toilet != null && toilet.GetComponent<Toilet>() != null, $"{path} is a toilet");
            }
        }

        private static void SaveTests(QuestSelfTest.Checks t)
        {
            var fresh = new SaveData();
            t.Ok(fresh.needs != null && fresh.needs.hunger == 0f, "a new game starts fed");

            // A save of version 4: no needs in it, Tyler without the food.
            var old = JsonUtility.FromJson<SaveData>("{\"version\":4,\"money\":40,\"traders\":[{\"trader\":\"Tyler\",\"stock\":[]}]}");
            old.Upgrade();
            t.Eq(old.version, SaveData.CurrentVersion, "an old save is brought up to date");
            t.Ok(old.needs != null && old.needs.thirst == 0f, "it has needs, and the player is fed");
            var tyler = old.traders.Find(x => x.trader == "Tyler");
            t.Ok(tyler != null && SaveData.TylerConsumables.All(c => tyler.stock.Any(s => s.item == c.item)), "Tyler sells the food after the upgrade");

            var saved = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData { needs = new NeedsData { hunger = 0.3f, thirst = 0.6f, nature = 0.9f } }));
            t.Ok(Near(saved.needs.hunger, 0.3f) && Near(saved.needs.thirst, 0.6f) && Near(saved.needs.nature, 0.9f), "the needs survive a save");
        }
    }
}
