using UnityEngine;

namespace ProjectMayham.Vision
{
    /// <summary>
    /// Put on enemies (layer Enemy): the sprites are only shown while the point is inside the player's sight.
    /// </summary>
    public class VisionTarget : MonoBehaviour
    {
        [SerializeField] private float fadeSpeed = 8f;
        [Tooltip("Optional offset of the point that is tested for visibility.")]
        [SerializeField] private Vector2 sightOffset;

        private SpriteRenderer[] renderers;
        private float[] baseAlpha;
        private float visibility;

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>();
            baseAlpha = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseAlpha[i] = renderers[i].color.a;
            ApplyAlpha();
        }

        private void LateUpdate()
        {
            var cone = VisionCone.Active;
            IsVisible = cone != null && cone.IsPointVisible((Vector2)transform.position + sightOffset);

            float target = IsVisible ? 1f : 0f;
            if (Mathf.Approximately(visibility, target)) return;
            visibility = Mathf.MoveTowards(visibility, target, fadeSpeed * Time.deltaTime);
            ApplyAlpha();
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
