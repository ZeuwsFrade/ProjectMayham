using ProjectMayham.Items;
using ProjectMayham.Level;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Makes the vending machines work (Mayham > Level > Set Up Vending Machines): a loot table of what they hold
    /// (the food and drink of the needs) and a <see cref="VendingMachine"/> on the machine's prefab, which the rooms
    /// hold as nested instances, so every machine in every room has it. What exists is kept as it is.
    /// </summary>
    public static class VendingSetup
    {
        public const string PrefabPath = "Assets/Prefabs/Interactable/Vending-Machine.prefab";
        public const string TablePath = "Assets/Data/Level/Loot/Loot_Vending.asset";
        private const string CoinPath = "Assets/Data/Items/Coin.asset";
        private const string Consumables = "Assets/Data/Items/Consumables";

        // What a vending machine sells, and how often.
        private static readonly (string item, float weight)[] Stock =
        {
            ("Bar", 3f), ("Water", 3f), ("Coffee", 2f), ("CheeseFoam", 1f), ("Noodles", 1f), ("Sandwich", 1f),
        };

        [MenuItem("Mayham/Level/Set Up Vending Machines")]
        public static void SetUpMenu() => Debug.Log(SetUp());

        public static string SetUp()
        {
            var coin = AssetDatabase.LoadAssetAtPath<ItemDefinition>(CoinPath);
            if (coin == null) return $"no coin at {CoinPath}";

            var table = AssetDatabase.LoadAssetAtPath<LootTable>(TablePath);
            bool madeTable = table == null;
            if (madeTable)
            {
                table = ScriptableObject.CreateInstance<LootTable>();
                var so = new SerializedObject(table);
                var entries = so.FindProperty("entries");
                entries.arraySize = 0;
                foreach (var (name, weight) in Stock)
                {
                    var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{Consumables}/{name}.asset");
                    if (item == null) continue;
                    entries.arraySize++;
                    var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                    entry.FindPropertyRelative("item").objectReferenceValue = item;
                    entry.FindPropertyRelative("weight").floatValue = weight;
                    entry.FindPropertyRelative("minCount").intValue = 1;
                    entry.FindPropertyRelative("maxCount").intValue = 1;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool madeMachine;
            try
            {
                madeMachine = !root.TryGetComponent<VendingMachine>(out var machine);
                if (madeMachine)
                {
                    machine = root.AddComponent<VendingMachine>();
                    var so = new SerializedObject(machine);
                    so.FindProperty("stock").objectReferenceValue = table;
                    so.FindProperty("coin").objectReferenceValue = coin;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            return $"vending: table {(madeTable ? "made" : "kept")}, machine on the prefab {(madeMachine ? "added" : "kept")}";
        }
    }
}
