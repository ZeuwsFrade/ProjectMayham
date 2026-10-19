using ProjectMayham.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    /// <summary>
    /// Hunger, thirst and the call of nature as three thin bars under the health and stamina bars, the progress of
    /// eating at the bottom of the screen and the messages of the needs. Built from code when the player appears
    /// (<see cref="PlayerNeeds"/> adds it), so no scene has to be changed. A bar is full when the need is at its worst.
    /// </summary>
    public class NeedsHUD : MonoBehaviour
    {
        private const float BarWidth = 260f;
        private const float BarHeight = 20f;
        private const float RowStep = 26f;

        private static readonly Color Back = new Color(0f, 0f, 0f, 0.55f);

        [Tooltip("Where the first bar sits, from the top-left corner of the screen.")]
        [SerializeField] private Vector2 origin = new Vector2(24f, -118f);
        [SerializeField] private Color hungerColor = new Color(0.95f, 0.6f, 0.2f);
        [SerializeField] private Color thirstColor = new Color(0.3f, 0.65f, 0.95f);
        [SerializeField] private Color natureColor = new Color(0.62f, 0.72f, 0.28f);
        [SerializeField, Min(0.5f)] private float noticeSeconds = 3f;

        private class Row
        {
            public Image fill;
            public TMP_Text label;
            public string title;
            public Color color;
            public int shownPercent = -1;
            public bool warning;
        }

        private readonly Row hunger = new Row();
        private readonly Row thirst = new Row();
        private readonly Row nature = new Row();
        private PlayerStats stats;
        private PlayerNeeds needs;
        private PlayerConsumer consumer;
        private RectTransform useRoot;
        private RectTransform useFrame;
        private Image useFill;
        private TMP_Text useLabel;
        private TMP_Text notice;
        private float noticeTimer;
        private string shownUse;

        private void Start()
        {
            stats = GetComponent<PlayerStats>();
            needs = stats != null ? stats.Needs : null;
            consumer = GetComponent<PlayerConsumer>();
            if (needs == null) { enabled = false; return; }

            var canvas = UIKit.NewCanvas("NeedsHUD", 40, transform);
            canvas.GetComponent<GraphicRaycaster>().enabled = false;

            hunger.title = "Голод"; hunger.color = hungerColor;
            thirst.title = "Жажда"; thirst.color = thirstColor;
            nature.title = "Зов природы"; nature.color = natureColor;
            BuildRow(canvas.transform, hunger, 0);
            BuildRow(canvas.transform, thirst, 1);
            BuildRow(canvas.transform, nature, 2);
            BuildUse(canvas.transform);

            notice = UIKit.Label(canvas.transform, string.Empty, 30f, TextAlignmentOptions.Center, UIKit.Accent);
            UIKit.Place(notice.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1000f, 60f));

            needs.Notice += OnNotice;
        }

        private void OnDestroy()
        {
            if (needs != null) needs.Notice -= OnNotice;
        }

        private void BuildRow(Transform parent, Row row, int index)
        {
            var frame = UIKit.Panel(parent, row.title, Back);
            UIKit.Place(frame.rectTransform, new Vector2(0f, 1f), origin + new Vector2(0f, -RowStep * index), new Vector2(BarWidth, BarHeight));
            frame.rectTransform.pivot = new Vector2(0f, 1f);
            frame.rectTransform.anchoredPosition = origin + new Vector2(0f, -RowStep * index);

            row.fill = UIKit.Panel(frame.transform, "Fill", row.color);
            var rect = row.fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            row.label = UIKit.Label(frame.transform, row.title, 15f, TextAlignmentOptions.Left);
            UIKit.Stretch(row.label.rectTransform);
            row.label.rectTransform.offsetMin = new Vector2(6f, 0f);
            row.label.textWrappingMode = TextWrappingModes.NoWrap;
            // The text is as tall as the bar: the default Ellipsis would cut it away.
            row.label.overflowMode = TextOverflowModes.Overflow;
        }

        private void BuildUse(Transform parent)
        {
            useRoot = UIKit.Rect("Use", parent);
            UIKit.Place(useRoot, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(360f, 44f));

            var frame = UIKit.Panel(useRoot, "Frame", Back);
            useFrame = frame.rectTransform;
            UIKit.Place(useFrame, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(360f, 12f));
            useFill = UIKit.Panel(frame.transform, "Fill", UIKit.Accent);
            var rect = useFill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            useLabel = UIKit.Label(useRoot, string.Empty, 22f, TextAlignmentOptions.Center);
            UIKit.Place(useLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(360f, 30f));
            useRoot.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (needs == null) return;

            Show(hunger, needs.Hunger, false);
            Show(thirst, needs.Thirst, false);
            Show(nature, needs.NatureCall, needs.NatureCall >= needs.NatureThreshold);
            ShowUse();

            if (noticeTimer > 0f)
            {
                noticeTimer -= Time.unscaledDeltaTime;
                var color = notice.color;
                color.a = Mathf.Clamp01(noticeTimer / 0.6f);
                notice.color = color;
                if (noticeTimer <= 0f) notice.text = string.Empty;
            }
        }

        private static void Show(Row row, float value, bool warning)
        {
            var rect = row.fill.rectTransform;
            var max = rect.anchorMax;
            max.x = Mathf.Clamp01(value);
            rect.anchorMax = max;

            if (warning != row.warning)
            {
                row.warning = warning;
                row.fill.color = warning ? Color.Lerp(row.color, Color.red, 0.5f) : row.color;
            }

            int percent = Mathf.RoundToInt(value * 100f);
            if (percent == row.shownPercent) return;
            row.shownPercent = percent;
            row.label.text = $"{row.title} {percent}%";
        }

        private void ShowUse()
        {
            bool active = consumer != null && consumer.IsUsing;
            string text = consumer == null ? string.Empty : active ? consumer.Label : consumer.Message;
            bool visible = active || !string.IsNullOrEmpty(text);
            if (useRoot.gameObject.activeSelf != visible) useRoot.gameObject.SetActive(visible);
            if (text != shownUse)
            {
                shownUse = text;
                useLabel.text = text;
            }

            var max = useFill.rectTransform.anchorMax;
            max.x = active ? consumer.Progress : 0f;
            useFill.rectTransform.anchorMax = max;
            useFrame.gameObject.SetActive(active);
        }

        private void OnNotice(string text)
        {
            notice.text = text;
            var color = notice.color;
            color.a = 1f;
            notice.color = color;
            noticeTimer = noticeSeconds;
        }
    }
}
