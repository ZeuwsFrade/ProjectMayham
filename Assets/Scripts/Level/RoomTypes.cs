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
    /// The maze is a grid of 4x4-tile cells: a one-tile wall line and three tiles of floor, so a corridor is three
    /// tiles wide. A room covers a block of cells and removes the wall lines inside it, so its inside is
    /// 4 * cells - 1 tiles wide and tall.
    /// </summary>
    public static class RoomSizes
    {
        public const int CellTiles = 4;

        /// <summary>Cells covered by a slot: small 7x7, medium 8x7, large 9x9.</summary>
        public static Vector2Int Cells(RoomSize size)
        {
            switch (size)
            {
                case RoomSize.Small: return new Vector2Int(7, 7);
                case RoomSize.Medium: return new Vector2Int(8, 7);
                default: return new Vector2Int(9, 9);
            }
        }

        /// <summary>Floor tiles inside the room walls: small 27x27, medium 31x27, large 35x35.</summary>
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
