using System.IO;
using ProjectMayham.Items;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>Menu commands for whoever writes quests.</summary>
    public static class QuestMenus
    {
        public const string QuestItemsFolder = "Assets/Data/Items/Quest";
        private const string PickupFolder = "Assets/Prefabs/Interactable";

        [MenuItem("Mayham/Quests/New Quest Item")]
        public static void NewQuestItemMenu()
        {
            Directory.CreateDirectory(QuestItemsFolder);
            AssetDatabase.Refresh();

            // The name of the asset is the id of the item in save files: it is chosen now, not by renaming later.
            string path = EditorUtility.SaveFilePanelInProject("Новый квестовый предмет", "QuestItem", "asset",
                "Имя файла станет идентификатором предмета: потом его лучше не менять.", QuestItemsFolder);
            if (string.IsNullOrEmpty(path)) return;

            var item = CreateQuestItem(Path.GetFileNameWithoutExtension(path), "Квестовый предмет");
            Selection.activeObject = item;
            EditorGUIUtility.PingObject(item);
        }

        /// <summary>
        /// Creates an item that exists only for a quest, or returns the one with this name: price 0 (so no trader
        /// buys it and it cannot be found twice), one per stack, with a pickup prefab of its own, and listed in the
        /// item database. Icon and pickup are copied from <paramref name="templateItem"/>; replace them later.
        /// </summary>
        public static ItemDefinition CreateQuestItem(string assetName, string displayName, string templateItem = "Papers")
        {
            Directory.CreateDirectory(QuestItemsFolder);
            AssetDatabase.Refresh();

            string itemPath = $"{QuestItemsFolder}/{assetName}.asset";
            string prefabPath = $"{PickupFolder}/{assetName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
            if (existing != null) return existing;

            var template = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Data/Items/{templateItem}.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            // A pickup of this name can belong to an item that was renamed since: that one is left alone.
            if (prefab != null && IsPickupOfAnItem(prefab))
            {
                prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
                prefab = null;
            }
            if (prefab == null)
            {
                AssetDatabase.CopyAsset($"{PickupFolder}/{templateItem}.prefab", prefabPath);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            // Filled in before it becomes an asset, so the file is right from its first write: Unity may read a new
            // asset back from disk while the pickup is being saved, and values set after creation would be lost.
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var so = new SerializedObject(item);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("icon").objectReferenceValue = template != null ? template.Icon : null;
            so.FindProperty("maxStack").intValue = 1;
            so.FindProperty("gridSize").vector2IntValue = template != null ? template.GridSize : Vector2Int.one;
            so.FindProperty("weight").floatValue = 0.2f;
            so.FindProperty("value").intValue = 0;
            so.FindProperty("holdable").boolValue = false;
            so.FindProperty("worldPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(item, itemPath);

            // The pickup is a copy of the template's: it has to hand out the new item, one piece.
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            contents.name = Path.GetFileNameWithoutExtension(prefabPath);
            var pickup = new SerializedObject(contents.GetComponent<ItemPickup>());
            pickup.FindProperty("item").objectReferenceValue = item;
            pickup.FindProperty("count").intValue = 1;
            pickup.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            ShelterSetup.RefreshItemDatabase();
            return item;
        }

        // True when some item already drops as this prefab.
        private static bool IsPickupOfAnItem(GameObject prefab)
        {
            var pickup = prefab.GetComponent<ItemPickup>();
            return pickup != null && pickup.Item != null && pickup.Item.WorldPrefab == prefab;
        }
    }
}
