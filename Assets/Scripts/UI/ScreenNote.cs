using TMPro;
using UnityEngine;

namespace ProjectMayham.UI
{
    /// <summary>
    /// A short note in the upper middle of the screen that goes away by itself, for what a wall map tells. One at a
    /// time: a new note replaces the old. Lives in the scene it was shown in.
    /// </summary>
    public class ScreenNote : MonoBehaviour
    {
        private static ScreenNote instance;

        private TMP_Text label;
        private float until;

        /// <summary>The note on screen now, or empty.</summary>
        public static string Current => instance != null && instance.label.gameObject.activeSelf ? instance.label.text : string.Empty;

        public static void Show(string text, float seconds = 5f)
        {
            if (instance == null)
            {
                var canvas = UIKit.NewCanvas("ScreenNote", 50);
                instance = canvas.gameObject.AddComponent<ScreenNote>();
                // Lower than the note of the quests ("Отмечено: …"), so the two never cover each other.
                instance.label = UIKit.Label(canvas.transform, string.Empty, 34f, TextAlignmentOptions.Center, UIKit.Accent);
                UIKit.Place(instance.label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(1100f, 60f));
                instance.label.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
            instance.label.text = text;
            instance.label.gameObject.SetActive(true);
            // Unscaled: a window that pauses the game does not keep the note up.
            instance.until = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            if (label.gameObject.activeSelf && Time.unscaledTime >= until) label.gameObject.SetActive(false);
        }
    }
}
