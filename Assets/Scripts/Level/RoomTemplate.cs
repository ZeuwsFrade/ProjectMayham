using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectMayham.Level
{
    /// <summary>
    /// Root of a room prefab: one hand-editable mini level. The tilemaps under <see cref="tiles"/> hold the floor and
    /// the inner walls, with the tile (0, 0) at the bottom-left corner of the room. At the start of a raid they are
    /// copied into the maze tilemaps, the rest (furniture, decor, loot spots) is used as is.
    ///
    /// Rule for editing: keep the outer ring of floor tiles free of anything that blocks movement. Doors open
    /// anywhere on the room border, so a free ring guarantees that every door leads to everything inside.
    /// </summary>
    public class RoomTemplate : MonoBehaviour
    {
        [SerializeField] private RoomKind kind;
        [SerializeField] private RoomSize size;
        [Tooltip("Parent of the 'Floor' and 'Walls' tilemaps. Destroyed after its tiles have been copied.")]
        [SerializeField] private Transform tiles;
        [SerializeField] private Tilemap floor;
        [SerializeField] private Tilemap walls;
        [Tooltip("Objects that stay in the room: furniture and other props.")]
        [SerializeField] private Transform props;
        [Tooltip("Objects drawn on the border walls. They are removed when a door opens in their place.")]
        [SerializeField] private Transform decor;
        [Tooltip("Empty children that mark the places where loot can appear.")]
        [SerializeField] private Transform lootSpots;

        public RoomKind Kind => kind;
        public RoomSize Size => size;
        public Transform Tiles => tiles;
        public Tilemap Floor => floor;
        public Tilemap Walls => walls;
        public Transform Props => props;
        public Transform Decor => decor;
        public Transform LootSpots => lootSpots;
        public Vector2Int Interior => RoomSizes.Interior(size);

        private void OnDrawGizmosSelected()
        {
            var interior = Interior;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + new Vector3(interior.x, interior.y) * 0.5f, new Vector3(interior.x, interior.y));
        }
    }
}
