using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectMayham.Level
{
    /// <summary>
    /// Root of a room prefab: one hand-editable mini level. The tilemaps under <see cref="tiles"/> hold the floor, the
    /// walls and the windows, with the tile (0, 0) at the bottom-left corner of the floor. The room owns the ring of
    /// outer walls around its floor (tiles -1 and width / height): a gap in the ring is a door, a window tile lets
    /// the player look in from the corridor. At the start of a raid the tiles are copied into the maze tilemaps, the
    /// rest (furniture, decor, loot spots) is used as is.
    ///
    /// Rules for editing:
    /// - the cells of the maze are four tiles (a wall line and three tiles of floor). A door is two tiles wide and
    ///   lies within one cell: it takes two of the offsets 4k, 4k + 1, 4k + 2 along its side and never 4k + 3, so
    ///   it faces the floor of a corridor and not a corridor wall;
    /// - inner walls stand on the same lattice (offsets 4k + 3), which keeps them clear of the doors;
    /// - every piece of floor has to stay reachable from the doors (Mayham > Level > Validate Rooms checks it).
    /// </summary>
    public class RoomTemplate : MonoBehaviour
    {
        [SerializeField] private RoomKind kind;
        [SerializeField] private RoomSize size;
        [Tooltip("Parent of the 'Floor', 'Walls' and 'Windows' tilemaps. Destroyed after its tiles have been copied.")]
        [SerializeField] private Transform tiles;
        [SerializeField] private Tilemap floor;
        [SerializeField] private Tilemap walls;
        [Tooltip("Tiles that stop movement but not sight.")]
        [SerializeField] private Tilemap windows;
        [Tooltip("Objects that stay in the room: furniture and other props.")]
        [SerializeField] private Transform props;
        [Tooltip("Objects drawn on the walls.")]
        [SerializeField] private Transform decor;
        [Tooltip("Empty children that mark the places where loot can appear.")]
        [SerializeField] private Transform lootSpots;

        public RoomKind Kind => kind;
        public RoomSize Size => size;
        public Transform Tiles => tiles;
        public Tilemap Floor => floor;
        public Tilemap Walls => walls;
        public Tilemap Windows => windows;
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
