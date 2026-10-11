using System.Collections.Generic;
using ProjectMayham.Items;
using ProjectMayham.Level;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Turns the furniture that can be searched into loot containers: makes the loot tables and the profiles of
    /// the kinds of containers that are missing and puts a <see cref="LootContainer"/> on the furniture prefabs.
    /// The rooms use these prefabs as they are, so they do not have to be built again. Nothing that exists is
    /// overwritten: a profile or a table that was tuned by hand stays as it is.
    /// </summary>
    public static class ContainerFactory
    {
        public const string ProfilesFolder = "Assets/Data/Level/Containers";
        private const string LootFolder = "Assets/Data/Level/Loot";
        private const string ItemsFolder = "Assets/Data/Items";
        private const string PrefabsFolder = "Assets/Prefabs";

        private class TableSpec
        {
            public string Name;
            public (string item, float weight, int min, int max)[] Entries;
        }

        private class ProfileSpec
        {
            public string Name, Title, Table;
            public Vector2Int Grid;
            public float Seconds = 1f, Empty;
            public int Min = 1, Max = 1;
            public string[] Prefabs;
        }

        private static readonly TableSpec[] Tables =
        {
            new TableSpec { Name = "Loot_Cabinet", Entries = new[] { ("Papers", 6f, 2, 8), ("Folders", 5f, 1, 3), ("Notes", 3f, 1, 3), ("Books", 2f, 1, 2), ("Coin", 1f, 1, 4) } },
            new TableSpec { Name = "Loot_Bookshelf", Entries = new[] { ("Books", 6f, 1, 3), ("Folders", 2f, 1, 2), ("Notes", 2f, 1, 2), ("Papers", 1f, 2, 5) } },
            new TableSpec { Name = "Loot_Crate", Entries = new[] { ("Mug", 4f, 1, 1), ("Mop", 2f, 1, 1), ("Books", 2f, 1, 2), ("Papers", 2f, 3, 10), ("Coin", 2f, 2, 8) } },
            new TableSpec { Name = "Loot_Bin", Entries = new[] { ("Notes", 5f, 1, 2), ("Papers", 4f, 1, 3), ("Coin", 2f, 1, 3), ("Mug", 1f, 1, 1) } },
        };

        private static readonly ProfileSpec[] Profiles =
        {
            new ProfileSpec { Name = "Container_Bin", Title = "Урна", Table = "Loot_Bin", Grid = new Vector2Int(2, 2), Seconds = 0.6f, Empty = 0.75f,
                Prefabs = new[] { "Interactable/Bin" } },
            new ProfileSpec { Name = "Container_CabinetSmall", Title = "Шкафчик", Table = "Loot_Cabinet", Grid = new Vector2Int(3, 3), Empty = 0.7f, Max = 2,
                Prefabs = new[] { "Interactable/Filing-Cabinet-Small" } },
            new ProfileSpec { Name = "Container_Cabinet", Title = "Шкаф", Table = "Loot_Cabinet", Grid = new Vector2Int(4, 5), Empty = 0.65f, Max = 3,
                Prefabs = new[] { "Interactable/Big-Filing-Cabinet", "Interactable/Filing-Cabinet-Open", "Interactable/Filing-Cabinet-Tall", "Interactable/Wide-Filing-Cabinet" } },
            new ProfileSpec { Name = "Container_Bookshelf", Title = "Книжный шкаф", Table = "Loot_Bookshelf", Grid = new Vector2Int(4, 5), Empty = 0.65f, Max = 3,
                Prefabs = new[] { "Furniture/Bookshelf", "Furniture/Tall-Bookshelf" } },
            new ProfileSpec { Name = "Container_Crate", Title = "Ящик", Table = "Loot_Crate", Grid = new Vector2Int(4, 4), Empty = 0.65f, Max = 3,
                Prefabs = new[] { "Furniture/Crate" } },
            new ProfileSpec { Name = "Container_Box", Title = "Коробка", Table = "Loot_Crate", Grid = new Vector2Int(3, 3), Empty = 0.7f, Max = 2,
                Prefabs = new[] { "Furniture/Box" } },
        };

        // Chance of one more thing from the loot table of the room, the same for every kind.
        private const float RoomLootChance = 0.3f;

        [MenuItem("Mayham/Level/Set Up Loot Containers")]
        public static void SetUpMenu() => Debug.Log(SetUp());

        public static string SetUp()
        {
            var problems = new List<string>();
            int tables = 0, profiles = 0, prefabs = 0;

            foreach (var spec in Tables)
            {
                if (EnsureTable(spec, problems)) tables++;
            }
            if (!AssetDatabase.IsValidFolder(ProfilesFolder)) AssetDatabase.CreateFolder("Assets/Data/Level", "Containers");
            foreach (var spec in Profiles)
            {
                if (EnsureProfile(spec)) profiles++;
                var profile = AssetDatabase.LoadAssetAtPath<LootContainerProfile>($"{ProfilesFolder}/{spec.Name}.asset");
                foreach (string prefab in spec.Prefabs)
                {
                    if (SetUpPrefab($"{PrefabsFolder}/{prefab}.prefab", profile, problems)) prefabs++;
                }
            }
            AssetDatabase.SaveAssets();

            string result = $"Loot containers: {tables} loot tables and {profiles} profiles created, {prefabs} prefabs set up";
            return problems.Count == 0 ? result : result + "\n - " + string.Join("\n - ", problems);
        }

        private static bool EnsureTable(TableSpec spec, List<string> problems)
        {
            string path = $"{LootFolder}/{spec.Name}.asset";
            if (AssetDatabase.LoadAssetAtPath<LootTable>(path) != null) return false;

            // Filled in before it becomes an asset, so the file is right from its first write.
            var table = ScriptableObject.CreateInstance<LootTable>();
            var so = new SerializedObject(table);
            var list = so.FindProperty("entries");
            foreach (var (itemName, weight, min, max) in spec.Entries)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/{itemName}.asset");
                if (item == null)
                {
                    problems.Add($"{spec.Name}: no item {itemName}");
                    continue;
                }
                list.arraySize++;
                var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative("item").objectReferenceValue = item;
                entry.FindPropertyRelative("weight").floatValue = weight;
                entry.FindPropertyRelative("minCount").intValue = min;
                entry.FindPropertyRelative("maxCount").intValue = max;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(table, path);
            return true;
        }

        private static bool EnsureProfile(ProfileSpec spec)
        {
            string path = $"{ProfilesFolder}/{spec.Name}.asset";
            if (AssetDatabase.LoadAssetAtPath<LootContainerProfile>(path) != null) return false;

            var profile = ScriptableObject.CreateInstance<LootContainerProfile>();
            var so = new SerializedObject(profile);
            so.FindProperty("title").stringValue = spec.Title;
            so.FindProperty("gridSize").vector2IntValue = spec.Grid;
            so.FindProperty("secondsPerItem").floatValue = spec.Seconds;
            so.FindProperty("loot").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LootTable>($"{LootFolder}/{spec.Table}.asset");
            so.FindProperty("emptyChance").floatValue = spec.Empty;
            so.FindProperty("minStacks").intValue = spec.Min;
            so.FindProperty("maxStacks").intValue = spec.Max;
            so.FindProperty("roomLootChance").floatValue = RoomLootChance;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(profile, path);
            return true;
        }

        // Adds the component where it is missing; a container that already has a profile keeps it.
        private static bool SetUpPrefab(string path, LootContainerProfile profile, List<string> problems)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                problems.Add($"no prefab at {path}");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                if (!root.TryGetComponent<LootContainer>(out var container))
                {
                    container = root.AddComponent<LootContainer>();
                    changed = true;
                }
                var so = new SerializedObject(container);
                var property = so.FindProperty("profile");
                if (property.objectReferenceValue == null && profile != null)
                {
                    property.objectReferenceValue = profile;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
