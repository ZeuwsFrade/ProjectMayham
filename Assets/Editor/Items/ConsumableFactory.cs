using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Trade;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Makes the food, the drinks and the bandages: the item assets, their pickups on the floor and placeholder
    /// icons (Assets/Sprites/Items, replace them with real art under the same names), then puts them into the item
    /// database, the loot of the rooms and Tyler's stock. What exists already is not overwritten, so a tuned item
    /// keeps its numbers. Mayham > Needs > 1. Create Consumables.
    /// </summary>
    public static class ConsumableFactory
    {
        public const string ItemsFolder = "Assets/Data/Items/Consumables";
        private const string IconsFolder = "Assets/Sprites/Items";
        private const string PickupsFolder = "Assets/Prefabs/Interactable";
        private const string PickupTemplate = "Assets/Prefabs/Interactable/Mug.prefab";
        private const string LootFolder = "Assets/Data/Level/Loot";
        private const string Tyler = "Assets/Data/Traders/Tyler.asset";
        private const int PixelsPerUnit = 16;   // like the other item icons: 16 pixels a cell

        private class Spec
        {
            public string Id, Title, Verb;
            public float UseSeconds = 1.5f;
            public float Satiety, Hydration, Nature, Heal, Stamina, RegenMultiplier = 1f, RegenSeconds;
            public float Weight;
            public int Value, MaxStack = 3, Sold;
            public Vector2Int Size = Vector2Int.one;
            public Color Color;
            /// <summary>Loot table name (without Loot_) to the weight in it.</summary>
            public (string table, float weight)[] Loot = new (string, float)[0];
        }

        public static readonly string[] Ids = { "Bar", "Noodles", "Sandwich", "Water", "CheeseFoam", "Coffee", "DripCoffee", "Bandage" };

        private static readonly Spec[] Specs =
        {
            new Spec { Id = "Bar", Title = "Батончик", Verb = "Съесть", UseSeconds = 1f, Satiety = 0.15f, Nature = 0.05f, RegenMultiplier = 2f, RegenSeconds = 20f,
                Weight = 0.1f, Value = 6, MaxStack = 5, Sold = 6, Color = new Color(0.78f, 0.5f, 0.2f),
                Loot = new[] { ("BreakRoom", 2f), ("Office", 0.8f), ("Storage", 0.8f), ("Crate", 0.8f), ("BossOffice", 0.5f), ("Bin", 0.3f) } },
            new Spec { Id = "Noodles", Title = "Лапша", Verb = "Съесть", UseSeconds = 2.5f, Satiety = 0.35f, Hydration = 0.05f, Nature = 0.1f, RegenMultiplier = 2.5f, RegenSeconds = 60f,
                Weight = 0.3f, Value = 12, Sold = 3, Color = new Color(0.9f, 0.78f, 0.35f),
                Loot = new[] { ("BreakRoom", 1.5f), ("Storage", 0.6f), ("Crate", 0.4f) } },
            new Spec { Id = "Sandwich", Title = "Бутерброд", Verb = "Съесть", UseSeconds = 1.8f, Satiety = 0.25f, Nature = 0.06f, RegenMultiplier = 2f, RegenSeconds = 40f,
                Weight = 0.25f, Value = 9, Sold = 4, Size = new Vector2Int(2, 1), Color = new Color(0.85f, 0.7f, 0.45f),
                Loot = new[] { ("BreakRoom", 1.5f), ("Office", 0.6f), ("BossOffice", 0.6f) } },
            new Spec { Id = "Water", Title = "Вода", Verb = "Выпить", UseSeconds = 1.5f, Hydration = 0.3f, Nature = 0.07f,
                Weight = 0.5f, Value = 4, Sold = 8, Size = new Vector2Int(1, 2), Color = new Color(0.45f, 0.72f, 0.95f),
                Loot = new[] { ("BreakRoom", 2f), ("Office", 0.8f), ("Storage", 0.8f), ("Crate", 0.8f), ("Toilet", 0.4f) } },
            new Spec { Id = "CheeseFoam", Title = "Сырная пенка", Verb = "Выпить", UseSeconds = 1.8f, Satiety = 0.08f, Hydration = 0.35f, Nature = 0.12f, RegenMultiplier = 1.5f, RegenSeconds = 30f,
                Weight = 0.4f, Value = 10, Sold = 3, Size = new Vector2Int(1, 2), Color = new Color(0.97f, 0.85f, 0.4f),
                Loot = new[] { ("BreakRoom", 1.2f), ("BossOffice", 0.7f) } },
            new Spec { Id = "Coffee", Title = "Кофе", Verb = "Выпить", UseSeconds = 1.5f, Hydration = 0.12f, Nature = 0.25f, Stamina = 30f,
                Weight = 0.3f, Value = 8, Sold = 4, Color = new Color(0.4f, 0.26f, 0.16f),
                Loot = new[] { ("BreakRoom", 1.5f), ("Office", 0.7f), ("BossOffice", 0.8f) } },
            new Spec { Id = "DripCoffee", Title = "Дрип-кофе", Verb = "Выпить", UseSeconds = 2f, Hydration = 0.1f, Nature = 0.35f, Stamina = 60f,
                Weight = 0.2f, Value = 14, Sold = 2, Color = new Color(0.55f, 0.35f, 0.2f),
                Loot = new[] { ("BreakRoom", 0.6f), ("BossOffice", 0.9f), ("Storage", 0.2f) } },
            new Spec { Id = "Bandage", Title = "Бинты", Verb = "Применить", UseSeconds = 2f, Heal = 10f, RegenMultiplier = 4f, RegenSeconds = 20f,
                Weight = 0.1f, Value = 15, MaxStack = 5, Sold = 4, Color = new Color(0.92f, 0.9f, 0.85f),
                Loot = new[] { ("Storage", 1.2f), ("Cabinet", 0.8f), ("Toilet", 0.5f), ("Crate", 0.5f), ("Office", 0.4f), ("BossOffice", 0.5f) } },
        };

        [MenuItem("Mayham/Needs/1. Create Consumables")]
        public static void CreateMenu() => Debug.Log(Create());

        public static string Create()
        {
            var problems = new List<string>();
            var made = new List<string>();
            if (!AssetDatabase.IsValidFolder(ItemsFolder)) AssetDatabase.CreateFolder("Assets/Data/Items", "Consumables");

            var items = new List<(Spec spec, ConsumableDefinition item)>();
            foreach (var spec in Specs)
            {
                var item = EnsureItem(spec, problems, made);
                if (item != null) items.Add((spec, item));
            }
            ShelterSetup.RefreshItemDatabase();

            int loot = AddToLoot(items, problems);
            int sold = AddToTyler(items, problems);
            AssetDatabase.SaveAssets();

            string result = $"Consumables: {made.Count} made ({string.Join(", ", made)}), {loot} loot entries added, {sold} added to Tyler's stock";
            return problems.Count == 0 ? result : result + "\n - " + string.Join("\n - ", problems);
        }

        private static ConsumableDefinition EnsureItem(Spec spec, List<string> problems, List<string> made)
        {
            string itemPath = $"{ItemsFolder}/{spec.Id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ConsumableDefinition>(itemPath);
            if (existing != null) return existing;

            var icon = EnsureIcon(spec);
            if (icon == null)
            {
                problems.Add($"{spec.Id}: no icon at {IconsFolder}/{spec.Id}.png");
                return null;
            }

            string prefabPath = $"{PickupsFolder}/{spec.Id}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null && !AssetDatabase.CopyAsset(PickupTemplate, prefabPath))
            {
                problems.Add($"{spec.Id}: could not copy {PickupTemplate}");
                return null;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            // Filled in before it becomes an asset: values set afterwards can be lost when the pickup is saved below.
            var item = ScriptableObject.CreateInstance<ConsumableDefinition>();
            var so = new SerializedObject(item);
            so.FindProperty("displayName").stringValue = spec.Title;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("maxStack").intValue = spec.MaxStack;
            so.FindProperty("gridSize").vector2IntValue = spec.Size;
            so.FindProperty("canRotate").boolValue = true;
            so.FindProperty("weight").floatValue = spec.Weight;
            so.FindProperty("value").intValue = spec.Value;
            so.FindProperty("holdable").boolValue = true;
            so.FindProperty("worldPrefab").objectReferenceValue = prefab;
            so.FindProperty("verb").stringValue = spec.Verb;
            so.FindProperty("useSeconds").floatValue = spec.UseSeconds;
            so.FindProperty("satiety").floatValue = spec.Satiety;
            so.FindProperty("hydration").floatValue = spec.Hydration;
            so.FindProperty("natureCall").floatValue = spec.Nature;
            so.FindProperty("heal").floatValue = spec.Heal;
            so.FindProperty("stamina").floatValue = spec.Stamina;
            so.FindProperty("regenMultiplier").floatValue = spec.RegenMultiplier;
            so.FindProperty("regenSeconds").floatValue = spec.RegenSeconds;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(item, itemPath);

            // The pickup is a copy of the mug's: it shows the icon and hands out the new item.
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                root.name = spec.Id;
                var renderer = root.GetComponentInChildren<SpriteRenderer>();
                if (renderer != null) renderer.sprite = icon;
                var pickup = root.GetComponent<ItemPickup>();
                if (pickup != null)
                {
                    var pso = new SerializedObject(pickup);
                    pso.FindProperty("item").objectReferenceValue = item;
                    pso.FindProperty("count").intValue = 1;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                }
                else problems.Add($"{spec.Id}: the pickup template has no ItemPickup");
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            made.Add(spec.Id);
            return item;
        }

        // ---- the placeholder icon ----

        private static Sprite EnsureIcon(Spec spec)
        {
            string path = $"{IconsFolder}/{spec.Id}.png";
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, DrawIcon(spec).EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            return LoadIcon(path);
        }

        // A flat shape in the colour of the item with a dark edge and a light spot: enough to tell them apart in the grid.
        private static Texture2D DrawIcon(Spec spec)
        {
            int w = spec.Size.x * PixelsPerUnit, h = spec.Size.y * PixelsPerUnit;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            var edge = Color.Lerp(spec.Color, Color.black, 0.55f);
            var shine = Color.Lerp(spec.Color, Color.white, 0.5f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool inside = x >= 2 && x < w - 2 && y >= 2 && y < h - 2;
                    bool border = inside && (x < 4 || x >= w - 4 || y < 4 || y >= h - 4);
                    // The corners are cut.
                    bool corner = (x < 4 || x >= w - 4) && (y < 4 || y >= h - 4) && (Mathf.Min(x, w - 1 - x) + Mathf.Min(y, h - 1 - y) < 5);
                    if (!inside || corner) texture.SetPixel(x, y, clear);
                    else if (border) texture.SetPixel(x, y, edge);
                    else if (x < 8 && y > h - 9 && x + (h - 1 - y) < 9) texture.SetPixel(x, y, shine);
                    else texture.SetPixel(x, y, spec.Color);
                }
            }
            texture.Apply();
            return texture;
        }

        private static Sprite LoadIcon(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            bool right = importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                && Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit) && importer.filterMode == FilterMode.Point
                && importer.textureCompression == TextureImporterCompression.Uncompressed && !importer.mipmapEnabled;
            if (!right)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---- loot and trade ----

        private static int AddToLoot(List<(Spec spec, ConsumableDefinition item)> items, List<string> problems)
        {
            int added = 0;
            var tables = new Dictionary<string, (LootTable table, SerializedObject so)>();
            foreach (var (spec, item) in items)
            {
                foreach (var (name, weight) in spec.Loot)
                {
                    if (!tables.TryGetValue(name, out var pair))
                    {
                        var table = AssetDatabase.LoadAssetAtPath<LootTable>($"{LootFolder}/Loot_{name}.asset");
                        if (table == null)
                        {
                            problems.Add($"no loot table Loot_{name} for {spec.Id}");
                            tables[name] = (null, null);
                            continue;
                        }
                        tables[name] = pair = (table, new SerializedObject(table));
                    }
                    if (pair.table == null || pair.table.Entries.Any(e => e.item == item)) continue;

                    var list = pair.so.FindProperty("entries");
                    list.arraySize++;
                    var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                    entry.FindPropertyRelative("item").objectReferenceValue = item;
                    entry.FindPropertyRelative("weight").floatValue = weight;
                    entry.FindPropertyRelative("minCount").intValue = 1;
                    entry.FindPropertyRelative("maxCount").intValue = spec.MaxStack > 3 && weight >= 1f ? 2 : 1;
                    pair.so.ApplyModifiedPropertiesWithoutUndo();
                    added++;
                }
            }
            foreach (var pair in tables.Values)
            {
                if (pair.table != null) EditorUtility.SetDirty(pair.table);
            }
            return added;
        }

        private static int AddToTyler(List<(Spec spec, ConsumableDefinition item)> items, List<string> problems)
        {
            var trader = AssetDatabase.LoadAssetAtPath<TraderDefinition>(Tyler);
            if (trader == null)
            {
                problems.Add($"no trader at {Tyler}");
                return 0;
            }

            var so = new SerializedObject(trader);
            var list = so.FindProperty("stock");
            int added = 0;
            foreach (var (spec, item) in items)
            {
                if (spec.Sold <= 0 || trader.Stock.Any(e => e.item == item)) continue;
                list.arraySize++;
                var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative("item").objectReferenceValue = item;
                entry.FindPropertyRelative("count").intValue = spec.Sold;
                entry.FindPropertyRelative("priceOverride").intValue = 0;
                added++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return added;
        }
    }
}
