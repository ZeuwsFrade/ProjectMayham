using System;
using System.IO;
using System.Linq;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Quests;
using ProjectMayham.Shelter;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>Creates the coffee module and its quest without rebuilding scenes or replacing existing content.</summary>
    public static class CoffeeMachineSetup
    {
        public const string ModulePath = "Assets/Resources/Modules/CoffeeMachine.asset";
        public const string BeansPath = "Assets/Data/Items/CoffeeBeans.asset";
        public const string QuestPath = "Assets/Resources/Quests/Intern/Intern_CoffeeMachine.asset";
        public const string MachineSprite = "Assets/Sprites/Shelter/CoffeeMachine.png";
        public const string BeansSprite = "Assets/Sprites/Items/CoffeeBeans.png";
        public const string PrefabPath = "Assets/Prefabs/Shelter/CoffeeMachine.prefab";

        [MenuItem("Mayham/Shelter/Set Up Coffee Machine")]
        public static void Menu() => Debug.Log(SetUp());

        public static string SetUp()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play mode before setup.");
            var machineSprite = ImportSprite(MachineSprite);
            var beansSprite = ImportSprite(BeansSprite);
            var water = Need<ItemDefinition>("Assets/Data/Items/Consumables/Water.asset");
            var coffee = Need<ItemDefinition>("Assets/Data/Items/Consumables/Coffee.asset");
            var mug = Need<ItemDefinition>("Assets/Data/Items/Mug.asset");
            var papers = Need<ItemDefinition>("Assets/Data/Items/Papers.asset");
            var folders = Need<ItemDefinition>("Assets/Data/Items/Folders.asset");
            var coin = Need<ItemDefinition>("Assets/Data/Items/Coin.asset");
            var beans = Ensure<ItemDefinition>(BeansPath, so =>
            {
                so.FindProperty("displayName").stringValue = "Кофейные зёрна";
                so.FindProperty("icon").objectReferenceValue = beansSprite;
                so.FindProperty("maxStack").intValue = 3;
                so.FindProperty("weight").floatValue = .25f;
                so.FindProperty("value").intValue = 12;
                so.FindProperty("holdable").boolValue = true;
                so.FindProperty("hands").intValue = (int)ItemDefinition.HandUse.One;
            });
            EnsureBeansPickup(beans, beansSprite);
            var prefab = EnsureMachinePrefab(machineSprite, water, beans, coffee);
            var module = Ensure<ShelterModuleDefinition>(ModulePath, so =>
            {
                so.FindProperty("displayName").stringValue = "Кофемашина";
                so.FindProperty("description").stringValue = "Готовит один кофе за рейд: тратит одну воду и одно использование зёрен. Порции зёрен хватает на три кофе. Бак вмещает три воды.";
                so.FindProperty("prefab").objectReferenceValue = prefab;
                Amounts(so.FindProperty("cost"), (mug, 2), (folders, 2), (coin, 8));
            });
            var giver = Need<QuestGiverDefinition>("Assets/Resources/Quests/Givers/Intern.asset");
            var rescue = Need<QuestDefinition>("Assets/Resources/Quests/Intern/Intern_00_Rescue.asset");
            Ensure<QuestDefinition>(QuestPath, so =>
            {
                so.FindProperty("giver").objectReferenceValue = giver;
                var requires = so.FindProperty("requires");
                requires.arraySize = 1;
                requires.GetArrayElementAtIndex(0).objectReferenceValue = rescue;
                so.FindProperty("order").intValue = 4;
                so.FindProperty("title").stringValue = "Кофе для выживших";
                so.FindProperty("offerText").stringValue = "Кружка есть. Стены есть. А кофе всё ещё нет. Так жить нельзя.\n\nЯ набросал чертёж кофемашины. Принеси две кружки, две бутылки воды и три пачки бумаги: проверю подачу воды и дорисую схему. За это отдам чертёж и порцию зёрен на первые три приготовления.";
                so.FindProperty("reminderText").stringValue = "Две кружки, две воды и три пачки бумаги. Чертёж почти готов. Зёрна потом ищи в комнатах отдыха и на складе.";
                so.FindProperty("completedText").stringValue = "Работает! Ну, на бумаге точно. Держи чертёж и зёрна.\n\nПострой кофемашину на свободном месте для модуля. Залей воду и засыпь порцию зёрен: она даст три использования. За каждый рейд машина приготовит один кофе, если есть вода и зёрна.";
                var objectives = so.FindProperty("objectives");
                objectives.arraySize = 3;
                Objective(objectives.GetArrayElementAtIndex(0), mug, 2);
                Objective(objectives.GetArrayElementAtIndex(1), water, 2);
                Objective(objectives.GetArrayElementAtIndex(2), papers, 3);
                so.FindProperty("rewardBlueprint").objectReferenceValue = module;
                so.FindProperty("rewardLoyalty").intValue = 10;
                Amounts(so.FindProperty("rewardItems"), (beans, 1));
            });
            AddLoot("Assets/Data/Level/Loot/Loot_BreakRoom.asset", beans, 2);
            AddLoot("Assets/Data/Level/Loot/Loot_Storage.asset", beans, 1);
            AddLoot("Assets/Data/Level/Loot/Loot_Cabinet.asset", beans, 1);
            AssetDatabase.SaveAssets();
            ShelterSetup.RefreshItemDatabase();
            ModuleCatalog.Reset();
            QuestCatalog.Reset();
            return "Coffee machine, beans, pickup, Intern quest and raid loot are ready.";
        }

        private static Sprite ImportSprite(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The new sprite is missing.", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return Need<Sprite>(path);
        }

        private static void EnsureBeansPickup(ItemDefinition beans, Sprite sprite)
        {
            const string path = "Assets/Prefabs/Items/CoffeeBeans.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var root = new GameObject("CoffeeBeans");
                try
                {
                    var renderer = root.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sortingOrder = 5;
                    root.transform.localScale = Vector3.one * (.6f / sprite.bounds.size.y);
                    root.AddComponent<ItemPickup>().Set(beans, 1);
                    root.AddComponent<ProjectMayham.World.WrapEntity>();
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            else if (prefab.GetComponent<ProjectMayham.World.WrapEntity>() == null)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.AddComponent<ProjectMayham.World.WrapEntity>();
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var so = new SerializedObject(beans);
            so.FindProperty("worldPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(beans);
        }

        private static GameObject EnsureMachinePrefab(Sprite sprite, ItemDefinition water, ItemDefinition beans, ItemDefinition coffee)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
            var root = new GameObject("CoffeeMachine");
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 5;
                var press = AssetDatabase.LoadAssetAtPath<GameObject>(ModuleSetup.PressPrefabPath);
                if (press != null) renderer.sharedMaterial = press.GetComponent<SpriteRenderer>().sharedMaterial;
                float scale = 1.5f / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
                root.transform.localScale = Vector3.one * scale;
                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(1.2f / scale, 1.2f / scale);
                collider.offset = Vector2.zero;
                var machine = root.AddComponent<CoffeeMachine>();
                var so = new SerializedObject(machine);
                so.FindProperty("water").objectReferenceValue = water;
                so.FindProperty("beans").objectReferenceValue = beans;
                so.FindProperty("coffee").objectReferenceValue = coffee;
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static T Need<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);

        private static T Ensure<T>(string path, Action<SerializedObject> fill) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            var so = new SerializedObject(asset);
            fill(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Amounts(SerializedProperty list, params (ItemDefinition item, int count)[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("item").objectReferenceValue = values[i].item;
                entry.FindPropertyRelative("count").intValue = values[i].count;
            }
        }

        private static void Objective(SerializedProperty entry, ItemDefinition item, int count)
        {
            entry.FindPropertyRelative("kind").intValue = (int)QuestObjectiveKind.Deliver;
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("count").intValue = count;
        }

        private static void AddLoot(string path, ItemDefinition beans, float weight)
        {
            var table = Need<LootTable>(path);
            if (table.Entries.Any(e => e.item == beans)) return;
            var so = new SerializedObject(table);
            var entries = so.FindProperty("entries");
            int i = entries.arraySize++;
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("item").objectReferenceValue = beans;
            entry.FindPropertyRelative("weight").floatValue = weight;
            entry.FindPropertyRelative("minCount").intValue = 1;
            entry.FindPropertyRelative("maxCount").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);
        }
    }
}
