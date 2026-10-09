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
    /// answers to pick from; the game is paused meanwhile. Only the window is made; there are no dialogue
    /// assets or scripting yet, nodes are built in code by whoever starts a conversation.
    /// </summary>
    public class DialogueWindow : ModalWindow
    {
        private Image portraitImage;
        private TMP_Text nameLabel;
        private TMP_Text textLabel;
        private RectTransform choices;
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
            UIKit.Place(panel, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1500f, 360f));
            panel.pivot = new Vector2(0.5f, 0f);

            nameLabel = UIKit.Label(panel, string.Empty, 36f, TextAlignmentOptions.Left, UIKit.Accent);
            UIKit.Place(nameLabel.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -16f), new Vector2(800f, 50f));
            nameLabel.rectTransform.pivot = new Vector2(0f, 1f);

            textLabel = UIKit.Label(panel, string.Empty, 30f);
            UIKit.Place(textLabel.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -70f), new Vector2(1440f, 120f));
            textLabel.rectTransform.pivot = new Vector2(0f, 1f);

            choices = UIKit.Rect("Choices", panel);
            UIKit.Place(choices, new Vector2(0f, 0f), new Vector2(30f, 20f), new Vector2(1440f, 150f));
            choices.pivot = new Vector2(0f, 0f);
            var layout = choices.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.LowerLeft;
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
                var button = UIKit.MakeButton(choices, "▸ " + Personalize(choice.Text), () => Pick(picked), 26f);
                var text = button.GetComponentInChildren<TMP_Text>();
                text.alignment = TextAlignmentOptions.Left;
                UIKit.Stretch(text.rectTransform, 0f);
                text.rectTransform.offsetMin = new Vector2(16f, 0f);
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            }
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
