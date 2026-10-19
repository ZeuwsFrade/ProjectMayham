using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Checks of the quest rules that run in the editor without Play mode and without touching the save file:
    /// Mayham > Quests > Run Self-Test. Every group works on quests that exist only in memory.
    /// </summary>
    public static partial class QuestSelfTest
    {
        /// <summary>Counts checks and collects what failed.</summary>
        public class Checks
        {
            public int Count;
            public string Group = string.Empty;
            public readonly List<string> Failures = new List<string>();

            public void Ok(bool condition, string what)
            {
                Count++;
                if (!condition) Failures.Add($"[{Group}] {what}");
            }

            public void Eq<T>(T actual, T expected, string what)
            {
                Count++;
                if (!EqualityComparer<T>.Default.Equals(actual, expected))
                    Failures.Add($"[{Group}] {what}: expected '{expected}', got '{actual}'");
            }
        }

        // A group that has no body yet compiles to nothing: every task adds the file with its group.
        static partial void DataTests(Checks t);
        static partial void LogTests(Checks t);
        static partial void TextTests(Checks t);
        static partial void DialogueTests(Checks t);
        static partial void TrackerTests(Checks t);
        static partial void ValidatorTests(Checks t);
        static partial void NpcTests(Checks t);
        static partial void DailyTests(Checks t);

        [MenuItem("Mayham/Quests/Run Self-Test")]
        public static void RunMenu() => Debug.Log(Run());

        /// <summary>Runs every group. "PASS: N checks", or "FAIL" with one line per failed check.</summary>
        public static string Run()
        {
            // The checks put their own data in place of the game session's; a running game would show it.
            if (EditorApplication.isPlaying) return "FAIL: leave Play mode first";

            var t = new Checks();
            // The checks feed the catalog broken quests on purpose: its warnings would only bury real ones.
            var filter = Debug.unityLogger.filterLogType;
            Debug.unityLogger.filterLogType = LogType.Error;
            try
            {
                Group(t, "data", () => DataTests(t));
                Group(t, "log", () => LogTests(t));
                Group(t, "text", () => TextTests(t));
                Group(t, "dialogue", () => DialogueTests(t));
                Group(t, "tracker", () => TrackerTests(t));
                Group(t, "validator", () => ValidatorTests(t));
                Group(t, "npcs", () => NpcTests(t));
                Group(t, "daily", () => DailyTests(t));
            }
            finally
            {
                Debug.unityLogger.filterLogType = filter;
            }

            return t.Failures.Count == 0
                ? $"PASS: {t.Count} checks"
                : $"FAIL: {t.Failures.Count} of {t.Count} checks\n - " + string.Join("\n - ", t.Failures);
        }

        private static void Group(Checks t, string name, Action run)
        {
            t.Group = name;
            try
            {
                run();
            }
            catch (Exception e)
            {
                t.Count++;
                t.Failures.Add($"[{name}] exception: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}
