using UnityEngine;

namespace ProjectMayham.Vision
{
    [CreateAssetMenu(menuName = "ProjectMayham/Vision Settings", fileName = "VisionSettings")]
    public class VisionSettings : ScriptableObject
    {
        [Header("Cone")]
        [Tooltip("Full angle of the view cone in degrees (the 50% point of the soft edge).")]
        [Range(10f, 360f)] public float coneAngle = 90f;
        [Min(0.5f)] public float radius = 9f;
        [Tooltip("Width of the soft angular falloff at the cone borders, degrees.")]
        [Range(0f, 45f)] public float softEdgeAngle = 10f;
        [Tooltip("Fraction of the radius where the distance falloff starts (1 = no falloff).")]
        [Range(0.05f, 1f)] public float radialFalloffStart = 0.6f;
        [Range(16, 256)] public int rayCount = 96;
        [Tooltip("Cone rotation smoothing. Higher = snappier, lower = lazier.")]
        [Min(0.1f)] public float rotationSmoothing = 14f;
        [Tooltip("Only colliders on these layers block sight. Triggers are always ignored.")]
        public LayerMask obstacleMask;

        [Header("Close-range awareness (360°)")]
        [Min(0f)] public float ambientRadius = 1.6f;
        [Range(0f, 1f)] public float ambientIntensity = 0.35f;
        [Range(8, 128)] public int ambientRayCount = 48;

        [Header("Darkness")]
        [Tooltip("Multiplier applied to everything outside of sight. Not pure black so silhouettes stay readable.")]
        public Color darknessColor = new Color(0.05f, 0.05f, 0.08f, 1f);

        [Header("Memory")]
        public bool memoryEnabled = true;
        [Range(0f, 1f)] public float memoryBrightness = 0.3f;
        [Tooltip("Seconds until a seen area is fully forgotten. 0 = never forget.")]
        [Min(0f)] public float memoryForgetSeconds = 40f;
        public Vector2 memoryWorldCenter = Vector2.zero;
        [Min(8f)] public float memoryWorldSize = 64f;
        [Range(2, 16)] public int memoryTexelsPerUnit = 8;

        [Header("Performance")]
        [Tooltip("Rays are re-cast only when the origin moves further than this.")]
        [Min(0f)] public float positionThreshold = 0.02f;
        [Tooltip("Rays are re-cast only when the cone turns more than this (degrees).")]
        [Min(0f)] public float angleThreshold = 0.5f;
        [Tooltip("Resolution of the visibility mask relative to the screen.")]
        [Range(0.25f, 1f)] public float maskResolutionScale = 0.5f;
    }
}
