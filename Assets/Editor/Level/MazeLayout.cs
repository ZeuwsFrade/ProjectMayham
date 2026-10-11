using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Level;
using UnityEngine;
using Random = System.Random;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// The corridor maze as a plan, before any tile is painted: where the nine room slots lie on the 51x51 grid of
    /// cells and which of the cells between them are open to each other. The level wraps around, so the grid has
    /// no edge. <see cref="TemplateBuilder"/> turns the plan into tilemaps; the checks and the numbers that tell one
    /// kind of maze from another (dead ends, blind branches, pieces of wall) are read off the plan.
    ///
    /// How the maze is made is said by <see cref="MazeSettings"/>. Without avenues one passage is dug from the
    /// start as far as it goes and the digging goes back to the last cell that still has somewhere to go: a tree of
    /// long winding corridors. With avenues the straight through-roads come first and passages are dug into the
    /// blocks between them from gates in their sides. Then come the openings that make loops, and the dead ends
    /// that are led on. An opening is only made where the pieces of wall on both sides of it stay big, so no post
    /// and no short stump of wall is left standing on its own, and no four cells make an open square.
    /// </summary>
    public class MazeLayout
    {
        public const int Regions = 3;                          // rooms are spread over a 3x3 grid of regions, one each
        public const int Region = 17;                          // cells per region
        public const int Cells = Regions * Region;             // 51 cells per side of the level
        public const int RoomMargin = 3;                       // cells of maze between a room and the border of its region
        public const int T = RoomSizes.CellTiles;              // tiles per cell: a wall line and the floor

        /// <summary>The spawn cell is a corner of four regions, far from every room.</summary>
        public static readonly Vector2Int Spawn = new Vector2Int(Region, Region);

        public class Slot
        {
            public RoomSize Size;
            public Vector2Int Cell;   // bottom-left cell
            public Vector2Int Span;   // cells wide and high
            public int Index;

            public bool Contains(Vector2Int cell) =>
                cell.x >= Cell.x && cell.x < Cell.x + Span.x && cell.y >= Cell.y && cell.y < Cell.y + Span.y;
        }

        private static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        private readonly Random rng;
        private readonly double straightBias;
        private readonly int minWallPiece;

        public List<Slot> Slots { get; }
        /// <summary>Every cell that is not a room.</summary>
        public HashSet<Vector2Int> Corridor { get; } = new HashSet<Vector2Int>();
        /// <summary>Corridor cell -> the cells it is open to.</summary>
        public Dictionary<Vector2Int, List<Vector2Int>> Linked { get; } = new Dictionary<Vector2Int, List<Vector2Int>>();
        /// <summary>Tiles of the wall lines that are open (tile indices from the corner of the level).</summary>
        public HashSet<Vector2Int> Opened { get; } = new HashSet<Vector2Int>();
        /// <summary>Cells of the avenues.</summary>
        public HashSet<Vector2Int> Avenue { get; } = new HashSet<Vector2Int>();
        /// <summary>Openings made after the passages were dug, to give a second way around.</summary>
        public int Loops { get; private set; }
        /// <summary>Dead ends that were opened to go on.</summary>
        public int LedOn { get; private set; }
        /// <summary>The dice after the maze was made: what is placed next goes on with them.</summary>
        public Random Rng => rng;

        public MazeLayout(int seed, MazeSettings settings)
        {
            rng = new Random(seed);
            // The settings hold floats; 0.55f is not the 0.55 the maze has always been made with.
            straightBias = Math.Round(settings.StraightBias, 4);
            minWallPiece = settings.MinWallPiece;

            Slots = PlaceSlots();
            for (int x = 0; x < Cells; x++)
                for (int y = 0; y < Cells; y++)
                    if (!Slots.Any(s => s.Contains(new Vector2Int(x, y)))) Corridor.Add(new Vector2Int(x, y));
            foreach (var c in Corridor) Linked[c] = new List<Vector2Int>();

            if (settings.Avenues == MazeAvenues.None) CarveTree();
            else CarveAvenues(settings.Avenues, settings.GateSpacing);
            Loops = AddLoops(Math.Round(settings.LoopChance, 4));
            if (settings.DeadEndsLedOn > 0f) LedOn = LeadDeadEndsOn(Math.Round(settings.DeadEndsLedOn, 4));
        }

        // ---- rooms ----

        private List<Slot> PlaceSlots()
        {
            var wanted = new[] { RoomSize.Large, RoomSize.Large, RoomSize.Large, RoomSize.Medium, RoomSize.Medium, RoomSize.Medium, RoomSize.Small, RoomSize.Small, RoomSize.Small };
            var sizes = wanted.OrderBy(_ => rng.Next()).ToList();
            var slots = new List<Slot>();
            for (int r = 0; r < Regions * Regions; r++)
            {
                // The room sits anywhere in its region that keeps the margin: neighbours are at least two margins of
                // maze apart, and no room lies across the seam of the wrapped level.
                var span = RoomSizes.Cells(sizes[r]);
                int slackX = Region - 2 * RoomMargin - span.x, slackY = Region - 2 * RoomMargin - span.y;
                if (slackX < 0 || slackY < 0) throw new InvalidOperationException($"A {sizes[r]} room does not fit into a region.");
                int x0 = (r % Regions) * Region + RoomMargin, y0 = (r / Regions) * Region + RoomMargin;
                slots.Add(new Slot { Size = sizes[r], Span = span, Index = r + 1, Cell = new Vector2Int(x0 + rng.Next(0, slackX + 1), y0 + rng.Next(0, slackY + 1)) });
            }
            return slots;
        }

        // ---- the maze ----

        public static Vector2Int Wrap(Vector2Int cell) => new Vector2Int((cell.x % Cells + Cells) % Cells, (cell.y % Cells + Cells) % Cells);

        /// <summary>Cells between two cells of the wrapped level, counted along the axes.</summary>
        public static int TorusDistance(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
            return Mathf.Min(dx, Cells - dx) + Mathf.Min(dy, Cells - dy);
        }

        // The tiles of the wall line between a cell and its neighbour in direction d: as many as a corridor is wide.
        private static IEnumerable<Vector2Int> SegmentBetween(Vector2Int cell, Vector2Int d)
        {
            for (int i = 1; i < T; i++)
            {
                if (d.x != 0) yield return new Vector2Int((d.x > 0 ? Wrap(cell + d).x : cell.x) * T, cell.y * T + i);
                else yield return new Vector2Int(cell.x * T + i, (d.y > 0 ? Wrap(cell + d).y : cell.y) * T);
            }
        }

        private static Vector2Int DirectionBetween(Vector2Int a, Vector2Int b)
        {
            foreach (var d in Dirs)
            {
                if (Wrap(a + d) == b) return d;
            }
            throw new InvalidOperationException($"{a} and {b} are not neighbours");
        }

        private void Open(Vector2Int a, Vector2Int b)
        {
            var d = DirectionBetween(a, b);
            foreach (var t in SegmentBetween(a, d)) Opened.Add(t);
            Linked[a].Add(b);
            Linked[b].Add(a);
        }

        /// <summary>
        /// Grows the maze over every cell that is not a room. One passage is dug as far as it goes, then the digging
        /// goes back to the last cell that still has a neighbour to go to: the result is a tree of long winding
        /// corridors with dead ends.
        /// </summary>
        private void CarveTree()
        {
            var visited = new HashSet<Vector2Int> { Spawn };
            var stack = new Stack<(Vector2Int cell, Vector2Int heading)>();
            stack.Push((Spawn, Vector2Int.zero));
            while (stack.Count > 0)
            {
                var (cell, heading) = stack.Peek();
                var options = Dirs.Where(d => { var n = Wrap(cell + d); return Corridor.Contains(n) && !visited.Contains(n); }).ToList();
                if (options.Count == 0) { stack.Pop(); continue; }

                // Going on straight more often than not makes corridors, not zigzags.
                var direction = options.Contains(heading) && rng.NextDouble() < straightBias ? heading : options[rng.Next(options.Count)];
                var next = Wrap(cell + direction);
                Open(cell, next);
                visited.Add(next);
                stack.Push((next, direction));
            }
            if (visited.Count != Corridor.Count) throw new InvalidOperationException("The maze does not reach every cell; try another seed.");
        }

        /// <summary>
        /// Avenues first: straight corridors that run through the whole level and, the level being wrapped, around
        /// it. Then the maze grows into the blocks between them: gates are opened in the sides of the avenues every
        /// few cells, and from every gate a passage is dug like in <see cref="CarveTree"/>, all of them in turns,
        /// until the blocks are full. A block is a set of winding trees then, each hanging on the avenue by its gate.
        /// </summary>
        private void CarveAvenues(MazeAvenues kind, int gateSpacing)
        {
            // An avenue runs along a border between the regions of the rooms, where the margin keeps the rooms away.
            var lines = kind == MazeAvenues.Cross ? new[] { Spawn.x } : Enumerable.Range(0, Regions).Select(r => r * Region).ToArray();
            foreach (int line in lines)
            {
                for (int k = 0; k < Cells; k++)
                {
                    Avenue.Add(new Vector2Int(k, line));
                    Avenue.Add(new Vector2Int(line, k));
                }
            }
            foreach (int line in lines)
            {
                for (int k = 0; k < Cells; k++)
                {
                    Open(new Vector2Int(k, line), Wrap(new Vector2Int(k + 1, line)));
                    Open(new Vector2Int(line, k), Wrap(new Vector2Int(line, k + 1)));
                }
            }

            var visited = new HashSet<Vector2Int>(Avenue);
            var walkers = new List<Stack<(Vector2Int cell, Vector2Int heading)>>();
            Action<Vector2Int, Vector2Int> gate = (from, direction) =>
            {
                var inside = Wrap(from + direction);
                if (!Corridor.Contains(inside) || !visited.Add(inside)) return;
                Open(from, inside);
                var walker = new Stack<(Vector2Int cell, Vector2Int heading)>();
                walker.Push((inside, direction));
                walkers.Add(walker);
            };

            // Gates on both sides of every avenue. A crossing has none: it is a part of two avenues.
            foreach (bool horizontal in new[] { true, false })
            {
                foreach (int line in lines)
                {
                    foreach (int side in new[] { 1, -1 })
                    {
                        for (int k = rng.Next(1, gateSpacing + 1); k < Cells; k += gateSpacing + rng.Next(-1, 2))
                        {
                            if (lines.Contains(k)) continue;
                            if (horizontal) gate(new Vector2Int(k, line), new Vector2Int(0, side));
                            else gate(new Vector2Int(line, k), new Vector2Int(side, 0));
                        }
                    }
                }
            }

            while (true)
            {
                while (walkers.Count > 0)
                {
                    var walker = walkers[rng.Next(walkers.Count)];
                    var (cell, heading) = walker.Peek();
                    var options = Dirs.Where(d => { var n = Wrap(cell + d); return Corridor.Contains(n) && !visited.Contains(n); }).ToList();
                    if (options.Count == 0)
                    {
                        walker.Pop();
                        if (walker.Count == 0) walkers.Remove(walker);
                        continue;
                    }

                    var direction = options.Contains(heading) && rng.NextDouble() < straightBias ? heading : options[rng.Next(options.Count)];
                    var next = Wrap(cell + direction);
                    Open(cell, next);
                    visited.Add(next);
                    walker.Push((next, direction));
                }
                if (visited.Count == Corridor.Count) break;

                // A block no gate fell into (gates far apart): it gets one where it touches an avenue.
                var left = Corridor.Where(c => !visited.Contains(c)).OrderBy(c => c.x * 1000 + c.y)
                    .SelectMany(c => Dirs.Select(d => (cell: c, d)))
                    .FirstOrDefault(p => Avenue.Contains(Wrap(p.cell + p.d)));
                if (left.d == Vector2Int.zero) throw new InvalidOperationException("The maze does not reach every cell; try another seed.");
                gate(Wrap(left.cell + left.d), -left.d);
            }
        }

        /// <summary>
        /// A few loops. Opening a wall cuts the walls behind it off from the rest; it is only done when what is cut
        /// off stays a big piece, so neither a lonely post nor a short stump of wall is left.
        /// </summary>
        private int AddLoops(double chance)
        {
            int loops = 0;
            foreach (var cell in Corridor.OrderBy(c => c.x * 1000 + c.y))
            {
                foreach (var d in new[] { Vector2Int.right, Vector2Int.up })
                {
                    var other = Wrap(cell + d);
                    if (!Corridor.Contains(other) || Linked[cell].Contains(other) || rng.NextDouble() >= chance) continue;
                    if (!WallPiecesStayBig(cell, other)) continue;
                    Open(cell, other);
                    loops++;
                }
            }
            return loops;
        }

        /// <summary>
        /// Opens dead ends so that they lead on: straight ahead when the wall there may go, into another dead end
        /// next, into any neighbouring corridor last. The rule about the pieces of wall holds here too, so a dead
        /// end whose every wall is needed stays one.
        /// </summary>
        private int LeadDeadEndsOn(double share)
        {
            var ends = Corridor.Where(c => Linked[c].Count == 1).OrderBy(c => c.x * 1000 + c.y).ToList();
            for (int i = ends.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (ends[i], ends[j]) = (ends[j], ends[i]);
            }

            int led = 0;
            foreach (var cell in ends)
            {
                // Another dead end may have been led into this one meanwhile.
                if (Linked[cell].Count != 1 || rng.NextDouble() >= share) continue;

                var ahead = Wrap(cell + DirectionBetween(Linked[cell][0], cell));
                var options = Dirs.Select(d => Wrap(cell + d))
                    .Where(n => Corridor.Contains(n) && !Linked[cell].Contains(n) && WallPiecesStayBig(cell, n))
                    .Select(n => (cell: n, order: rng.Next()))
                    .OrderBy(o => o.cell == ahead ? 0 : 1).ThenBy(o => Linked[o.cell].Count == 1 ? 0 : 1).ThenBy(o => o.order)
                    .ToList();
                if (options.Count == 0) continue;

                Open(cell, options[0].cell);
                led++;
            }
            return led;
        }

        /// <summary>A wall between two cells stands unless both are maze cells that are open to each other.</summary>
        private bool Wall(Vector2Int a, Vector2Int b)
        {
            a = Wrap(a); b = Wrap(b);
            return !Corridor.Contains(a) || !Corridor.Contains(b) || !Linked[a].Contains(b);
        }

        // The four walls that meet at a post, each with the post it leads to and the two cells it separates.
        // A post is a corner of the grid, the bottom-left one of its cell.
        private static (Vector2Int to, Vector2Int a, Vector2Int b)[] WallsAt(Vector2Int post) => new[]
        {
            (post + Vector2Int.right, post, post + Vector2Int.down),
            (post + Vector2Int.up, post, post + Vector2Int.left),
            (post + Vector2Int.left, post + Vector2Int.left, post + Vector2Int.left + Vector2Int.down),
            (post + Vector2Int.down, post + Vector2Int.down, post + Vector2Int.down + Vector2Int.left),
        };

        /// <summary>
        /// True when the wall between two neighbouring cells can go: the walls that hang on either of its two ends
        /// still make up a piece of at least the least number of posts the settings ask for. Walls are followed from
        /// post to post.
        /// </summary>
        private bool WallPiecesStayBig(Vector2Int cell, Vector2Int other)
        {
            // Seen from the left or the lower of the two cells, as the wall is named by the post of the other one.
            var d = DirectionBetween(cell, other);
            if (d.x < 0 || d.y < 0)
            {
                (cell, other) = (other, cell);
                d = -d;
            }

            // The wall runs from the post of 'other' along the side the two cells share.
            var first = other;
            var second = Wrap(other + (d.x != 0 ? Vector2Int.up : Vector2Int.right));

            foreach (var start in new[] { first, second })
            {
                var seen = new HashSet<Vector2Int> { start };
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(start);
                while (queue.Count > 0 && seen.Count < minWallPiece)
                {
                    var post = queue.Dequeue();
                    foreach (var (to, a, b) in WallsAt(post))
                    {
                        var wa = Wrap(a);
                        var wb = Wrap(b);
                        bool removed = (wa == cell && wb == other) || (wa == other && wb == cell);   // the one that is to go
                        var next = Wrap(to);
                        if (removed || !Wall(a, b) || !seen.Add(next)) continue;
                        queue.Enqueue(next);
                    }
                }
                if (seen.Count < minWallPiece) return false;
            }
            return true;
        }

        // ---- reading the plan ----

        public List<Vector2Int> DeadEnds() => Linked.Where(p => p.Value.Count == 1).Select(p => p.Key).ToList();

        /// <summary>
        /// The exit is as far away as it gets when walking the bird's way, not along the maze: the path through the
        /// maze alone says little, the rooms are shortcuts. A dead end, so that it has to be looked for; a maze that
        /// has none left gives the farthest cell off the avenues.
        /// </summary>
        public Vector2Int ExitCell()
        {
            var candidates = DeadEnds();
            if (candidates.Count == 0) candidates = Corridor.Where(c => !Avenue.Contains(c)).ToList();
            return candidates.OrderByDescending(c => TorusDistance(c, Spawn)).ThenBy(c => c.x).ThenBy(c => c.y).First();
        }

        /// <summary>Steps along the corridors from a cell to every cell that can be reached from it.</summary>
        public Dictionary<Vector2Int, int> Distances(Vector2Int from)
        {
            var distance = new Dictionary<Vector2Int, int> { [from] = 0 };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var next in Linked[cell])
                {
                    if (distance.ContainsKey(next)) continue;
                    distance[next] = distance[cell] + 1;
                    queue.Enqueue(next);
                }
            }
            return distance;
        }

        /// <summary>
        /// Share of the corridor cells that lie in blind branches: whoever walks into one has to come back the same
        /// way. <paramref name="deepest"/> is how far the deepest of them goes, in cells from the nearest corridor
        /// that leads on.
        /// </summary>
        public float BlindShare(out int deepest)
        {
            var degree = Corridor.ToDictionary(c => c, c => Linked[c].Count);
            var queue = new Queue<Vector2Int>(Corridor.Where(c => degree[c] == 1));
            var blind = new HashSet<Vector2Int>();
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (degree[cell] != 1 || !blind.Add(cell)) continue;
                foreach (var next in Linked[cell])
                {
                    if (blind.Contains(next)) continue;
                    if (--degree[next] == 1) queue.Enqueue(next);
                }
            }

            deepest = 0;
            if (blind.Count == 0 || blind.Count == Corridor.Count) return blind.Count / (float)Corridor.Count;

            var depth = Corridor.Where(c => !blind.Contains(c)).ToDictionary(c => c, _ => 0);
            var walk = new Queue<Vector2Int>(depth.Keys);
            while (walk.Count > 0)
            {
                var cell = walk.Dequeue();
                foreach (var next in Linked[cell])
                {
                    if (depth.ContainsKey(next)) continue;
                    depth[next] = depth[cell] + 1;
                    deepest = Mathf.Max(deepest, depth[next]);
                    walk.Enqueue(next);
                }
            }
            return blind.Count / (float)Corridor.Count;
        }

        /// <summary>
        /// Posts of the smallest piece of wall that stands on its own. 1 is a lonely post in the middle of an open
        /// square of four cells.
        /// </summary>
        public int SmallestWallPiece()
        {
            var seen = new HashSet<Vector2Int>();
            int smallest = int.MaxValue;
            for (int x = 0; x < Cells; x++)
            {
                for (int y = 0; y < Cells; y++)
                {
                    var start = new Vector2Int(x, y);
                    if (!seen.Add(start)) continue;

                    int size = 1;
                    var queue = new Queue<Vector2Int>();
                    queue.Enqueue(start);
                    while (queue.Count > 0)
                    {
                        foreach (var (to, a, b) in WallsAt(queue.Dequeue()))
                        {
                            var next = Wrap(to);
                            if (!Wall(a, b) || !seen.Add(next)) continue;
                            size++;
                            queue.Enqueue(next);
                        }
                    }
                    smallest = Mathf.Min(smallest, size);
                }
            }
            return smallest;
        }

        /// <summary>The whole plan in one line: two plans with the same signature are the same maze.</summary>
        public string Signature()
        {
            ulong hash = 1469598103934665603UL;
            foreach (var t in Opened.OrderBy(t => t.x * 10000 + t.y)) hash = (hash ^ (ulong)(t.x * 10000 + t.y)) * 1099511628211UL;
            string slots = string.Join(",", Slots.Select(s => $"{s.Size.ToString()[0]}{s.Cell.x}.{s.Cell.y}"));
            return $"{slots}:{Loops}:{DeadEnds().Count}:{Opened.Count}:{hash}";
        }
    }
}
