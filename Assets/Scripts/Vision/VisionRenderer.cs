using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectMayham.Vision
{
    /// <summary>
    /// Turns the <see cref="VisionCone"/> geometry into a mesh, renders it into a screen-space mask,
    /// and draws a camera-attached multiply quad that darkens everything outside of the mask.
    /// Works with the URP 2D renderer without needing a custom renderer feature.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class VisionRenderer : MonoBehaviour
    {
        private static readonly int VisionVP = Shader.PropertyToID("_VisionVP");
        private static readonly int FalloffStart = Shader.PropertyToID("_VisionFalloffStart");
        private static readonly int MaskTex = Shader.PropertyToID("_VisionMask");
        private static readonly int MemoryTex = Shader.PropertyToID("_VisionMemory");
        private static readonly int CamRect = Shader.PropertyToID("_CamRect");
        private static readonly int MemoryRect = Shader.PropertyToID("_MemoryRect");
        private static readonly int DarkColor = Shader.PropertyToID("_DarkColor");
        private static readonly int MemoryBrightness = Shader.PropertyToID("_MemoryBrightness");
        private static readonly int MemoryWrap = Shader.PropertyToID("_MemoryWrap");

        [SerializeField] private VisionSettings settings;
        [SerializeField] private VisionCone cone;
        [SerializeField] private Shader compositeShader;
        [SerializeField] private Shader maskShader;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        private Camera cam;
        private Material compositeMaterial;
        private Material maskMaterial;
        private RenderTexture maskTexture;
        private Mesh visionMesh;
        private CommandBuffer cmd;
        private VisionMemory memory;
        private MeshRenderer quadRenderer;
        private Transform quad;

        private int lastVersion = -1;
        private Vector3 lastCamPosition;
        private float lastOrthoSize;
        private bool maskDirty;

        private void OnEnable()
        {
            cam = GetComponent<Camera>();
            if (cone == null) cone = VisionCone.Active;
            if (settings == null && cone != null) settings = cone.Settings;
            if (settings == null || compositeShader == null || maskShader == null)
            {
                Debug.LogError("VisionRenderer is missing settings or shaders.", this);
                enabled = false;
                return;
            }

            compositeMaterial = new Material(compositeShader) { hideFlags = HideFlags.HideAndDontSave };
            maskMaterial = new Material(maskShader) { hideFlags = HideFlags.HideAndDontSave };
            visionMesh = new Mesh { name = "VisionMesh", hideFlags = HideFlags.HideAndDontSave };
            visionMesh.MarkDynamic();
            cmd = new CommandBuffer { name = "Vision" };

            if (settings.memoryEnabled) memory = new VisionMemory(settings, maskMaterial);
            CreateQuad();

            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;

            memory?.Dispose();
            memory = null;
            if (maskTexture != null) { maskTexture.Release(); Destroy(maskTexture); maskTexture = null; }
            if (quad != null) Destroy(quad.gameObject);
            if (visionMesh != null) Destroy(visionMesh);
            if (compositeMaterial != null) Destroy(compositeMaterial);
            if (maskMaterial != null) Destroy(maskMaterial);
            cmd?.Release();
            cmd = null;
            lastVersion = -1;
        }

        private void CreateQuad()
        {
            var go = new GameObject("VisionOverlay") { hideFlags = HideFlags.HideAndDontSave };
            quad = go.transform;
            quad.SetParent(transform, false);
            quad.localPosition = new Vector3(0f, 0f, 1f);

            var mesh = new Mesh { name = "VisionQuad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0)
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1000f, 1000f, 1f));

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            quadRenderer = go.AddComponent<MeshRenderer>();
            quadRenderer.sharedMaterial = compositeMaterial;
            quadRenderer.sortingOrder = short.MaxValue;
            quadRenderer.shadowCastingMode = ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (quadRenderer == null) return;

            // The overlay only belongs to the gameplay camera, not to the Scene view or previews.
            bool isOurs = camera == cam;
            quadRenderer.forceRenderingOff = !isOurs;
            if (!isOurs || cone == null || cone.Version == 0) return;

            EnsureMaskTexture();
            FitQuad();

            bool coneChanged = cone.Version != lastVersion;
            if (coneChanged) RebuildMesh();

            bool cameraChanged = lastCamPosition != cam.transform.position || !Mathf.Approximately(lastOrthoSize, cam.orthographicSize);

            cmd.Clear();
            if (memory != null)
            {
                if (coneChanged) memory.Stamp(cmd, visionMesh);
                memory.Fade(cmd, Time.deltaTime);
            }
            if (coneChanged || cameraChanged || maskDirty) DrawMask();
            if (cmd.sizeInBytes > 0) Graphics.ExecuteCommandBuffer(cmd);

            lastVersion = cone.Version;
            lastCamPosition = cam.transform.position;
            lastOrthoSize = cam.orthographicSize;
            maskDirty = false;

            ApplyMaterial();
        }

        private void EnsureMaskTexture()
        {
            int w = Mathf.Max(16, Mathf.RoundToInt(cam.pixelWidth * settings.maskResolutionScale));
            int h = Mathf.Max(16, Mathf.RoundToInt(cam.pixelHeight * settings.maskResolutionScale));
            if (maskTexture != null && maskTexture.width == w && maskTexture.height == h) return;

            if (maskTexture != null) { maskTexture.Release(); Destroy(maskTexture); }
            maskTexture = new RenderTexture(w, h, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear)
            {
                name = "VisionMask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            maskTexture.Create();
            maskDirty = true;
        }

        private void FitQuad()
        {
            float height = cam.orthographicSize * 2f;
            quad.localScale = new Vector3(height * cam.aspect, height, 1f);
        }

        private void DrawMask()
        {
            Matrix4x4 vp = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
            cmd.SetRenderTarget(maskTexture);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.SetGlobalMatrix(VisionVP, vp);
            cmd.SetGlobalFloat(FalloffStart, settings.radialFalloffStart);
            cmd.DrawMesh(visionMesh, Matrix4x4.identity, maskMaterial, 0, 0);
        }

        private void ApplyMaterial()
        {
            float height = cam.orthographicSize * 2f;
            Vector3 p = cam.transform.position;
            compositeMaterial.SetTexture(MaskTex, maskTexture);
            compositeMaterial.SetVector(CamRect, new Vector4(p.x, p.y, height * cam.aspect, height));
            compositeMaterial.SetColor(DarkColor, settings.darknessColor);

            if (memory != null)
            {
                compositeMaterial.SetTexture(MemoryTex, memory.Texture);
                compositeMaterial.SetVector(MemoryRect, memory.WorldRect);
                compositeMaterial.SetFloat(MemoryBrightness, settings.memoryBrightness);
                compositeMaterial.SetFloat(MemoryWrap, memory.Wraps ? 1f : 0f);
            }
            else
            {
                compositeMaterial.SetTexture(MemoryTex, Texture2D.blackTexture);
                compositeMaterial.SetVector(MemoryRect, new Vector4(0, 0, 1, 1));
                compositeMaterial.SetFloat(MemoryBrightness, 0f);
                compositeMaterial.SetFloat(MemoryWrap, 0f);
            }
        }

        /// <summary>
        /// Builds a non-indexed triangle list. Every triangle owns its origin vertex so the angular weight
        /// stays constant along a ray (shared origin vertices would make the soft edge narrow near the player).
        /// uv.x = brightness weight, uv.y = distance from origin / radius.
        /// </summary>
        private void RebuildMesh()
        {
            vertices.Clear();
            uvs.Clear();
            triangles.Clear();

            Vector2 origin = cone.Origin;

            var cp = cone.ConePoints;
            var cw = cone.ConeWeights;
            for (int i = 0; i < cp.Length - 1; i++)
            {
                AddTriangle(origin, cp[i], cp[i + 1], (cw[i] + cw[i + 1]) * 0.5f, cw[i], cw[i + 1], settings.radius);
            }

            var ap = cone.AmbientPoints;
            for (int i = 0; i < ap.Length; i++)
            {
                float w = settings.ambientIntensity;
                AddTriangle(origin, ap[i], ap[(i + 1) % ap.Length], w, w, w, settings.ambientRadius);
            }

            visionMesh.Clear();
            visionMesh.SetVertices(vertices);
            visionMesh.SetUVs(0, uvs);
            visionMesh.SetTriangles(triangles, 0, false);
            visionMesh.bounds = new Bounds(origin, Vector3.one * (settings.radius * 2f + 2f));
        }

        private void AddTriangle(Vector2 origin, Vector2 a, Vector2 b, float wOrigin, float wa, float wb, float radius)
        {
            int start = vertices.Count;
            vertices.Add(origin);
            vertices.Add(a);
            vertices.Add(b);
            uvs.Add(new Vector2(wOrigin, 0f));
            uvs.Add(new Vector2(wa, Vector2.Distance(origin, a) / radius));
            uvs.Add(new Vector2(wb, Vector2.Distance(origin, b) / radius));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }
    }
}

