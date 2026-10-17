using System.Linq;
using ProjectMayham.Player;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// The Inspector of <see cref="PlayerFootsteps"/>: the kind of sound first, then only its fields. A track of several
    /// steps is searched for steps as soon as it is set or its settings change, and what was found is shown with the
    /// stride that walks in the tempo of the recording.
    /// </summary>
    [CustomEditor(typeof(PlayerFootsteps))]
    public class PlayerFootstepsEditor : Editor
    {
        // In the order of PlayerFootsteps.SoundKind.
        private static readonly string[] KindNames = { "Отдельные шаги", "Дорожка из нескольких шагов", "Дорожка целиком (по кругу)" };
        /// <summary>A "step" longer than this is probably a recording of several steps.</summary>
        public const float LongForOneStep = 1.2f;

        private static readonly string[] Own =
        {
            "m_Script", "kind", "steps", "track", "trackSteps", "trackGains", "trackThreshold", "trackEvenVolume", "trackStepLength",
            "loopTrack", "loopSprintTrack", "loopFade", "loopRate",
        };
        // A loop does not walk by strides and is not varied step by step.
        private static readonly string[] StepOnly = { "stride", "pitchRange", "volumeJitter" };

        private string lastError;

        public static bool LooksLikeTrack(AudioClip clip) => clip != null && clip.length > LongForOneStep;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var kind = serializedObject.FindProperty("kind");
            int picked = EditorGUILayout.Popup(new GUIContent("Звук шагов",
                "Отдельные шаги — каждая запись один шаг. Дорожка из нескольких шагов — игра проигрывает её по шагу. " +
                "Дорожка целиком — играет по кругу, пока игрок идёт, и с начала после каждой остановки."),
                kind.enumValueIndex, KindNames);
            if (picked != kind.enumValueIndex) kind.enumValueIndex = picked;

            var chosen = (PlayerFootsteps.SoundKind)picked;
            if (chosen == PlayerFootsteps.SoundKind.Track) TrackGUI();
            else if (chosen == PlayerFootsteps.SoundKind.Loop) LoopGUI();
            else StepsGUI();

            EditorGUILayout.Space();
            DrawPropertiesExcluding(serializedObject, chosen == PlayerFootsteps.SoundKind.Loop ? Own.Concat(StepOnly).ToArray() : Own);
            serializedObject.ApplyModifiedProperties();
        }

        private void StepsGUI()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("steps"), new GUIContent("Записи шагов", "На каждый шаг звучит случайная из них."), true);
            var steps = serializedObject.FindProperty("steps");
            for (int i = 0; i < steps.arraySize; i++)
            {
                var clip = steps.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
                if (!LooksLikeTrack(clip)) continue;
                EditorGUILayout.HelpBox($"«{clip.name}» длиной {clip.length:0.0} с — похоже на дорожку из нескольких шагов. " +
                    "Выберите «Дорожка из нескольких шагов», иначе на каждый шаг будет звучать вся запись.", MessageType.Warning);
            }
        }

        private void TrackGUI()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("track"), new GUIContent("Дорожка", "Одна запись из нескольких шагов."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("trackThreshold"),
                new GUIContent("Порог шага", "Тише этой доли от самого громкого шага — не шаг. Шаги не находятся — уменьшите, лишние — увеличьте."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("trackEvenVolume"),
                new GUIContent("Выравнивать громкость", "Слишком громкие шаги делаются как обычные."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("trackStepLength"),
                new GUIContent("Самый длинный шаг, с", "Шаг заканчивается здесь или там, где начинается следующий."));
            bool changed = EditorGUI.EndChangeCheck();

            if (GUILayout.Button("Найти шаги заново")) changed = true;
            if (changed) Find();

            var track = serializedObject.FindProperty("track").objectReferenceValue as AudioClip;
            var starts = serializedObject.FindProperty("trackSteps");
            if (track == null)
            {
                EditorGUILayout.HelpBox("Перетащите сюда запись из нескольких шагов — шаги в ней найдутся сами.", MessageType.Info);
                return;
            }
            if (!string.IsNullOrEmpty(lastError))
            {
                EditorGUILayout.HelpBox("Шаги не найдены: " + lastError, MessageType.Error);
                return;
            }
            if (starts.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Шаги не найдены. Уменьшите порог шага или нажмите «Найти шаги заново».", MessageType.Warning);
                return;
            }

            var times = Enumerable.Range(0, starts.arraySize).Select(i => starts.GetArrayElementAtIndex(i).floatValue).ToList();
            string text = $"Найдено шагов: {times.Count}.";
            if (times.Count >= 2)
            {
                float interval = Median(times.Zip(times.Skip(1), (a, b) => b - a).ToList());
                text += $" В записи шаг примерно раз в {interval:0.00} с.";
                float walk = WalkSpeed();
                if (walk > 0f) text += $" Чтобы при ходьбе игрок шагал в том же темпе, длина шага ≈ {walk * interval:0.0}.";
            }
            text += "\nНачала шагов, с: " + string.Join(" · ", times.Select(t => t.ToString("0.00")));
            EditorGUILayout.HelpBox(text, MessageType.Info);
        }

        private void LoopGUI()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loopTrack"),
                new GUIContent("Дорожка ходьбы", "Играет по кругу, пока игрок идёт; после каждой остановки — с начала."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loopSprintTrack"),
                new GUIContent("Дорожка бега", "Необязательно. Пусто — при беге играет дорожка ходьбы, чуть быстрее."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loopFade"),
                new GUIContent("Появление и затухание, с", "Сколько дорожка нарастает, когда игрок пошёл, и затихает, когда остановился."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loopRate"),
                new GUIContent("Скорость проигрывания", "От и до: дорожка играет быстрее или медленнее вслед за скоростью игрока. " +
                    "Шире — шаги лучше успевают за игроком, но звук заметнее выше или ниже."));

            var walk = serializedObject.FindProperty("loopTrack").objectReferenceValue as AudioClip;
            var run = serializedObject.FindProperty("loopSprintTrack").objectReferenceValue as AudioClip;
            if (walk == null)
            {
                EditorGUILayout.HelpBox("Перетащите сюда запись ходьбы из нескольких шагов.", MessageType.Info);
                return;
            }
            foreach (var clip in new[] { walk, run })
            {
                if (clip == null || LooksLikeTrack(clip)) continue;
                EditorGUILayout.HelpBox($"«{clip.name}» длиной {clip.length:0.00} с — похоже на один шаг: по кругу он будет звучать как метроном. " +
                    "Для одного шага лучше «Отдельные шаги».", MessageType.Warning);
            }
            if (run == null)
                EditorGUILayout.HelpBox("Дорожки бега нет: при беге играет дорожка ходьбы, ускоренная не больше чем до верхней скорости проигрывания.", MessageType.None);
        }

        private void Find()
        {
            serializedObject.ApplyModifiedProperties();
            var result = FootstepTrackAnalyzer.Fill(serializedObject);
            lastError = result.Error;
            serializedObject.ApplyModifiedProperties();
        }

        private float WalkSpeed()
        {
            var component = target as Component;
            if (component == null || !component.TryGetComponent<PlayerController>(out var controller)) return 0f;
            var speed = new SerializedObject(controller).FindProperty("walkSpeed");
            return speed != null ? speed.floatValue : 0f;
        }

        private static float Median(System.Collections.Generic.List<float> values)
        {
            if (values.Count == 0) return 0f;
            values.Sort();
            int middle = values.Count / 2;
            return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) * 0.5f;
        }
    }
}
