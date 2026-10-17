using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Player;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Finds the steps in a recording of several steps for <see cref="PlayerFootsteps"/>: the loudness is measured in
    /// windows of 10 ms, and a step begins where it jumps well above the quiet before it and is loud enough. Steps
    /// closer than <see cref="MinGap"/> are one step (heel and toe). With "even volume" the loud steps are turned down
    /// to the usual (median) loudness; quiet ones are left as they are, a volume above 1 is not possible.
    /// </summary>
    public static class FootstepTrackAnalyzer
    {
        /// <summary>A step starts this long before its attack, so the attack is not cut.</summary>
        public const float PreRoll = 0.015f;
        public const float MinGap = 0.3f;
        private const float Window = 0.01f;
        private const float Jump = 2.2f;

        public class Result
        {
            public float[] Starts = new float[0];
            public float[] Gains = new float[0];
            /// <summary>The usual time between two steps of the recording, 0 with fewer than two.</summary>
            public float Interval;
            public string Error;
        }

        public static Result Analyze(AudioClip clip, float threshold, bool evenVolume, float maxStepLength)
        {
            var result = new Result();
            var mono = Samples(clip, out result.Error);
            if (mono == null) return result;

            int rate = clip.frequency;
            int window = Mathf.Max(1, Mathf.RoundToInt(rate * Window));
            int windows = mono.Length / window;
            var rms = new float[windows];
            float loudest = 0f;
            for (int w = 0; w < windows; w++)
            {
                double sum = 0;
                for (int i = w * window; i < (w + 1) * window; i++) sum += mono[i] * mono[i];
                rms[w] = (float)System.Math.Sqrt(sum / window);
                loudest = Mathf.Max(loudest, rms[w]);
            }
            if (loudest <= 0f)
            {
                result.Error = "запись беззвучная";
                return result;
            }

            var onsets = new List<int>();
            int gap = Mathf.RoundToInt(MinGap / Window);
            for (int w = 0; w < windows; w++)
            {
                float before = 0f;
                for (int k = 1; k <= 3; k++) before += w - k >= 0 ? rms[w - k] : 0f;
                before /= 3f;
                if (rms[w] < threshold * loudest || rms[w] <= Jump * before) continue;
                if (onsets.Count > 0 && w - onsets[onsets.Count - 1] < gap) continue;
                onsets.Add(w);
            }

            result.Starts = onsets.Select(w => Mathf.Max(0f, w * Window - PreRoll)).ToArray();
            result.Gains = Enumerable.Repeat(1f, onsets.Count).ToArray();
            if (onsets.Count >= 2)
            {
                var intervals = new List<float>();
                for (int k = 1; k < result.Starts.Length; k++) intervals.Add(result.Starts[k] - result.Starts[k - 1]);
                result.Interval = Median(intervals);
            }

            if (evenVolume && onsets.Count > 0)
            {
                var peaks = new float[onsets.Count];
                for (int k = 0; k < onsets.Count; k++)
                {
                    var (start, end) = PlayerFootsteps.TrackSegment(result.Starts, k, clip.length, maxStepLength);
                    int from = Mathf.Clamp(Mathf.RoundToInt(start * rate), 0, mono.Length);
                    int to = Mathf.Clamp(Mathf.RoundToInt(end * rate), from, mono.Length);
                    for (int i = from; i < to; i++) peaks[k] = Mathf.Max(peaks[k], Mathf.Abs(mono[i]));
                }
                float usual = Median(peaks);
                for (int k = 0; k < peaks.Length; k++) result.Gains[k] = peaks[k] > usual ? usual / peaks[k] : 1f;
            }
            return result;
        }

        /// <summary>
        /// Finds the steps of the track of a <see cref="PlayerFootsteps"/> with its own settings and writes them into
        /// it. Returns what was found (also when nothing was).
        /// </summary>
        public static Result Fill(SerializedObject footsteps)
        {
            var clip = footsteps.FindProperty("track").objectReferenceValue as AudioClip;
            var result = clip == null
                ? new Result { Error = "нет дорожки" }
                : Analyze(clip, footsteps.FindProperty("trackThreshold").floatValue, footsteps.FindProperty("trackEvenVolume").boolValue,
                    footsteps.FindProperty("trackStepLength").floatValue);
            Write(footsteps.FindProperty("trackSteps"), result.Starts);
            Write(footsteps.FindProperty("trackGains"), result.Gains);
            return result;
        }

        private static void Write(SerializedProperty list, float[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        // The samples mixed to mono. Unity gives them only for a clip decompressed on load: the import settings of an
        // asset are switched to that once (the default for short sounds anyway).
        private static float[] Samples(AudioClip clip, out string error)
        {
            error = null;
            if (clip == null)
            {
                error = "нет дорожки";
                return null;
            }

            string path = AssetDatabase.GetAssetPath(clip);
            if (!string.IsNullOrEmpty(path) && AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                var settings = importer.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.DecompressOnLoad || importer.loadInBackground)
                {
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    importer.defaultSampleSettings = settings;
                    importer.loadInBackground = false;
                    importer.SaveAndReimport();
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
            }

            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0))
            {
                error = "Unity не отдал звук этой записи";
                return null;
            }

            int channels = clip.channels;
            var mono = new float[clip.samples];
            for (int i = 0; i < mono.Length; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += data[i * channels + c];
                mono[i] = sum / channels;
            }
            return mono;
        }

        private static float Median(IEnumerable<float> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            if (sorted.Count == 0) return 0f;
            int middle = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) * 0.5f;
        }
    }
}
