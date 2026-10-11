using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Checks of the maze plans the presets make: Mayham > Level > Check Maze. Nothing in the scene or in the
    /// settings asset is touched: the settings of the checks exist only in memory.
    /// </summary>
    public static class MazeSelfTest
    {
        // The maze as it was before it got settings, for the seed the scenes were built with and a few more:
        // slots : loops : dead ends : open wall tiles : hash of them : the next number of the dice.
        private static readonly Dictionary<int, string> Classic = new Dictionary<int, string>
        {
            { 4242, "M5.5,L20.5,S38.4,S4.20,M20.22,S41.22,L4.39,L22.38,M40.37:29:146:6213:1471954069111023333:371253568" },
            { 1, "S6.3,L20.3,L39.5,S6.21,M22.23,L39.22,M3.37,M21.40,S37.40:32:153:6222:5555452938621965239:1783790949" },
            { 2, "M3.6,L20.3,S39.4,M4.23,L20.22,S37.20,L4.37,S23.41,M40.40:21:155:6189:16487323927388171893:1919385078" },
            { 3, "M4.5,M23.6,S37.3,L5.20,S24.23,M38.21,L4.38,L20.39,S39.39:27:158:6207:6748043155384438561:399716307" },
            { 77, "S6.3,S22.5,L38.5,S3.21,L22.22,M37.20,L5.37,M23.38,M37.37:19:144:6183:5994603981026575733:1874395726" },
        };

        private static readonly int[] Seeds = { 11, 222, 3333, 44444, 5, 60 };

        private class Average
        {
            public float Blind, Deepest, DeadEnds, Loops, Walk;
        }

        [MenuItem("Mayham/Level/Check Maze")]
        public static void RunMenu() => Debug.Log(Run());

        /// <summary>"PASS: N checks", or "FAIL" with one line per failed check.</summary>
        public static string Run()
        {
            var t = new QuestSelfTest.Checks { Group = "maze" };
            var made = new List<MazeSettings>();
            MazeSettings Settings(MazePreset preset, int seed, Action<SerializedObject> change = null)
            {
                var settings = MazeSettings.Temporary(preset, seed);
                made.Add(settings);
                if (change != null)
                {
                    var so = new SerializedObject(settings);
                    change(so);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                return settings;
            }

            try
            {
                foreach (var pair in Classic)
                {
                    var layout = new MazeLayout(pair.Key, Settings(MazePreset.Classic, pair.Key));
                    t.Eq(layout.Signature() + ":" + layout.Rng.Next(), pair.Value, $"classic, seed {pair.Key}: the maze is the one it has always been");
                }

                var averages = new Dictionary<MazePreset, Average>();
                foreach (MazePreset preset in Enum.GetValues(typeof(MazePreset)))
                {
                    var sum = new Average();
                    foreach (int seed in Seeds)
                    {
                        var settings = Settings(preset, seed);
                        var layout = new MazeLayout(seed, settings);
                        var reach = layout.Distances(MazeLayout.Spawn);
                        t.Eq(reach.Count, layout.Corridor.Count, $"{preset}, seed {seed}: every corridor cell can be reached from the start");
                        int smallest = layout.SmallestWallPiece();
                        t.Ok(smallest >= settings.MinWallPiece, $"{preset}, seed {seed}: no piece of wall is smaller than {settings.MinWallPiece} posts (the smallest has {smallest})");
                        var exit = layout.ExitCell();
                        t.Ok(layout.Corridor.Contains(exit) && exit != MazeLayout.Spawn && !layout.Avenue.Contains(exit), $"{preset}, seed {seed}: the exit is a corridor cell off the avenues");
                        t.Eq(new MazeLayout(seed, settings).Signature(), layout.Signature(), $"{preset}, seed {seed}: the same seed and settings give the same maze");

                        sum.Blind += layout.BlindShare(out int deepest);
                        sum.Deepest += deepest;
                        sum.DeadEnds += layout.DeadEnds().Count;
                        sum.Loops += layout.Loops + layout.LedOn;
                        sum.Walk += (float)reach.Values.Average();
                    }
                    averages[preset] = new Average
                    {
                        Blind = sum.Blind / Seeds.Length, Deepest = sum.Deepest / Seeds.Length, DeadEnds = sum.DeadEnds / Seeds.Length,
                        Loops = sum.Loops / Seeds.Length, Walk = sum.Walk / Seeds.Length
                    };
                }

                // What the presets are for, on the average of the seeds above.
                var classic = averages[MazePreset.Classic];
                var loops = averages[MazePreset.Loops];
                var shortWalls = averages[MazePreset.LoopsShortWalls];
                var avenues = averages[MazePreset.Avenues];
                t.Ok(classic.Blind > 0.3f && classic.Deepest > 30f, $"classic: a third of the maze and more is blind branches, and they are deep ({classic.Blind:0.00}, {classic.Deepest:0})");
                t.Ok(loops.Loops > classic.Loops * 2.5f, $"more loops: several times the openings of the classic maze ({loops.Loops:0} against {classic.Loops:0})");
                t.Ok(loops.Blind < 0.3f && loops.Deepest < 25f, $"more loops: fewer and shallower blind branches ({loops.Blind:0.00}, {loops.Deepest:0})");
                t.Ok(loops.Walk < classic.Walk * 0.7f, $"more loops: a shorter walk from the start ({loops.Walk:0} against {classic.Walk:0})");
                t.Ok(shortWalls.Blind < 0.17f && shortWalls.Deepest < 15f, $"shorter walls: few and shallow blind branches ({shortWalls.Blind:0.00}, {shortWalls.Deepest:0})");
                t.Ok(shortWalls.DeadEnds < classic.DeadEnds * 0.85f, $"shorter walls: fewer dead ends ({shortWalls.DeadEnds:0} against {classic.DeadEnds:0})");
                t.Ok(shortWalls.Walk < classic.Walk * 0.6f, $"shorter walls: a much shorter walk from the start ({shortWalls.Walk:0} against {classic.Walk:0})");
                t.Ok(avenues.Walk < classic.Walk * 0.6f, $"avenues: a much shorter walk from the start ({avenues.Walk:0} against {classic.Walk:0})");

                // Dead ends that are led on: the same maze with and without.
                foreach (int seed in Seeds.Take(3))
                {
                    var without = new MazeLayout(seed, Settings(MazePreset.LoopsShortWalls, seed, so => so.FindProperty("deadEndsLedOn").floatValue = 0f));
                    var with = new MazeLayout(seed, Settings(MazePreset.LoopsShortWalls, seed));
                    t.Eq(without.LedOn, 0, $"seed {seed}: no dead end is led on when the settings say none");
                    t.Ok(with.LedOn > 0, $"seed {seed}: dead ends are led on when the settings say so");
                    t.Ok(with.DeadEnds().Count < without.DeadEnds().Count, $"seed {seed}: and fewer of them are left ({with.DeadEnds().Count} against {without.DeadEnds().Count})");
                }

                // Avenues: straight and open from end to end, the start on a crossing, gates into the blocks.
                foreach (int seed in Seeds.Take(3))
                {
                    var grid = new MazeLayout(seed, Settings(MazePreset.Avenues, seed));
                    int lines = MazeLayout.Regions;
                    t.Eq(grid.Avenue.Count, 2 * lines * MazeLayout.Cells - lines * lines, $"seed {seed}: the grid has an avenue along every border between the regions");
                    t.Ok(grid.Avenue.Contains(MazeLayout.Spawn), $"seed {seed}: the start is on a crossing of avenues");
                    bool open = true;
                    for (int i = 0; i < lines; i++)
                    {
                        int line = i * MazeLayout.Region;
                        for (int k = 0; k < MazeLayout.Cells; k++)
                        {
                            open &= grid.Linked[new Vector2Int(k, line)].Contains(MazeLayout.Wrap(new Vector2Int(k + 1, line)));
                            open &= grid.Linked[new Vector2Int(line, k)].Contains(MazeLayout.Wrap(new Vector2Int(line, k + 1)));
                        }
                    }
                    t.Ok(open, $"seed {seed}: every avenue is open from end to end");
                    int gates = grid.Avenue.Sum(c => grid.Linked[c].Count(n => !grid.Avenue.Contains(n)));
                    t.Ok(gates >= 40, $"seed {seed}: the blocks are entered through many gates ({gates})");

                    var cross = new MazeLayout(seed, Settings(MazePreset.Avenues, seed, so => so.FindProperty("avenues").enumValueIndex = (int)MazeAvenues.Cross));
                    t.Eq(cross.Avenue.Count, 2 * MazeLayout.Cells - 1, $"seed {seed}: the cross is two avenues");
                    t.Eq(cross.Distances(MazeLayout.Spawn).Count, cross.Corridor.Count, $"seed {seed}: and reaches every cell too");
                }
            }
            finally
            {
                foreach (var settings in made)
                {
                    if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
                }
            }

            return t.Failures.Count == 0
                ? $"PASS: {t.Count} checks"
                : $"FAIL: {t.Failures.Count} of {t.Count} checks\n - " + string.Join("\n - ", t.Failures.Take(30));
        }
    }
}
