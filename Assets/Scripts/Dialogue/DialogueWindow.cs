using System;
using System.Collections.Generic;
using ProjectMayham.Core;
using ProjectMayham.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMayham.Dialogue
{
    /// <summary>One answer the player can pick.</summary>
    public class DialogueChoice
    {
        public string Text;
        /// <summary>The line to show next; null leaves the dialogue unless <see cref="Action"/> keeps it open.</summary>
        public DialogueNode Next;
        /// <summary>Called when the choice is picked (open the trade window, give a quest, ...).</summary>
        public Action Action;
        /// <summary>Close the window after the choice.</summary>
        public bool Closes;
    }

    /// <summary>A line of the speaker and the answers to it.</summary>
    public class DialogueNode
    {
        public string Text;
        public List<DialogueChoice> Choices = new List<DialogueChoice>();
    }

    /// <summary>
    /// Dialogue on its own canvas, as described in the design document: the speaker's sprite, their line and the
    /// answers to pick from (four are seen at once, more scroll with the wheel or the bar); the game is paused meanwhile. The nodes are built in code by whoever starts
    /// a conversation: <see cref="Npcs.NpcConversation"/> builds them from a dialogue asset.
    /// </summary>
    public class DialogueWindow : ModalWindow
    {
        private Image portraitImage;
        private TMP_Text nameLabel;
        private TMP_Text textLabel;
        private RectTransform choices;
        private ScrollRect choicesScroll;
        private Action onClosed;

        protected override int SortingOrder => 100;

        public static void Open(string speakerName, Sprite portrait, DialogueNode start, Action onClosed = null)
        {
            var window = Get<DialogueWindow>();
            if (window.IsOpen) return;
            window.onClosed = onClosed;
            window.Show();
            window.nameLabel.text = speakerName;
            window.portraitImage.sprite = portrait;
            window.portraitImage.enabled = portrait != null;
            window.ShowNode(start);
        }

        protected override void Build(Canvas canvas)
        {
            var dim = UIKit.Panel(canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.5f));
            UIKit.Stretch(dim.rectTransform);

            portraitImage = UIKit.Panel(dim.transform, "Portrait", Color.white);
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;
            UIKit.Place(portraitImage.rectTransform, new Vector2(0f, 0f), new Vector2(120f, 250f), new Vector2(520f, 760f));
            portraitImage.rectTransform.pivot = new Vector2(0f, 0f);

            var panel = UIKit.Panel(dim.transform, "Panel", UIKit.PanelColor).rectTransform;
            UIKit.Place(panel, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1500f, 412f));
            panel.pivot = new Vector2(0.5f, 0f);

            nameLabel = UIKit.Label(panel, string.Empty, 36f, TextAlignmentOptions.Left, UIKit.Accent);
            UIKit.Place(nameLabel.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -16f), new Vector2(800f, 50f));
            nameLabel.rectTransform.pivot = new Vector2(0f, 1f);

            textLabel = UIKit.Label(panel, string.Empty, 30f);
            UIKit.Place(textLabel.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -70f), new Vector2(1440f, 120f));
            textLabel.rectTransform.pivot = new Vector2(0f, 1f);

            // The answers: a list that scrolls when there are more than four, with a bar on the right.
            var area = UIKit.Rect("Choices", panel);
            UIKit.Place(area, new Vector2(0f, 0f), new Vector2(30f, 20f), new Vector2(1440f, 202f));
            area.pivot = new Vector2(0f, 0f);

            var viewport = UIKit.Rect("Viewport", area);
            UIKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            // Invisible, but catches the wheel between and beside the answers.
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            choices = UIKit.Rect("Content", viewport);
            choices.anchorMin = new Vector2(0f, 1f);
            choices.anchorMax = new Vector2(1f, 1f);
            choices.pivot = new Vector2(0.5f, 1f);
            choices.offsetMin = choices.offsetMax = Vector2.zero;
            var layout = choices.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
            choices.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            choicesScroll = area.gameObject.AddComponent<ScrollRect>();
            choicesScroll.content = choices;
            choicesScroll.viewport = viewport;
            choicesScroll.horizontal = false;
            choicesScroll.movementType = ScrollRect.MovementType.Clamped;
            choicesScroll.scrollSensitivity = 40f;
            choicesScroll.verticalScrollbar = ScrollBar(area);
            choicesScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            choicesScroll.verticalScrollbarSpacing = 8f;
        }

        // A thin bar on the right edge of the answers; shown only when they do not all fit.
        private static Scrollbar ScrollBar(RectTransform area)
        {
            var bar = UIKit.Rect("Scrollbar", area);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(16f, 0f);
            bar.anchoredPosition = Vector2.zero;
            bar.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

            var slide = UIKit.Rect("Sliding Area", bar);
            UIKit.Stretch(slide);
            var handle = UIKit.Rect("Handle", slide);
            UIKit.Stretch(handle);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = UIKit.ButtonColor;

            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }

        protected override void OnClosed()
        {
            var callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }

        private void ShowNode(DialogueNode node)
        {
            textLabel.text = Personalize(node.Text);
            UIKit.ClearChildren(choices);
            foreach (var choice in node.Choices)
            {
                var picked = choice;
                var button = UIKit.MakeButton(choices, "› " + Personalize(choice.Text), () => Pick(picked), 26f);
                var text = button.GetComponentInChildren<TMP_Text>();
                text.alignment = TextAlignmentOptions.Left;
                UIKit.Stretch(text.rectTransform, 0f);
                text.rectTransform.offsetMin = new Vector2(16f, 0f);
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            }
            // Every line starts with its first answers in view.
            Canvas.ForceUpdateCanvases();
            choicesScroll.verticalNormalizedPosition = 1f;
        }

        private void Pick(DialogueChoice choice)
        {
            choice.Action?.Invoke();
            if (!IsOpen) return; // the action may have closed or replaced the dialogue
            if (choice.Closes) Close();
            else if (choice.Next != null) ShowNode(choice.Next);
        }

        /// <summary>[ИГРОК] stands for the name the player chose.</summary>
        private static string Personalize(string text) =>
            string.IsNullOrEmpty(text) ? string.Empty : text.Replace("[ИГРОК]", GameSession.Data.playerName);
    }
}
