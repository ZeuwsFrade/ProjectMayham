using ProjectMayham.Player;
using UnityEngine;

namespace ProjectMayham.Vision
{
    /// <summary>
    /// Computes the view cone geometry: smoothly follows the cursor and casts rays against obstacles.
    /// Rays are only re-cast when the origin or the angle changes beyond a threshold.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class VisionCone : MonoBehaviour
    {
        public static VisionCone Active { get; private set; }

        [SerializeField] private VisionSettings settings;

        private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        private PlayerController player;
        private ContactFilter2D filter;

        private float currentAngle;
        private float lastAngle;
        private Vector2 lastOrigin;
        private bool initialized;

        public VisionSettings Settings => settings;
        /// <summary>Incremented every time the geometry is rebuilt.</summary>
        public int Version { get; private set; }
        public Vector2 Origin { get; private set; }
        public float Angle => currentAngle;

        /// <summary>World-space end point of every cone ray (hit point or max radius).</summary>
        public Vector2[] ConePoints { get; private set; } = System.Array.Empty<Vector2>();
        /// <summary>Angular brightness weight (0..1) of every cone ray.</summary>
        public float[] ConeWeights { get; private set; } = System.Array.Empty<float>();
        /// <summary>End points of the 360° close-range rays.</summary>
        public Vector2[] AmbientPoints { get; private set; } = System.Array.Empty<Vector2>();

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            Active = this;
            initialized = false;
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
        }

        private void LateUpdate()
        {
            if (settings == null) return;

            Vector2 origin = transform.position;
            Vector2 aim = player.AimPoint - origin;
            float targetAngle = aim.sqrMagnitude > 0.0001f ? Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg : currentAngle;

            if (!initialized) currentAngle = targetAngle;
            float t = 1f - Mathf.Exp(-settings.rotationSmoothing * Time.deltaTime);
            currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, t);

            bool moved = (origin - lastOrigin).sqrMagnitude > settings.positionThreshold * settings.positionThreshold;
            bool turned = Mathf.Abs(Mathf.DeltaAngle(currentAngle, lastAngle)) > settings.angleThreshold;
            if (!initialized || moved || turned)
            {
                BuildFilter();
                Origin = origin;
                if (!initialized || moved) RebuildAmbient();
                RebuildCone();

                lastOrigin = origin;
                lastAngle = currentAngle;
                initialized = true;
                Version++;
            }
        }

        private void BuildFilter()
        {
            filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(settings.obstacleMask);
        }

        private Vector2 CastRay(Vector2 dir, float radius)
        {
            int n = Physics2D.Raycast(Origin, dir, filter, hits, radius);
            return n > 0 ? hits[0].point : Origin + dir * radius;
        }

        private void RebuildCone()
        {
            int count = settings.rayCount;
            if (ConePoints.Length != count)
            {
                ConePoints = new Vector2[count];
                ConeWeights = new float[count];
            }

            float half = settings.coneAngle * 0.5f;
            float soft = settings.softEdgeAngle * 0.5f;
            float spread = Mathf.Min(half + soft, 180f);

            for (int i = 0; i < count; i++)
            {
                float offset = Mathf.Lerp(-spread, spread, i / (float)(count - 1));
                float rad = (currentAngle + offset) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                ConePoints[i] = CastRay(dir, settings.radius);
                ConeWeights[i] = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half - soft, half + soft, Mathf.Abs(offset)));
            }
        }

        private void RebuildAmbient()
        {
            int count = settings.ambientRadius > 0f ? settings.ambientRayCount : 0;
            if (AmbientPoints.Length != count) AmbientPoints = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                float rad = i * Mathf.PI * 2f / count;
                AmbientPoints[i] = CastRay(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)), settings.ambientRadius);
            }
        }

        /// <summary>
        /// True when a world point is inside the current field of view and not blocked by obstacles.
        /// Colliders under <paramref name="ignoreRoot"/> (the object being tested) never block the line.
        /// </summary>
        public bool IsPointVisible(Vector2 point, Transform ignoreRoot = null)
        {
            if (settings == null) return false;

            Vector2 origin = transform.position;
            Vector2 d = point - origin;
            float dist = d.magnitude;

            bool inAmbient = dist <= settings.ambientRadius;
            bool inCone = dist <= settings.radius &&
                          Mathf.Abs(Mathf.DeltaAngle(currentAngle, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg)) <= settings.coneAngle * 0.5f;
            if (!inAmbient && !inCone) return false;

            int count = Physics2D.Linecast(origin, point, filter, hits);
            for (int i = 0; i < count; i++)
            {
                if (ignoreRoot == null || !hits[i].collider.transform.IsChildOf(ignoreRoot)) return false;
            }
            return true;
        }
    }
}
