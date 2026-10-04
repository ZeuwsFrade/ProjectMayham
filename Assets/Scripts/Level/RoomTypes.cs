using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>What a room is for. Decides its furniture, floor and loot.</summary>
    public enum RoomKind
    {
        Office,
        BreakRoom,
        BossOffice,
        Toilet,
        Parking,
        Storage,
        CopyRoom
    }

    /// <summary>Size class of a room slot in the corridor template.</summary>
    public enum RoomSize
    {
        Small,
        Medium,
        Large
    }

    /// <summary>
    /// The maze is a grid of 3x3-tile cells: a one-tile wall line and two tiles of floor. A room covers a block of
    /// cells and removes the wall lines inside it, so its inside is 3 * cells - 1 tiles wide and tall.
    /// </summary>
    public static class RoomSizes
    {
        public const int CellTiles = 3;

        /// <summary>Cells covered by a slot: small 2x2, medium 3x2, large 3x3.</summary>
        public static Vector2Int Cells(RoomSize size)
        {
            switch (size)
            {
                case RoomSize.Small: return new Vector2Int(2, 2);
                case RoomSize.Medium: return new Vector2Int(3, 2);
                default: return new Vector2Int(3, 3);
            }
        }

        /// <summary>Floor tiles inside the room walls: small 5x5, medium 8x5, large 8x8.</summary>
        public static Vector2Int Interior(RoomSize size)
        {
            var cells = Cells(size);
            return new Vector2Int(cells.x * CellTiles - 1, cells.y * CellTiles - 1);
        }

        public static string Title(RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Office: return "Офис";
                case RoomKind.BreakRoom: return "Комната отдыха";
                case RoomKind.BossOffice: return "Офис босса";
                case RoomKind.Toilet: return "Туалет";
                case RoomKind.Parking: return "Парковка";
                case RoomKind.Storage: return "Склад";
                default: return "Комната ксерокопии";
            }
        }
    }
}
