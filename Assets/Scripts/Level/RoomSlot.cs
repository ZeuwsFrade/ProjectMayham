using System.Collections.Generic;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// A place in the corridor template where a room is put at the start of every raid. The walls around the slot and
    /// its doors belong to the template; everything inside is filled by the chosen <see cref="RoomTemplate"/>.
    /// </summary>
    public class RoomSlot : MonoBehaviour
    {
        [SerializeField] private RoomSize size;
        [Tooltip("Bottom-left floor tile inside the slot, in maze (world) cell coordinates.")]
        [SerializeField] private Vector2Int interiorOrigin;
        [Tooltip("Tiles (world cell coordinates) where the border wall is open to a corridor.")]
        [SerializeField] private List<Vector2Int> doorTiles = new List<Vector2Int>();

        public RoomSize Size => size;
        public Vector2Int InteriorOrigin => interiorOrigin;
        public Vector2Int Interior => RoomSizes.Interior(size);
        public IReadOnlyList<Vector2Int> DoorTiles => doorTiles;

        public void Configure(RoomSize newSize, Vector2Int origin, IEnumerable<Vector2Int> doors)
        {
            size = newSize;
            interiorOrigin = origin;
            doorTiles = new List<Vector2Int>(doors);
            transform.position = new Vector3(origin.x, origin.y, 0f);
        }

        private void OnDrawGizmos()
        {
            var interior = Interior;
            Gizmos.color = new Color(0.3f, 0.9f, 0.9f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(interiorOrigin.x + interior.x * 0.5f, interiorOrigin.y + interior.y * 0.5f), new Vector3(interior.x, interior.y));
            Gizmos.color = Color.green;
            foreach (var door in doorTiles) Gizmos.DrawWireCube(new Vector3(door.x + 0.5f, door.y + 0.5f), Vector3.one * 0.8f);
        }
    }
}
