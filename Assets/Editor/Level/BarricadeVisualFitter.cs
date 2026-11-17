using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>Fits the visible pixels of a furniture pile to its existing push collider.</summary>
    public static class BarricadeVisualFitter
    {
        public static void Fit(GameObject root)
        {
            var collider = root.GetComponent<BoxCollider2D>();
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            if (collider == null || renderers.Length == 0) return;

            var group = root.transform.Find("Visuals");
            if (group == null)
            {
                group = new GameObject("Visuals") { layer = root.layer }.transform;
                group.SetParent(root.transform, false);
            }
            group.localPosition = Vector3.zero;
            group.localRotation = Quaternion.identity;
            group.localScale = Vector3.one;
            foreach (var sr in renderers)
                if (sr.transform.parent != group) sr.transform.SetParent(group, true);

            int baseOrder = renderers.Min(sr => sr.sortingOrder);
            var basePieces = renderers.Where(sr => sr.sortingOrder == baseOrder)
                .OrderBy(sr => sr.transform.localPosition.x).ToArray();
            float slot = collider.size.x / basePieces.Length;
            for (int i = 0; i < basePieces.Length; i++)
            {
                var sr = basePieces[i];
                var b = BoundsInRoot(root, sr);
                sr.transform.localScale *= slot * 1.04f / Mathf.Max(0.001f, b.size.x);
                b = BoundsInRoot(root, sr);
                float x = -collider.size.x * 0.5f + slot * (i + 0.5f);
                sr.transform.localPosition += new Vector3(x - b.center.x, -b.center.y, 0f);
            }
            foreach (var sr in renderers.Where(sr => sr.sortingOrder != baseOrder))
            {
                var b = BoundsInRoot(root, sr);
                float factor = Mathf.Min(slot * 0.8f / Mathf.Max(0.001f, b.size.x),
                    collider.size.y * 0.9f / Mathf.Max(0.001f, b.size.y));
                sr.transform.localScale *= factor;
                b = BoundsInRoot(root, sr);
                float edge = Mathf.Max(0f, (collider.size.x - b.size.x) * 0.5f);
                float x = Mathf.Clamp(b.center.x, -edge, edge);
                sr.transform.localPosition += new Vector3(x - b.center.x, 0.03f - b.center.y, 0f);
            }

            var total = VisualBounds(root);
            float sx = collider.size.x / Mathf.Max(0.001f, total.size.x);
            float sy = collider.size.y / Mathf.Max(0.001f, total.size.y);
            group.localScale = new Vector3(sx, sy, 1f);
            group.localPosition = new Vector3(collider.offset.x - total.center.x * sx,
                collider.offset.y - total.center.y * sy, 0f);
        }

        public static Bounds VisualBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0) return new Bounds();
            var result = BoundsInRoot(root, renderers[0]);
            for (int i = 1; i < renderers.Length; i++) result.Encapsulate(BoundsInRoot(root, renderers[i]));
            return result;
        }

        private static Bounds BoundsInRoot(GameObject root, SpriteRenderer renderer)
        {
            var local = OpaqueBounds(renderer.sprite);
            if (renderer.flipX) local.center = new Vector3(-local.center.x, local.center.y, 0f);
            if (renderer.flipY) local.center = new Vector3(local.center.x, -local.center.y, 0f);
            var matrix = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var bounds = new Bounds(matrix.MultiplyPoint3x4(local.min), Vector3.zero);
            bounds.Encapsulate(matrix.MultiplyPoint3x4(local.max));
            bounds.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(local.min.x, local.max.y, 0f)));
            bounds.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(local.max.x, local.min.y, 0f)));
            return bounds;
        }

        private static Bounds OpaqueBounds(Sprite sprite)
        {
            if (sprite == null) throw new InvalidOperationException("Barricade part has no sprite.");
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
                var pixels = texture.GetPixels32();
                int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
                for (int y = 0; y < texture.height; y++)
                    for (int x = 0; x < texture.width; x++)
                        if (pixels[y * texture.width + x].a > 16)
                        {
                            minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                            maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                        }
                if (maxX < 0) throw new InvalidOperationException("Barricade part is fully transparent.");
                var origin = sprite.rect.position + sprite.pivot;
                var center = new Vector3(((minX + maxX + 1) * 0.5f - origin.x) / sprite.pixelsPerUnit,
                    ((minY + maxY + 1) * 0.5f - origin.y) / sprite.pixelsPerUnit, 0f);
                var size = new Vector3((maxX - minX + 1) / sprite.pixelsPerUnit,
                    (maxY - minY + 1) / sprite.pixelsPerUnit, 0f);
                return new Bounds(center, size);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
