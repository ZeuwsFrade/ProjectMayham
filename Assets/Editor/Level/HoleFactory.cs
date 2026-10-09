using ProjectMayham.Level;
using ProjectMayham.World;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Builds the first version of the prefab of a hole in a wall. The hole lies along its X axis like a wall that
    /// runs left to right and takes the place of one wall tile: it blocks the way like the wall did, but not the
    /// view. Its picture reaches a little over the two wall tiles next to it, so the wall seems to go on up to the
    /// break. An existing prefab is never overwritten.
    /// </summary>
    public static class HoleFactory
    {
        public const string SpritePath = "Assets/Sprites/Level/Wall-Hole.png";
        // The same hole for a wall that runs up and down, drawn lying: see WallHole.
        public const string TurnedSpritePath = "Assets/Sprites/Level/Wall-Hole-Turned.png";

        private const string PrefabName = "Wall-Hole";
        private const int PixelsPerUnit = 64;   // the picture is drawn at the size of the wall tiles
        private const int SortingOrder = 2;     // over the walls and the windows, under the furniture

        [MenuItem("Mayham/Level/Create Missing Wall Holes")]
        public static void CreateMenu() => Debug.Log(Create(false));

        public static string Create(bool force)
        {
            string folder = TemplateBuilder.HolesFolder;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Holes");

            string path = $"{folder}/{PrefabName}.prefab";
            if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return "0 hole prefabs created";

            var sprite = LoadSprite(SpritePath);
            if (sprite == null) return $"HoleFactory: no sprite at {SpritePath}";

            var root = new GameObject(PrefabName) { layer = LayerMask.NameToLayer("Furniture") };
            root.AddComponent<BoxCollider2D>().size = Vector2.one;
            var hole = root.AddComponent<WallHole>();
            root.AddComponent<WrapEntity>();

            var rubble = new GameObject("Rubble") { layer = root.layer };
            rubble.transform.SetParent(root.transform, false);
            var renderer = rubble.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = SortingOrder;

            var so = new SerializedObject(hole);
            so.FindProperty("picture").objectReferenceValue = renderer;
            so.FindProperty("turnedPicture").objectReferenceValue = LoadSprite(TurnedSpritePath);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return "1 hole prefab created";
        }

        private static Sprite LoadSprite(string path)
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
    }
}
