using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectMayham.Level
{
    public partial class RaidGenerator
    {
        [Serializable]
        private struct TemplateTile
        {
            public Vector3Int Cell;
            public TileBase Floor;
            public TileBase Wall;
        }

        [Header("Raid zones")]
        [SerializeField, Min(1f)] private float entryRadius = 42f;
        [SerializeField, Min(1f)] private float riskRadius = 90f;
        [SerializeField, HideInInspector] private List<TemplateTile> corridorChanges = new List<TemplateTile>();
        private readonly Dictionary<RoomSlot, RaidZone> roomZones = new Dictionary<RoomSlot, RaidZone>();

        public Vector2 SpawnPoint { get; private set; }

        public IEnumerable<Vector2> CorridorSpawnPoints()
        {
            for (int row = 0; row < cells; row++)
            for (int column = 0; column < cells; column++)
            {
                Vector2 point = CellCenter(new Vector2Int(column, row));
                var tile = CellOf(point);
                if (floorMap.HasTile(tile) && !wallMap.HasTile(tile) && !IsInsideRoom(point)) yield return point;
            }
        }

        private RaidZone RingAt(Vector2 point) => RaidZones.Classify(
            RaidZones.Delta(SpawnPoint, point, Tiles).magnitude, entryRadius, Mathf.Max(entryRadius, riskRadius));

        public RaidZone ZoneAt(Vector2 point)
        {
            point.x = gridOrigin.x + Mathf.Repeat(point.x - gridOrigin.x, Tiles);
            point.y = gridOrigin.y + Mathf.Repeat(point.y - gridOrigin.y, Tiles);
            foreach (var pair in roomZones)
            {
                var offset = point - pair.Key.InteriorOrigin;
                var size = pair.Key.Interior;
                if (offset.x >= 0 && offset.y >= 0 && offset.x < size.x && offset.y < size.y) return pair.Value;
            }
            return RingAt(point);
        }

        private void PrepareZones(List<RoomSlot> slots)
        {
            var marker = transform.Find("PlayerSpawn");
            if (marker == null) marker = keepClear?.FirstOrDefault(point => point != null);
            SpawnPoint = marker != null ? (Vector2)marker.position : Vector2.zero;
            foreach (var slot in slots)
                roomZones.Add(slot, RingAt(slot.InteriorOrigin + (Vector2)slot.Interior * 0.5f));
        }

        private void RestoreCorridors()
        {
            if (corridorChanges.Count == 0) return;
            var positions = corridorChanges.Select(tile => tile.Cell).ToArray();
            floorMap.SetTiles(positions, corridorChanges.Select(tile => tile.Floor).ToArray());
            wallMap.SetTiles(positions, corridorChanges.Select(tile => tile.Wall).ToArray());
            corridorChanges.Clear();
        }

        private void RebuildCorridors(List<RoomSlot> slots, System.Random rng)
        {
            if (floorMap == null || wallMap == null) return;

            var nodes = new bool[cells, cells];
            TileBase floor = null;
            TileBase wall = null;
            for (int row = 0; row < cells; row++)
            for (int column = 0; column < cells; column++)
            {
                var cell = new Vector2Int(column, row);
                if (InsideSlot(slots, cell)) continue;
                var tile = floorMap.GetTile(CellOf(CellCenter(cell)));
                nodes[column, row] = tile != null;
                if (floor == null) floor = tile;
                if (wall == null) wall = wallMap.GetTile(CrossingTile(cell, true, 0));
            }
            if (floor == null || wall == null) return;

            var edges = new List<(Vector2Int from, Vector2Int to, bool horizontal, RaidZone zone, double cost)>();
            int offsetX = rng.Next(3), offsetY = rng.Next(3);
            for (int row = 0; row < cells; row++)
            for (int column = 0; column < cells; column++)
            {
                if (!nodes[column, row]) continue;
                var from = new Vector2Int(column, row);
                AddEdge(from, new Vector2Int((column + 1) % cells, row), true);
                AddEdge(from, new Vector2Int(column, (row + 1) % cells), false);
            }

            void AddEdge(Vector2Int from, Vector2Int to, bool horizontal)
            {
                if (!nodes[to.x, to.y]) return;
                if (windowMap != null)
                    for (int index = 1; index < RoomSizes.CellTiles; index++)
                        if (windowMap.HasTile(CrossingTile(from, horizontal, index))) return;
                var midpoint = CellCenter(from) + (horizontal ? Vector3.right : Vector3.up) * (RoomSizes.CellTiles * 0.5f);
                var zone = RingAt(midpoint);
                int lane = horizontal ? from.y + offsetY : from.x + offsetX;
                double cost = rng.NextDouble();
                if (zone == RaidZone.Entry && lane % 3 == 0) cost *= 0.1;
                if (zone == RaidZone.Risk && lane % 5 == 0) cost *= 0.5;
                edges.Add((from, to, horizontal, zone, cost));
            }

            var parents = Enumerable.Range(0, cells * cells).ToArray();
            var positions = new List<Vector3Int>();
            var floors = new List<TileBase>();
            var walls = new List<TileBase>();
            foreach (var edge in edges.OrderBy(edge => edge.cost))
            {
                int first = FindRoot(parents, edge.from.y * cells + edge.from.x);
                int second = FindRoot(parents, edge.to.y * cells + edge.to.x);
                bool open = first != second;
                if (open) parents[first] = second;
                else open = rng.NextDouble() < (edge.zone == RaidZone.Entry ? 0.48 : edge.zone == RaidZone.Risk ? 0.18 : 0.045);

                for (int index = 1; index < RoomSizes.CellTiles; index++)
                {
                    var position = CrossingTile(edge.from, edge.horizontal, index);
                    var oldFloor = floorMap.GetTile(position);
                    var oldWall = wallMap.GetTile(position);
                    if (open ? oldWall == null && oldFloor != null : oldWall != null && oldFloor == null) continue;
                    corridorChanges.Add(new TemplateTile { Cell = position, Floor = oldFloor, Wall = oldWall });
                    positions.Add(position);
                    floors.Add(open ? floor : null);
                    walls.Add(open ? null : wall);
                }
            }
            var changed = positions.ToArray();
            floorMap.SetTiles(changed, floors.ToArray());
            wallMap.SetTiles(changed, walls.ToArray());
        }

        private Vector3Int CrossingTile(Vector2Int from, bool horizontal, int index)
        {
            int step = RoomSizes.CellTiles;
            int column = horizontal ? (from.x + 1) * step % Tiles : from.x * step + index;
            int row = horizontal ? from.y * step + index : (from.y + 1) * step % Tiles;
            return new Vector3Int(gridOrigin.x + column, gridOrigin.y + row, 0);
        }

        private static int FindRoot(int[] parents, int node)
        {
            while (node != parents[node])
            {
                parents[node] = parents[parents[node]];
                node = parents[node];
            }
            return node;
        }
    }
}
