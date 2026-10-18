using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Checks of the raid exit and the wall maps (Mayham > Level > Check Exit and Wall Maps): the compass words, what
    /// a map says, when Esc leaves a raid, and — in an open raid scene whose generator has the prefabs — 20 raids:
    /// one exit on corridor floor far from the start, the maps on straight corridor walls and apart.
    /// </summary>
    public static class ExitSelfTest
    {
        [MenuItem("Mayham/Level/Check Exit and Wall Maps (open raid scene)")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            var failures = new List<string>();
            int checks = 0;
            void Ok(bool condition, string what)
            {
                checks++;
                if (!condition) failures.Add(what);
            }

            // ---- words ----
            var points = new (Vector2 delta, string words)[]
            {
                (new Vector2(0f, 10f), "на севере"), (new Vector2(10f, 10f), "на северо-востоке"), (new Vector2(10f, 0f), "на востоке"),
                (new Vector2(10f, -10f), "на юго-востоке"), (new Vector2(0f, -10f), "на юге"), (new Vector2(-10f, -10f), "на юго-западе"),
                (new Vector2(-10f, 0f), "на западе"), (new Vector2(-10f, 10f), "на северо-западе"),
                (new Vector2(10f, 3f), "на востоке"), (new Vector2(10f, 5f), "на северо-востоке"), (new Vector2(1f, 1f), CompassText.Here),
            };
            foreach (var (delta, words) in points) Ok(CompassText.Describe(delta) == words, $"{delta} is '{words}', not '{CompassText.Describe(delta)}'");
            Ok(WallMap.Reading(Vector2.zero, new Vector2(0f, 20f)) == "Выход — на севере", "a map says where the exit is");
            Ok(WallMap.Reading(Vector2.zero, null) == "Выхода на карте нет", "a map of a raid without an exit says so");
            Ok(RaidController.CanLeaveByEscape(true, true) && RaidController.CanLeaveByEscape(false, false) && !RaidController.CanLeaveByEscape(false, true),
                "Esc leaves in a test scene or without an exit; with an exit only the exit does");

            // ---- raids ----
            var generator = Object.FindFirstObjectByType<RaidGenerator>();
            var so = generator != null ? new SerializedObject(generator) : null;
            if (so == null || so.FindProperty("exitPrefab").objectReferenceValue == null)
                return Result(failures, checks, " (no raid generator with an exit in the open scene: raids not checked)");
            if (EditorApplication.isPlaying) return Result(failures, checks, " (playing: raids not checked)");

            var floor = (Tilemap)so.FindProperty("floorMap").objectReferenceValue;
            var walls = (Tilemap)so.FindProperty("wallMap").objectReferenceValue;
            float exitDistance = so.FindProperty("exitDistance").floatValue;
            int mapCount = so.FindProperty("wallMapCount").intValue;
            float spacing = so.FindProperty("wallMapSpacing").floatValue;
            var keep = new List<Vector2>();
            var keepList = so.FindProperty("keepClear");
            for (int i = 0; i < keepList.arraySize; i++)
                if (keepList.GetArrayElementAtIndex(i).objectReferenceValue is Transform mark) keep.Add(mark.position);
            var slots = generator.GetComponentsInChildren<RoomSlot>();
            var wrap = Object.FindFirstObjectByType<World.WrapWorld>();
            float Distance(Vector2 a, Vector2 b) => wrap != null ? wrap.Delta(a, b).magnitude : (b - a).magnitude;
            Vector3Int Cell(Vector2 p) => new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), 0);

            int far = 0;
            try
            {
                for (int n = 1; n <= 20; n++)
                {
                    generator.Generate(n * 7919 + 13);
                    var exit = generator.Exit;
                    Ok(exit != null, $"raid {n}: there is an exit");
                    if (exit == null) continue;
                    Vector2 at = exit.transform.position;
                    var cell = Cell(at);
                    Ok(floor.HasTile(cell) && !walls.HasTile(cell) && !slots.Any(s => s.Covers(cell)), $"raid {n}: the exit is on corridor floor");
                    if (keep.All(k => Distance(k, at) >= exitDistance)) far++;

                    var maps = generator.WallMaps;
                    Ok(maps.Count == mapCount, $"raid {n}: {maps.Count} wall maps, not {mapCount}");
                    foreach (var map in maps)
                    {
                        // Up in the picture points into the wall it hangs on.
                        Vector2 up = map.transform.up;
                        var wallSide = new Vector3Int(Mathf.RoundToInt(up.x), Mathf.RoundToInt(up.y), 0);
                        var standing = Cell((Vector2)map.transform.position - up * 0.42f);
                        Ok(floor.HasTile(standing) && !walls.HasTile(standing) && !slots.Any(s => s.Covers(standing)) && walls.HasTile(standing + wallSide),
                            $"raid {n}: a map at {map.transform.position} hangs on a corridor wall");
                    }
                    for (int a = 0; a < maps.Count; a++)
                        for (int b = a + 1; b < maps.Count; b++)
                            Ok(Distance(maps[a].transform.position, maps[b].transform.position) >= spacing - 1f, $"raid {n}: two maps are too close");
                }
                Ok(far >= 18, $"the exit lies far from the start in {far} raids of 20");
            }
            finally
            {
                generator.Clear();
            }
            return Result(failures, checks, $" (exit far from the start in {far}/20 raids)");
        }

        private static string Result(List<string> failures, int checks, string note) =>
            failures.Count == 0
                ? $"PASS: {checks} checks{note}"
                : $"FAIL: {failures.Count} of {checks} checks{note}\n - " + string.Join("\n - ", failures);
    }
}
