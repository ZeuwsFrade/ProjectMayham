using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// A place in the corridor template where a room is put at the start of every raid. The template leaves the slot
    /// empty: the chosen <see cref="RoomTemplate"/> brings its own outer walls, doors and windows.
    /// </summary>
    public class RoomSlot : MonoBehaviour
    {
        [SerializeField] private RoomSize size;
        [Tooltip("Bottom-left floor tile inside the slot, in maze (world) cell coordinates.")]
        [SerializeField] private Vector2Int interiorOrigin;

        public RoomSize Size => size;
        public Vector2Int InteriorOrigin => interiorOrigin;
        public Vector2Int Interior => RoomSizes.Interior(size);

        public void Configure(RoomSize newSize, Vector2Int origin)
        {
            size = newSize;
            interiorOrigin = origin;
            transform.position = new Vector3(origin.x, origin.y, 0f);
        }

        /// <summary>True for the floor of the slot and for the ring of outer walls around it.</summary>
        public bool Covers(Vector3Int cell)
        {
            var interior = Interior;
            return cell.x >= interiorOrigin.x - 1 && cell.x <= interiorOrigin.x + interior.x &&
                   cell.y >= interiorOrigin.y - 1 && cell.y <= interiorOrigin.y + interior.y;
        }

        private void OnDrawGizmos()
        {
            var interior = Interior;
            Gizmos.color = new Color(0.3f, 0.9f, 0.9f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(interiorOrigin.x + interior.x * 0.5f, interiorOrigin.y + interior.y * 0.5f), new Vector3(interior.x, interior.y));
        }
    }
}
