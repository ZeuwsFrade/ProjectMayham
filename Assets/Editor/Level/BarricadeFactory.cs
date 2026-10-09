using System.Collections.Generic;
using ProjectMayham.Level;
using ProjectMayham.World;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Builds the first versions of the barricade prefabs out of the furniture sprites. A barricade lies along its X
    /// axis and is a little narrower than a corridor, so it slides between the walls but leaves no gap to squeeze
    /// through. Existing prefabs are never overwritten.
    /// </summary>
    public static class BarricadeFactory
    {
        private const float Width = RoomSizes.CellTiles - 1 - 0.2f;   // the floor of a corridor less a little play
        private const float Thickness = 0.85f;

        // Heavy and slow to push: with the player's acceleration this gives well under a tile per second.
        private const float Mass = 5f;
        private const float Damping = 4f;

        private class Piece
        {
            public string Folder, Sprite;
            public Vector2 Position;
            public float Angle, Scale = 1f;
            public int Order = 5;
        }

        private static Piece P(string folder, string sprite, float x, float y, float angle = 0f, float scale = 1f, int order = 5) =>
            new Piece { Folder = folder, Sprite = sprite, Position = new Vector2(x, y), Angle = angle, Scale = scale, Order = order };

        private static readonly Dictionary<string, Piece[]> Barricades = new Dictionary<string, Piece[]>
        {
            { "Barricade-Crates", new[]
                {
                    P("Furniture", "Crate", -0.93f, 0f, 0f, 0.9f),
                    P("Furniture", "Box", 0f, 0f, 0f, 0.9f),
                    P("Furniture", "Crate", 0.93f, 0f, 0f, 0.9f),
                    P("Furniture", "Box", -0.45f, 0.05f, 24f, 0.55f, 6),
                } },
            { "Barricade-Cabinets", new[]
                {
                    P("Interactable", "Filing-Cabinet-Small", -0.93f, 0f, 90f, 0.9f),
                    P("Furniture", "Bookshelf", 0f, 0f, -90f, 0.9f),
                    P("Interactable", "Filing-Cabinet-Small", 0.93f, 0f, -90f, 0.9f),
                    P("Furniture", "Chair", 0.45f, 0.05f, 150f, 0.7f, 6),
                } },
            { "Barricade-Tables", new[]
                {
                    P("Furniture", "Small-Table", -0.93f, 0f, 180f, 0.95f),
                    P("Furniture", "Small-Table", 0f, 0f, 0f, 0.95f),
                    P("Furniture", "Crate", 0.93f, 0f, 8f, 0.9f),
                    P("Furniture", "Chair-2", -0.5f, 0.02f, -115f, 0.75f, 6),
                } },
        };

        [MenuItem("Mayham/Level/Create Missing Barricades")]
        public static void CreateMenu() => Debug.Log(Create(false));

        public static string Create(bool force)
        {
            string folder = TemplateBuilder.BarricadesFolder;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Barricades");

            int created = 0;
            foreach (var pair in Barricades)
            {
                string path = $"{folder}/{pair.Key}.prefab";
                if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;

                var root = new GameObject(pair.Key) { layer = LayerMask.NameToLayer("Furniture") };
                var body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.mass = Mass;
                body.linearDamping = Damping;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                root.AddComponent<BoxCollider2D>().size = new Vector2(Width, Thickness);
                root.AddComponent<Barricade>();
                root.AddComponent<WrapEntity>();

                foreach (var piece in pair.Value)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{piece.Folder}/{piece.Sprite}.prefab");
                    var sprite = source != null ? source.GetComponentInChildren<SpriteRenderer>().sprite : null;
                    if (sprite == null) { Debug.LogWarning($"BarricadeFactory: no sprite for {piece.Sprite}"); continue; }

                    var child = new GameObject(piece.Sprite) { layer = root.layer };
                    child.transform.SetParent(root.transform, false);
                    child.transform.localPosition = piece.Position;
                    child.transform.localRotation = Quaternion.Euler(0f, 0f, piece.Angle);
                    child.transform.localScale = Vector3.one * piece.Scale;
                    var renderer = child.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sortingOrder = piece.Order;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                created++;
            }
            AssetDatabase.SaveAssets();
            return $"{created} barricade prefabs created";
        }
    }
}
