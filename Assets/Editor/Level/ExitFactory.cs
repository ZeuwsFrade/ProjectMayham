using ProjectMayham.Level;
using ProjectMayham.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Makes the raid exit and the wall map (Mayham > Level > Create Exit and Wall Maps): their prefabs from the
    /// placeholder sprites, and gives them to the raid generator of the open scene. Prefabs that exist are kept.
    /// </summary>
    public static class ExitFactory
    {
        public const string ExitPrefabPath = "Assets/Prefabs/Interactable/RaidExit.prefab";
        public const string MapPrefabPath = "Assets/Prefabs/Furniture/WallMap.prefab";
        private const string ExitSprite = "Assets/Sprites/Level/Exit.png";
        private const string MapSprite = "Assets/Sprites/Level/WallMap.png";

        [MenuItem("Mayham/Level/Create Exit and Wall Maps")]
        public static void CreateMenu() => Debug.Log(Create());

        public static string Create()
        {
            var exitSprite = Sprite(ExitSprite);
            var mapSprite = Sprite(MapSprite);
            if (exitSprite == null || mapSprite == null) return $"no sprites at {ExitSprite} and {MapSprite}";

            // The exit lies on the floor (under what is dropped on it); the map hangs on the wall, over the floor props.
            var exit = Prefab<RaidExit>(ExitPrefabPath, exitSprite, 1);
            var map = Prefab<WallMap>(MapPrefabPath, mapSprite, 6);

            var generator = Object.FindFirstObjectByType<RaidGenerator>();
            if (generator == null) return "prefabs made; open a raid scene and run this again to give them to its generator";
            var so = new SerializedObject(generator);
            so.FindProperty("exitPrefab").objectReferenceValue = exit;
            so.FindProperty("wallMapPrefab").objectReferenceValue = map;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            return $"exit and wall map given to the generator of {generator.gameObject.scene.name} (save the scene)";
        }

        private static GameObject Prefab<T>(string path, Sprite sprite, int order) where T : Component
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            try
            {
                var body = root.AddComponent<SpriteRenderer>();
                body.sprite = sprite;
                body.sortingOrder = order;
                root.AddComponent<T>();
                // The level wraps around: it is drawn at the copy of its place nearest to the player.
                root.AddComponent<WrapEntity>();
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Pixel art like the other props: 16 pixels a tile, no smoothing.
        private static Sprite Sprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, 16f)
                || importer.filterMode != FilterMode.Point || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
