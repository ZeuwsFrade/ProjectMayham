using ProjectMayham.UI;
using TMPro;
using UnityEngine;

namespace ProjectMayham.Tutorial
{
    /// <summary>A note being read: a title, a few paragraphs and a button. Esc closes it too.</summary>
    public class NoteWindow : ModalWindow
    {
        private TMP_Text titleLabel;
        private TMP_Text textLabel;

        protected override int SortingOrder => 280;

        public static void Open(string title, string text)
        {
            var window = Get<NoteWindow>();
            if (window.IsOpen) return;
            window.Show();
            window.titleLabel.text = title;
            window.textLabel.text = text;
        }

        protected override void Build(Canvas canvas)
        {
            var dim = UIKit.Panel(canvas.transform, "Dim", UIKit.Dim);
            UIKit.Stretch(dim.rectTransform);

            var panel = UIKit.Panel(dim.transform, "Panel", UIKit.PanelColor).rectTransform;
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 560f));

            titleLabel = UIKit.Label(panel, string.Empty, 38f, TextAlignmentOptions.Center, UIKit.Accent);
            UIKit.Place(titleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(840f, 56f));

            textLabel = UIKit.Label(panel, string.Empty, 27f, TextAlignmentOptions.TopLeft);
            UIKit.Place(textLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(820f, 340f));
            textLabel.overflowMode = TextOverflowModes.Overflow;

            var ok = UIKit.MakeButton(panel, "Понятно", Close, 30f);
            UIKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(260f, 70f));
        }
    }
}
