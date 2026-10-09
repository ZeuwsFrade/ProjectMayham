using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    /// <summary>
    /// Small helpers to build uGUI windows from code (the shelter windows are created when the scene starts,
    /// so the scene itself stays light). Everything is laid out for a 1920x1080 reference resolution.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.65f);
        public static readonly Color PanelColor = new Color(0.09f, 0.1f, 0.13f, 0.97f);
        public static readonly Color RowColor = new Color(1f, 1f, 1f, 0.06f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.3f, 0.42f, 1f);
        public static readonly Color ButtonHover = new Color(0.3f, 0.42f, 0.58f, 1f);
        public static readonly Color ButtonDisabled = new Color(0.2f, 0.2f, 0.22f, 0.8f);
        public static readonly Color Accent = new Color(1f, 0.82f, 0.35f, 1f);
        public static readonly Color Muted = new Color(1f, 1f, 1f, 0.6f);
        public static readonly Color Bad = new Color(1f, 0.45f, 0.4f, 1f);

        public static Canvas NewCanvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null) go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            return canvas;
        }

        /// <summary>Buttons need an event system; scenes made without one get it here.</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Places a rect by anchor, pivot at the same point: <paramref name="position"/> is relative to the anchor.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size = 26f,
            TextAlignmentOptions align = TextAlignmentOptions.Left, Color? color = null)
        {
            var label = Rect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color ?? Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        public static Button MakeButton(Transform parent, string text, UnityAction onClick, float fontSize = 24f)
        {
            // The image stays white; the button tints it.
            var image = Panel(parent, "Button", Color.white);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHover;
            colors.selectedColor = ButtonColor;
            colors.pressedColor = ButtonColor * 0.75f;
            colors.disabledColor = ButtonDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);

            var label = Label(image.transform, text, fontSize, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 4f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        public static void SetButtonText(Button button, string text) =>
            button.GetComponentInChildren<TMP_Text>().text = text;

        /// <summary>A vertically scrolling list. Add rows to the returned content; rows decide their own height.</summary>
        public static RectTransform ScrollList(Transform parent, string name, out ScrollRect scroll)
        {
            var viewport = Rect(name, parent);
            var back = viewport.gameObject.AddComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0.25f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return content;
        }

        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false); // layouts must not count it until it is really gone
                Object.Destroy(child);
            }
        }
    }
}
