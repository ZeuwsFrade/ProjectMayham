using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    /// <summary>
    /// Full-screen loading screen that survives scene changes. It fades in, loads the next scene in the background
    /// (showing a progress bar), waits a moment so it never just flashes, and fades out. Created on first use.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        private const float FadeTime = 0.35f;
        private const float MinimumTime = 1.0f;

        private static LoadingScreen instance;

        private CanvasGroup group;
        private TMP_Text titleLabel;
        private TMP_Text captionLabel;
        private RectTransform barFill;

        public static bool IsBusy => instance != null && instance.busy;
        private bool busy;

        /// <summary>Starts loading a scene behind the loading screen.</summary>
        public static void Show(string sceneName, string caption)
        {
            if (instance == null)
            {
                var go = new GameObject("LoadingScreen");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<LoadingScreen>();
                instance.Build();
            }
            if (instance.busy) return;
            instance.StartCoroutine(instance.Run(sceneName, caption));
        }

        private void Build()
        {
            var canvas = UIKit.NewCanvas("Canvas", 10000, transform);
            group = canvas.gameObject.AddComponent<CanvasGroup>();

            var back = UIKit.Panel(canvas.transform, "Back", new Color(0.03f, 0.03f, 0.04f, 1f));
            UIKit.Stretch(back.rectTransform);

            titleLabel = UIKit.Label(canvas.transform, "ЗАГРУЗКА", 56f, TextAlignmentOptions.Center);
            UIKit.Place(titleLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1200f, 90f));

            captionLabel = UIKit.Label(canvas.transform, string.Empty, 28f, TextAlignmentOptions.Center, UIKit.Muted);
            UIKit.Place(captionLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1200f, 60f));

            var bar = UIKit.Panel(canvas.transform, "Bar", new Color(1f, 1f, 1f, 0.12f)).rectTransform;
            UIKit.Place(bar, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(720f, 12f));
            var fill = UIKit.Panel(bar, "Fill", UIKit.Accent).rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            barFill = fill;

            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        private IEnumerator Run(string sceneName, string caption)
        {
            busy = true;
            titleLabel.text = "ЗАГРУЗКА";
            captionLabel.text = caption ?? string.Empty;
            SetProgress(0f);
            group.blocksRaycasts = true; // nothing underneath can be clicked while loading

            yield return Fade(1f);

            var operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = false;

            float started = Time.unscaledTime;
            // Loading stops at 0.9 until the scene is allowed to activate.
            while (operation.progress < 0.9f || Time.unscaledTime - started < MinimumTime)
            {
                SetProgress(Mathf.Min(operation.progress / 0.9f, (Time.unscaledTime - started) / MinimumTime));
                yield return null;
            }
            SetProgress(1f);

            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;
            yield return null; // let the new scene run its Awake/Start before it is shown

            yield return Fade(0f);
            group.blocksRaycasts = false;
            busy = false;
        }

        private IEnumerator Fade(float target)
        {
            float from = group.alpha;
            for (float t = 0f; t < FadeTime; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, target, t / FadeTime);
                yield return null;
            }
            group.alpha = target;
        }

        private void SetProgress(float value) => barFill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
    }
}
