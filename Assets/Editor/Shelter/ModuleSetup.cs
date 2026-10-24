using System.IO;
using ProjectMayham.Items;
using ProjectMayham.Shelter;
using ProjectMayham.World;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Makes the first module of the shelter (Mayham > Shelter > Set Up Modules): the paper press, which turns books
    /// into paper over a raid. Its prefab is the office printer with a <see cref="ModuleConverter"/> on it; the module
    /// asset lies in Resources/Modules, where the game finds it. What exists is kept as it is.
    /// </summary>
    public static class ModuleSetup
    {
        public const string ModulesFolder = "Assets/Resources/Modules";
        public const string PressPath = ModulesFolder + "/PaperPress.asset";
        public const string PressPrefabPath = "Assets/Prefabs/Shelter/PaperPress.prefab";
        private const string PrinterPath = "Assets/Prefabs/Interactable/Big-Office-Printer.prefab";
        private const string Items = "Assets/Data/Items";

        [MenuItem("Mayham/Shelter/Set Up Modules")]
        public static void SetUpMenu() => Debug.Log(SetUp());

        public static string SetUp()
        {
            bool made = AssetDatabase.LoadAssetAtPath<ShelterModuleDefinition>(PressPath) == null;
            var press = EnsurePress();
            return press == null ? "modules: the press could not be made (see the console)" : $"modules: the paper press {(made ? "made" : "kept")}";
        }

        /// <summary>The paper press module, made with its prefab when it is not there.</summary>
        public static ShelterModuleDefinition EnsurePress()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShelterModuleDefinition>(PressPath);
            if (existing != null) return existing;

            var books = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{Items}/Books.asset");
            var papers = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{Items}/Papers.asset");
            var folders = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{Items}/Folders.asset");
            var mop = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{Items}/Mop.asset");
            var coin = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{Items}/Coin.asset");
            if (books == null || papers == null || folders == null || mop == null || coin == null)
            {
                Debug.LogError("ModuleSetup: books, papers, folders, the mop and the coin have to exist.");
                return null;
            }

            var prefab = EnsurePressPrefab(books, papers);
            if (prefab == null) return null;

            Directory.CreateDirectory(ModulesFolder);
            var module = ScriptableObject.CreateInstance<ShelterModuleDefinition>();
            var so = new SerializedObject(module);
            so.FindProperty("displayName").stringValue = "Макулатурный пресс";
            so.FindProperty("description").stringValue =
                "Перемалывает книги в бумагу: стопка книг — пять пачек бумаги. Загрузи книги, а к возвращению из рейда бумага готова.";
            so.FindProperty("prefab").objectReferenceValue = prefab;
            var cost = so.FindProperty("cost");
            cost.arraySize = 0;
            AddCost(cost, folders, 3);
            AddCost(cost, mop, 1);
            AddCost(cost, coin, 5);
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(module, PressPath);
            AssetDatabase.SaveAssets();
            ModuleCatalog.Reset();
            return module;
        }

        // The printer of the offices, with the converter instead of what it has for the maze.
        private static GameObject EnsurePressPrefab(ItemDefinition books, ItemDefinition papers)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PressPrefabPath);
            if (existing != null) return existing;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrinterPath) == null)
            {
                Debug.LogError($"ModuleSetup: no printer at {PrinterPath}.");
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(PressPrefabPath));
            var root = PrefabUtility.LoadPrefabContents(PrinterPath);
            try
            {
                root.name = "PaperPress";
                // The shelter does not wrap around.
                foreach (var wrap in root.GetComponentsInChildren<WrapEntity>(true)) Object.DestroyImmediate(wrap);
                var converter = root.AddComponent<ModuleConverter>();
                var so = new SerializedObject(converter);
                so.FindProperty("input").objectReferenceValue = books;
                so.FindProperty("output").objectReferenceValue = papers;
                so.FindProperty("outputPerInput").intValue = 5;
                so.FindProperty("capacity").intValue = 10;
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, PressPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddCost(SerializedProperty cost, ItemDefinition item, int count)
        {
            cost.arraySize++;
            var entry = cost.GetArrayElementAtIndex(cost.arraySize - 1);
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("count").intValue = count;
        }
    }
}
