using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectMayham.World
{
    /// <summary>
    /// Makes the level a seamless torus: leaving through one edge brings you in through the opposite one.
    /// The tilemaps are padded with wrapped copies of themselves so the far side is visible and solid across
    /// the seam. The anchor (player) is kept inside the domain; whenever it leaves, it is shifted by a whole
    /// domain size together with the camera, which is invisible because the padding looks identical.
    /// Every <see cref="WrapEntity"/> is kept at its image nearest to the anchor.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class WrapWorld : MonoBehaviour
    {
        public static WrapWorld Active { get; private set; }

        [Header("Domain")]
        [Tooltip("World-space centre of the wrapped area.")]
        [SerializeField] private Vector2 center;
        [Tooltip("World-space size of the wrapped area. Must be a whole number of tilemap cells.")]
        [SerializeField] private Vector2 size = new Vector2(40f, 40f);

        [Header("Anchor")]
        [Tooltip("The object the world wraps around, normally the player.")]
        [SerializeField] private Transform anchor;
        [Tooltip("Moved together with the anchor when it wraps. Left empty, the main camera is used.")]
        [SerializeField] private Transform[] moveWithAnchor;

        [Header("Tilemaps")]
        [Tooltip("Tilemaps that get wrapped copies of their tiles around the domain at startup.")]
        [SerializeField] private Tilemap[] tilemaps;
        [Tooltip("How far the copies extend beyond the domain, in world units. Half of the domain is enough " +
                 "as long as the camera and the vision radius see less than that.")]
        [SerializeField] private Vector2 padding = new Vector2(20f, 20f);

        // An entity is only re-imaged once it is this much farther than half a domain away,
        // so something sitting exactly opposite the anchor does not flip every frame.
        private const float ImageHysteresis = 0.5f;

        private static readonly HashSet<WrapEntity> entities = new HashSet<WrapEntity>();
        private Rigidbody2D anchorBody;

        public Vector2 Size => size;
        public Vector2 Min => center - size * 0.5f;
        public Vector2 Max => center + size * 0.5f;

        /// <summary>Raised after the anchor has been shifted to the opposite side, with the applied offset.</summary>
        public event System.Action<Vector2> Shifted;

        internal static void Register(WrapEntity entity) => entities.Add(entity);
        internal static void Unregister(WrapEntity entity) => entities.Remove(entity);

        private void Awake()
        {
            Active = this;
            if (anchor != null) anchorBody = anchor.GetComponent<Rigidbody2D>();
            PadTilemaps();
        }

        private void Start()
        {
            if ((moveWithAnchor == null || moveWithAnchor.Length == 0) && Camera.main != null)
                moveWithAnchor = new[] { Camera.main.transform };

            var cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                float halfHeight = cam.orthographicSize;
                float halfWidth = halfHeight * cam.aspect;
                if (halfWidth > padding.x || halfHeight > padding.y)
                    Debug.LogWarning("WrapWorld: the camera sees farther than the tilemap padding, the seam will be visible.", this);
            }
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        private void FixedUpdate()
        {
            if (anchor == null) return;

            Vector2 position = anchor.position;
            Vector2 wrapped = Wrap(position);
            if (wrapped != position)
            {
                Vector2 shift = wrapped - position;
                Teleport(anchor, anchorBody, shift);
                foreach (var t in moveWithAnchor)
                    if (t != null) t.position += (Vector3)shift;
                position = wrapped;
                Shifted?.Invoke(shift);
            }

            foreach (var entity in entities)
            {
                Vector2 current = entity.transform.position;
                Vector2 offset = current - position;
                if (Mathf.Abs(offset.x) <= size.x * 0.5f + ImageHysteresis &&
                    Mathf.Abs(offset.y) <= size.y * 0.5f + ImageHysteresis) continue;

                Teleport(entity.transform, entity.Body, NearestImage(current, position) - current);
            }
        }

        /// <summary>The point moved by whole domain sizes so that it lies inside the domain.</summary>
        public Vector2 Wrap(Vector2 point)
        {
            Vector2 min = Min;
            return new Vector2(min.x + Mathf.Repeat(point.x - min.x, size.x), min.y + Mathf.Repeat(point.y - min.y, size.y));
        }

        /// <summary>The copy of <paramref name="point"/> that is closest to <paramref name="reference"/>.</summary>
        public Vector2 NearestImage(Vector2 point, Vector2 reference) => reference + Delta(reference, point);

        /// <summary>Shortest vector from one point to another, taking the wrap-around into account.</summary>
        public Vector2 Delta(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            d.x -= size.x * Mathf.Round(d.x / size.x);
            d.y -= size.y * Mathf.Round(d.y / size.y);
            return d;
        }

        private static void Teleport(Transform target, Rigidbody2D body, Vector2 shift)
        {
            if (body == null)
            {
                target.position += (Vector3)shift;
                return;
            }

            // Switching interpolation off for the move stops the body from being smeared across the level for a frame.
            var interpolation = body.interpolation;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.position += shift;
            target.position += (Vector3)shift;
            body.interpolation = interpolation;
        }

        /// <summary>
        /// Copies the tiles of the domain into the padding around it. Being in the same tilemap, rule tiles
        /// connect across the seam and the collider covers the copies as well.
        /// </summary>
        private void PadTilemaps()
        {
            if (tilemaps == null) return;

            foreach (var map in tilemaps)
            {
                if (map == null) continue;

                Vector3Int minCell = map.WorldToCell(Min);
                Vector3 cell = map.layoutGrid.cellSize;
                int width = Mathf.RoundToInt(size.x / cell.x);
                int height = Mathf.RoundToInt(size.y / cell.y);
                int padX = Mathf.CeilToInt(padding.x / cell.x);
                int padY = Mathf.CeilToInt(padding.y / cell.y);

                var positions = new List<Vector3Int>();
                var tiles = new List<TileBase>();
                for (int x = -padX; x < width + padX; x++)
                {
                    for (int y = -padY; y < height + padY; y++)
                    {
                        if (x >= 0 && x < width && y >= 0 && y < height) continue;

                        var source = new Vector3Int(minCell.x + (int)Mathf.Repeat(x, width), minCell.y + (int)Mathf.Repeat(y, height), minCell.z);
                        var tile = map.GetTile(source);
                        if (tile == null) continue;
                        positions.Add(new Vector3Int(minCell.x + x, minCell.y + y, minCell.z));
                        tiles.Add(tile);
                    }
                }
                map.SetTiles(positions.ToArray(), tiles.ToArray());

                // Tiles on the old outer edge now have neighbours.
                map.RefreshAllTiles();
                if (map.TryGetComponent<TilemapCollider2D>(out var tilemapCollider)) tilemapCollider.ProcessTilemapChanges();
                if (map.TryGetComponent<CompositeCollider2D>(out var composite)) composite.GenerateGeometry();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, size);
            Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
            Gizmos.DrawWireCube(center, size + padding * 2f);
        }
    }
}
