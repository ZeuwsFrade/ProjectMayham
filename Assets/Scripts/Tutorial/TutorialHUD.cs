using System.Collections.Generic;
using ProjectMayham.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMayham.Tutorial
{
    /// <summary>
    /// What the tutorial shows on screen: the goals of the stage in the upper right corner (done ones struck
    /// through) and a hint card low in the middle of the screen that goes away by itself. Built from code.
    /// </summary>
    public class TutorialHUD : MonoBehaviour
    {
        private static readonly Color DoneColor = new Color(0.55f, 0.85f, 0.55f, 0.9f);

        private RectTransform goalsPanel;
        private TMP_Text stageLabel;
        private readonly List<TMP_Text> goalLabels = new List<TMP_Text>();
        private GameObject hintRoot;
        private TMP_Text hintLabel;
        private float hintUntil;

        public static TutorialHUD Create(Transform parent)
        {
            var hud = new GameObject("TutorialHUD").AddComponent<TutorialHUD>();
            hud.transform.SetParent(parent, false);
            hud.Build();
            return hud;
        }

        /// <summary>The hint on screen now, or empty.</summary>
        public string Hint => hintRoot != null && hintRoot.activeSelf ? hintLabel.text : string.Empty;

        private void Build()
        {
            var canvas = UIKit.NewCanvas("TutorialCanvas", 45, transform);

            goalsPanel = UIKit.Panel(canvas.transform, "Goals", new Color(0f, 0f, 0f, 0.55f)).rectTransform;
            // Under the money in the corner.
            UIKit.Place(goalsPanel, new Vector2(1f, 1f), new Vector2(-24f, -90f), new Vector2(560f, 100f));
            Stack(goalsPanel, 6f, new RectOffset(20, 20, 14, 16));

            stageLabel = UIKit.Label(goalsPanel, string.Empty, 28f, TextAlignmentOptions.Left, UIKit.Accent);

            // Low in the middle, above what is in the hands, growing upwards: clear of the goals and of the player.
            var hint = UIKit.Panel(canvas.transform, "Hint", new Color(0.09f, 0.1f, 0.13f, 0.92f)).rectTransform;
            UIKit.Place(hint, new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(1000f, 80f));
            Stack(hint, 0f, new RectOffset(28, 28, 16, 18));
            hintLabel = UIKit.Label(hint, string.Empty, 28f, TextAlignmentOptions.Center);
            hintRoot = hint.gameObject;
            hintRoot.SetActive(false);
        }

        // A panel that grows down with its lines.
        private static void Stack(RectTransform panel, float spacing, RectOffset padding)
        {
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>Shows the stage and its goals. <paramref name="done"/> says which goals are reached.</summary>
        public void ShowGoals(string stage, IReadOnlyList<string> goals, IReadOnlyList<bool> done)
        {
            stageLabel.text = stage;
            while (goalLabels.Count < goals.Count)
            {
                var label = UIKit.Label(goalsPanel, string.Empty, 24f);
                label.overflowMode = TextOverflowModes.Overflow;
                goalLabels.Add(label);
            }
            for (int i = 0; i < goalLabels.Count; i++)
            {
                var label = goalLabels[i];
                bool shown = i < goals.Count;
                label.gameObject.SetActive(shown);
                if (!shown) continue;
                label.text = done[i] ? $"<s>• {goals[i]}</s>" : $"• {goals[i]}";
                label.color = done[i] ? DoneColor : Color.white;
            }
        }

        /// <summary>Shows a hint for a while; a new one replaces the old.</summary>
        public void ShowHint(string text, float seconds = 9f)
        {
            if (string.IsNullOrEmpty(text)) return;
            hintLabel.text = text;
            hintRoot.SetActive(true);
            hintUntil = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            if (hintRoot.activeSelf && Time.unscaledTime >= hintUntil) hintRoot.SetActive(false);
        }
    }
}
