using System;
using TMPro;
using UnityEngine;

namespace ProjectMayham.UI
{
    /// <summary>A small Yes/No window. Esc counts as "No".</summary>
    public class ConfirmDialog : ModalWindow
    {
        private TMP_Text titleLabel;
        private TMP_Text messageLabel;
        private TMP_Text yesLabel;
        private TMP_Text noLabel;
        private Action onYes;
        private Action onNo;
        private GameObject noButton;
        private RectTransform yesRect;

        protected override int SortingOrder => 300;

        /// <summary>A message with a single "OK" button.</summary>
        public static void Notice(string title, string message, string okText = "Понятно", Action onOk = null)
        {
            var dialog = Get<ConfirmDialog>();
            if (dialog.IsOpen) return;
            dialog.Present(title, message, onOk, null, okText, null);
        }

        public static void Ask(string title, string message, Action yes, Action no = null,
            string yesText = "Да", string noText = "Нет")
        {
            var dialog = Get<ConfirmDialog>();
            if (dialog.IsOpen) return;
            dialog.Present(title, message, yes, no, yesText, noText);
        }

        protected override void Build(Canvas canvas)
        {
            var dim = UIKit.Panel(canvas.transform, "Dim", UIKit.Dim);
            UIKit.Stretch(dim.rectTransform);

            var panel = UIKit.Panel(dim.transform, "Panel", UIKit.PanelColor).rectTransform;
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 340f));

            titleLabel = UIKit.Label(panel, string.Empty, 40f, TextAlignmentOptions.Center, UIKit.Accent);
            UIKit.Place(titleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(700f, 60f));
            titleLabel.rectTransform.pivot = new Vector2(0.5f, 1f);

            messageLabel = UIKit.Label(panel, string.Empty, 28f, TextAlignmentOptions.Center);
            UIKit.Place(messageLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(680f, 130f));

            var yes = UIKit.MakeButton(panel, string.Empty, () => Answer(true), 30f);
            UIKit.Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(-150f, 40f), new Vector2(240f, 70f));
            yes.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            yesLabel = yes.GetComponentInChildren<TMP_Text>();

            var no = UIKit.MakeButton(panel, string.Empty, () => Answer(false), 30f);
            UIKit.Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(240f, 70f));
            no.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            noLabel = no.GetComponentInChildren<TMP_Text>();
            noButton = no.gameObject;
            yesRect = (RectTransform)yes.transform;
        }

        private void Present(string title, string message, Action yes, Action no, string yesText, string noText)
        {
            Show(); // builds the UI on first use
            titleLabel.text = title;
            messageLabel.text = message;
            yesLabel.text = yesText;
            noLabel.text = noText;
            // With no "No" text the window is a plain notice: one button in the middle.
            bool hasNo = noText != null;
            noButton.SetActive(hasNo);
            yesRect.anchoredPosition = new Vector2(hasNo ? -150f : 0f, yesRect.anchoredPosition.y);
            onYes = yes;
            onNo = no;
        }

        private void Answer(bool yes)
        {
            var callback = yes ? onYes : onNo;
            onYes = onNo = null;
            Close();
            callback?.Invoke();
        }

        // Esc is "No"; a notice has no "No", so there Esc means "OK".
        protected override void OnCancel() => Answer(!noButton.activeSelf);
    }
}
