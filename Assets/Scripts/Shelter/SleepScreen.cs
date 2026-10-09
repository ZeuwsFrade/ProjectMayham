using System.Collections;
using ProjectMayham.Core;
using ProjectMayham.Player;
using ProjectMayham.UI;
using TMPro;
using UnityEngine;

namespace ProjectMayham.Shelter
{
    /// <summary>The fade to black while the player sleeps: health is restored while the screen is dark.</summary>
    public class SleepScreen : ModalWindow
    {
        private CanvasGroup group;
        private TMP_Text label;

        protected override int SortingOrder => 500;

        public static void Sleep(PlayerStats stats)
        {
            var screen = Get<SleepScreen>();
            if (screen.IsOpen) return;
            screen.Show();
            screen.StartCoroutine(screen.Run(stats));
        }

        protected override void Build(Canvas canvas)
        {
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            var back = UIKit.Panel(canvas.transform, "Black", Color.black);
            UIKit.Stretch(back.rectTransform);

            label = UIKit.Label(canvas.transform, string.Empty, 40f, TextAlignmentOptions.Center);
            UIKit.Place(label.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 100f));
        }

        // Nothing to cancel: the player sleeps until morning.
        protected override void OnCancel() { }

        private IEnumerator Run(PlayerStats stats)
        {
            label.text = string.Empty;
            group.alpha = 0f;
            yield return Fade(1f, 0.8f);

            if (stats != null) stats.RestoreAll();
            GameSession.Save();
            label.text = "Вы отдохнули. Здоровье восстановлено.";
            yield return Wait(1.6f);

            label.text = string.Empty;
            yield return Fade(0f, 0.8f);
            Close();
        }

        private IEnumerator Fade(float target, float duration)
        {
            float from = group.alpha;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, target, t / duration);
                yield return null;
            }
            group.alpha = target;
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }
    }
}
