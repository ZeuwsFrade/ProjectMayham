using UnityEngine;

namespace ProjectMayham.Vision
{
    /// <summary>
    /// Hides sprites that are outside of the player's sight.
    /// Furniture and walls do NOT need this component: they are always drawn (darkened like the walls).
    /// </summary>
    public class VisionTarget : MonoBehaviour
    {
        public enum PersistenceMode
        {
            /// <summary>Shown only while inside the field of view (enemies).</summary>
            OnlyWhileVisible,
            /// <summary>Hidden until first seen, then stays drawn forever (interactable objects).</summary>
            RememberOnceSeen
        }

        [SerializeField] private PersistenceMode persistence = PersistenceMode.OnlyWhileVisible;
        [SerializeField] private float fadeSpeed = 8f;
        [Tooltip("Optional offset of the point that is tested for visibility.")]
        [SerializeField] private Vector2 sightOffset;

        private SpriteRenderer[] renderers;
        private float[] baseAlpha;
        private float visibility;
        private bool discovered;

        public bool IsVisible { get; private set; }
        public bool IsDiscovered => discovered;

        private void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>();
            baseAlpha = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseAlpha[i] = renderers[i].color.a;
            ApplyAlpha();
        }

        private void LateUpdate()
        {
            bool needsTest = persistence == PersistenceMode.OnlyWhileVisible || !discovered;
            if (needsTest)
            {
                IsVisible = TestVisible(VisionCone.Active);
                if (IsVisible) discovered = true;
            }
            else IsVisible = false; // already discovered: no more sight tests needed

            bool shown = persistence == PersistenceMode.RememberOnceSeen ? discovered : IsVisible;
            float target = shown ? 1f : 0f;
            if (Mathf.Approximately(visibility, target)) return;

            visibility = Mathf.MoveTowards(visibility, target, fadeSpeed * Time.deltaTime);
            ApplyAlpha();
        }

        /// <summary>
        /// The object counts as seen when its centre or any inset corner of its bounds is visible.
        /// Its own colliders never block the test, so solid objects (cabinets) can be discovered.
        /// </summary>
        private bool TestVisible(VisionCone cone)
        {
            if (cone == null || renderers.Length == 0) return false;

            Bounds b = renderers[0].bounds;
            Vector2 c = (Vector2)b.center + sightOffset;
            Vector2 e = b.extents * 0.7f;

            return cone.IsPointVisible(c, transform)
                || cone.IsPointVisible(c + new Vector2(e.x, e.y), transform)
                || cone.IsPointVisible(c + new Vector2(-e.x, e.y), transform)
                || cone.IsPointVisible(c + new Vector2(e.x, -e.y), transform)
                || cone.IsPointVisible(c + new Vector2(-e.x, -e.y), transform);
        }

        private void ApplyAlpha()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Color c = renderers[i].color;
                c.a = baseAlpha[i] * visibility;
                renderers[i].color = c;
            }
        }
    }
}
