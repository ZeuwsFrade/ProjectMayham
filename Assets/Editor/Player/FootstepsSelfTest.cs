using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Player;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Checks of the footsteps without Play mode: finding the steps in a track (a made-up one with known steps and the
    /// one of the project), the parts of a track that are steps, the Inspector's warning about a track among single
    /// steps and the prefab builder. Mayham > Player > Footsteps Self-Test.
    /// </summary>
    public static class FootstepsSelfTest
    {
        private const int Rate = 48000;

        [MenuItem("Mayham/Player/Footsteps Self-Test")]
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

            // A made-up track: quiet noise and four steps of different loudness; the first has a toe tap after the heel.
            var times = new[] { 0.2f, 0.8f, 1.5f, 2.3f };
            var loudness = new[] { 0.8f, 0.3f, 0.6f, 0.2f };
            var clip = MadeTrack(times, loudness, 0.15f);
            try
            {
                var found = FootstepTrackAnalyzer.Analyze(clip, 0.18f, true, 0.45f);
                Ok(found.Error == null, "a made-up track is read: " + found.Error);
                Ok(found.Starts.Length == 4, $"four steps are found and the toe tap is not one ({found.Starts.Length}: {string.Join(", ", found.Starts.Select(s => s.ToString("0.000")))})");
                for (int i = 0; i < Mathf.Min(times.Length, found.Starts.Length); i++)
                    Ok(Mathf.Abs(found.Starts[i] - (times[i] - FootstepTrackAnalyzer.PreRoll)) < 0.02f, $"step {i + 1} begins just before its attack ({found.Starts[i]:0.000})");
                Ok(found.Gains.Length == found.Starts.Length && found.Gains.All(g => g > 0f && g <= 1f), "every step has a volume between 0 and 1");
                if (found.Gains.Length == 4)
                    Ok(found.Gains[0] < found.Gains[1] && Mathf.Approximately(found.Gains[3], 1f), "the loud step is turned down, the quiet one is left");
                Ok(Mathf.Abs(found.Interval - 0.7f) < 0.05f, $"the usual time between the steps ({found.Interval:0.00})");

                var plain = FootstepTrackAnalyzer.Analyze(clip, 0.18f, false, 0.45f);
                Ok(plain.Gains.All(g => g == 1f), "without evening out every step keeps its loudness");
                var strict = FootstepTrackAnalyzer.Analyze(clip, 0.6f, true, 0.45f);
                Ok(strict.Starts.Length >= 1 && strict.Starts.Length < found.Starts.Length, $"a higher threshold finds fewer steps ({strict.Starts.Length})");
                Ok(PlayerFootstepsEditor.LooksLikeTrack(clip), "a recording of 3 s is taken for a track");
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }

            var single = AudioClip.Create("one step", Rate * 2 / 5, 1, Rate, false);
            Ok(!PlayerFootstepsEditor.LooksLikeTrack(single), "a recording of 0.4 s is one step");
            Object.DestroyImmediate(single);

            // The parts of a track.
            var starts = new[] { 0f, 0.5f, 2f };
            Ok(PlayerFootsteps.TrackSegment(starts, 0, 2.3f, 0.45f) == (0f, 0.45f), "a step ends at its longest");
            Ok(PlayerFootsteps.TrackSegment(starts, 0, 2.3f, 1f) == (0f, 0.5f), "or where the next step begins");
            Ok(PlayerFootsteps.TrackSegment(starts, 2, 2.3f, 0.45f) == (2f, 2.3f), "the last one at the end of the track");

            // The speed of the loop follows the player, within its range.
            var range = new Vector2(0.85f, 1.15f);
            Ok(Mathf.Approximately(PlayerFootsteps.LoopRate(3.5f, 3.5f, range), 1f), "at the speed of the recording the loop plays as recorded");
            Ok(Mathf.Approximately(PlayerFootsteps.LoopRate(3.8f, 3.5f, range), 3.8f / 3.5f), "a little faster with a faster player");
            Ok(Mathf.Approximately(PlayerFootsteps.LoopRate(6f, 3.5f, range), 1.15f), "but not faster than the range allows");
            Ok(Mathf.Approximately(PlayerFootsteps.LoopRate(1f, 3.5f, range), 0.85f), "and not slower");
            Ok(Mathf.Approximately(PlayerFootsteps.LoopRate(6f, 3.5f, new Vector2(1.15f, 0.85f)), 1.15f), "a range given the wrong way round still works");
            Ok(Mathf.Approximately(PlayerFootsteps.LoopRate(2f, 0f, range), 1f), "without a recorded speed the loop plays as recorded");

            // The prefab builder: the track of the project with its steps, but only for a component without a sound.
            var go = new GameObject("footsteps self-test") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var footsteps = go.AddComponent<PlayerFootsteps>();
                var so = new SerializedObject(footsteps);
                PlayerPrefabBuilder.SetFootsteps(so);
                so.ApplyModifiedPropertiesWithoutUndo();
                var track = so.FindProperty("track").objectReferenceValue as AudioClip;
                int count = so.FindProperty("trackSteps").arraySize;
                Ok(footsteps.Kind == PlayerFootsteps.SoundKind.Track && track != null, "the builder gives a new player the track of the project");
                Ok(count >= 10, $"with its steps found ({count})");
                Ok(so.FindProperty("loopTrack").objectReferenceValue == track, "and the same recording for the loop");

                so.FindProperty("kind").enumValueIndex = (int)PlayerFootsteps.SoundKind.Steps;
                so.FindProperty("track").objectReferenceValue = null;
                so.FindProperty("steps").arraySize = 1;
                so.FindProperty("steps").GetArrayElementAtIndex(0).objectReferenceValue = track;
                so.ApplyModifiedPropertiesWithoutUndo();
                PlayerPrefabBuilder.SetFootsteps(so);
                so.ApplyModifiedPropertiesWithoutUndo();
                Ok(footsteps.Kind == PlayerFootsteps.SoundKind.Steps && so.FindProperty("steps").arraySize == 1, "a sound chosen by hand is kept");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            return failures.Count == 0
                ? $"PASS: {checks} checks"
                : $"FAIL: {failures.Count} of {checks} checks\n - " + string.Join("\n - ", failures);
        }

        // Three seconds of quiet noise with a decaying burst for every step; the first step also has a smaller tap
        // a little after it (heel and toe), which must not count as a step of its own.
        private static AudioClip MadeTrack(float[] times, float[] loudness, float toeAfter)
        {
            var random = new System.Random(7);
            var data = new float[Rate * 3];
            for (int i = 0; i < data.Length; i++) data[i] = ((float)random.NextDouble() * 2f - 1f) * 0.01f;

            void Burst(float at, float level)
            {
                int from = Mathf.RoundToInt(at * Rate);
                for (int i = 0; i < Rate * 12 / 100 && from + i < data.Length; i++)
                    data[from + i] += ((float)random.NextDouble() * 2f - 1f) * level * Mathf.Exp(-i / (Rate * 0.03f));
            }

            for (int k = 0; k < times.Length; k++) Burst(times[k], loudness[k]);
            Burst(times[0] + toeAfter, loudness[0] * 0.35f);

            var clip = AudioClip.Create("made-up steps", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
