using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Trade;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Makes the things the player wears: the item assets, their pickups on the floor and the import settings of
    /// their icons, then puts them into the item database, into the loot of crates and boxes and into Tyler's
    /// stock. What exists already is not overwritten, so a tuned item or table keeps its numbers.
    /// The icons in Assets/Sprites/Items are placeholders.
    /// </summary>
    public static class EquipmentFactory
    {
        public const string ItemsFolder = "Assets/Data/Items/Equipment";
        private const string IconsFolder = "Assets/Sprites/Items";
        private const string PickupsFolder = "Assets/Prefabs/Interactable";
        private const string PickupTemplate = "Assets/Prefabs/Interactable/Mug.prefab";
        private const string CrateLoot = "Assets/Data/Level/Loot/Loot_Crate.asset";
        private const string Tyler = "Assets/Data/Traders/Tyler.asset";
        private const int PixelsPerUnit = 16;   // like the other item icons: 16 pixels a cell

        private class Spec
        {
            public string Id, Title;
            public EquipSlot Slot;
            public Vector2Int Size, Backpack;
            public float Weight, LootWeight;
            public int Value, Sold;
        }

        private static readonly Spec[] Specs =
        {
            new Spec { Id = "Briefcase", Title = "Портфель", Slot = EquipSlot.Back, Size = new Vector2Int(2, 2), Backpack = new Vector2Int(6, 3), Weight = 1f, Value = 15, LootWeight = 0.4f, Sold = 2 },
            new Spec { Id = "Backpack", Title = "Рюкзак", Slot = EquipSlot.Back, Size = new Vector2Int(2, 3), Backpack = new Vector2Int(8, 5), Weight = 1.5f, Value = 40, LootWeight = 0.25f, Sold = 1 },
            new Spec { Id = "HikingBackpack", Title = "Туристический рюкзак", Slot = EquipSlot.Back, Size = new Vector2Int(3, 3), Backpack = new Vector2Int(10, 6), Weight = 2.5f, Value = 90, LootWeight = 0.1f },
            new Spec { Id = "Cap", Title = "Кепка", Slot = EquipSlot.Head, Size = new Vector2Int(1, 1), Weight = 0.2f, Value = 5, LootWeight = 0.4f },
            new Spec { Id = "Sneakers", Title = "Кроссовки", Slot = EquipSlot.Feet, Size = new Vector2Int(2, 1), Weight = 0.8f, Value = 10, LootWeight = 0.3f },
        };

        [MenuItem("Mayham/Items/Create Equipment")]
        public static void CreateMenu() => Debug.Log(Create());

        public static string Create()
        {
            var problems = new List<string>();
            var made = new List<string>();
            if (!AssetDatabase.IsValidFolder(ItemsFolder)) AssetDatabase.CreateFolder("Assets/Data/Items", "Equipment");

            var items = new List<(Spec spec, ItemDefinition item)>();
            foreach (var spec in Specs)
            {
                var item = EnsureItem(spec, problems, made);
                if (item != null) items.Add((spec, item));
            }
            ShelterSetup.RefreshItemDatabase();

            int loot = AddToCrateLoot(items, problems);
            int sold = AddToTyler(items, problems);
            AssetDatabase.SaveAssets();

            string result = $"Equipment: {made.Count} items made ({string.Join(", ", made)}), {loot} added to the crate loot, {sold} to Tyler's stock";
            return problems.Count == 0 ? result : result + "\n - " + string.Join("\n - ", problems);
        }

        private static ItemDefinition EnsureItem(Spec spec, List<string> problems, List<string> made)
        {
            string itemPath = $"{ItemsFolder}/{spec.Id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
            if (existing != null) return existing;

            var icon = LoadIcon($"{IconsFolder}/{spec.Id}.png");
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
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var so = new SerializedObject(item);
            so.FindProperty("displayName").stringValue = spec.Title;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("maxStack").intValue = 1;
            so.FindProperty("gridSize").vector2IntValue = spec.Size;
            so.FindProperty("canRotate").boolValue = true;
            so.FindProperty("weight").floatValue = spec.Weight;
            so.FindProperty("value").intValue = spec.Value;
            so.FindProperty("equipSlot").enumValueIndex = (int)spec.Slot;
            so.FindProperty("backpackGrid").vector2IntValue = spec.Slot == EquipSlot.Back ? spec.Backpack : new Vector2Int(8, 5);
            so.FindProperty("worldPrefab").objectReferenceValue = prefab;
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

        // Rare finds in crates and boxes: a thing to wear in about one crate of ten that hold something.
        private static int AddToCrateLoot(List<(Spec spec, ItemDefinition item)> items, List<string> problems)
        {
            var table = AssetDatabase.LoadAssetAtPath<LootTable>(CrateLoot);
            if (table == null)
            {
                problems.Add($"no loot table at {CrateLoot} (Mayham > Level > Set Up Loot Containers makes it)");
                return 0;
            }

            var so = new SerializedObject(table);
            var list = so.FindProperty("entries");
            int added = 0;
            foreach (var (spec, item) in items)
            {
                if (table.Entries.Any(e => e.item == item)) continue;
                list.arraySize++;
                var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative("item").objectReferenceValue = item;
                entry.FindPropertyRelative("weight").floatValue = spec.LootWeight;
                entry.FindPropertyRelative("minCount").intValue = 1;
                entry.FindPropertyRelative("maxCount").intValue = 1;
                added++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return added;
        }

        private static int AddToTyler(List<(Spec spec, ItemDefinition item)> items, List<string> problems)
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
