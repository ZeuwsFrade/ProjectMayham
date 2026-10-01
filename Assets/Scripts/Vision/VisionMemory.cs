using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectMayham.Vision
{
    /// <summary>
    /// Persistent world-aligned mask of areas the player has already seen.
    /// Seen areas are stamped with the vision mesh and slowly subtracted over time.
    /// </summary>
    public sealed class VisionMemory : System.IDisposable
    {
        private static readonly int VisionVP = Shader.PropertyToID("_VisionVP");
        private static readonly int FalloffStart = Shader.PropertyToID("_VisionFalloffStart");
        private static readonly int FadeAmount = Shader.PropertyToID("_VisionFadeAmount");

        // The 8-bit texture cannot represent a smaller step than this.
        private const float MinFadeStep = 2f / 255f;

        private readonly VisionSettings settings;
        private readonly Material maskMaterial;
        private readonly Matrix4x4 viewProjection;
        private readonly Mesh fullScreenQuad;
        private float fadeDebt;
        private bool needsClear = true;

        public RenderTexture Texture { get; }
        /// <summary>xy = world min corner, zw = world size.</summary>
        public Vector4 WorldRect { get; }

        public VisionMemory(VisionSettings settings, Material maskMaterial)
        {
            this.settings = settings;
            this.maskMaterial = maskMaterial;

            float size = settings.memoryWorldSize;
            Vector2 min = settings.memoryWorldCenter - Vector2.one * (size * 0.5f);
            WorldRect = new Vector4(min.x, min.y, size, size);

            int texels = Mathf.Clamp(Mathf.RoundToInt(size * settings.memoryTexelsPerUnit), 64, 4096);
            Texture = new RenderTexture(texels, texels, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear)
            {
                name = "VisionMemory",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Texture.Create();

            Matrix4x4 proj = Matrix4x4.Ortho(min.x, min.x + size, min.y, min.y + size, -1f, 1f);
            viewProjection = GL.GetGPUProjectionMatrix(proj, true);

            fullScreenQuad = new Mesh { name = "VisionFadeQuad", hideFlags = HideFlags.HideAndDontSave };
            fullScreenQuad.vertices = new[]
            {
                new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0)
            };
            fullScreenQuad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            fullScreenQuad.bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
        }

        /// <summary>Adds the currently visible area to the memory.</summary>
        public void Stamp(CommandBuffer cmd, Mesh visionMesh)
        {
            BindTarget(cmd);
            cmd.SetGlobalMatrix(VisionVP, viewProjection);
            cmd.SetGlobalFloat(FalloffStart, settings.radialFalloffStart);
            cmd.DrawMesh(visionMesh, Matrix4x4.identity, maskMaterial, 0, 0);
        }

        /// <summary>Accumulates elapsed time and subtracts from the texture once the step is large enough.</summary>
        public void Fade(CommandBuffer cmd, float deltaTime)
        {
            if (settings.memoryForgetSeconds <= 0f) return;

            fadeDebt += deltaTime / settings.memoryForgetSeconds;
            if (fadeDebt < MinFadeStep) return;

            BindTarget(cmd);
            cmd.SetGlobalFloat(FadeAmount, fadeDebt);
            cmd.DrawMesh(fullScreenQuad, Matrix4x4.identity, maskMaterial, 0, 1);
            fadeDebt = 0f;
        }

        private void BindTarget(CommandBuffer cmd)
        {
            cmd.SetRenderTarget(Texture);
            if (needsClear)
            {
                cmd.ClearRenderTarget(false, true, Color.clear);
                needsClear = false;
            }
        }

        public void Dispose()
        {
            if (Texture != null) Texture.Release();
            if (Texture != null) Object.Destroy(Texture);
            if (fullScreenQuad != null) Object.Destroy(fullScreenQuad);
        }
    }
}

