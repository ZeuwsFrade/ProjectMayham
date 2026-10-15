using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Player;
using ProjectMayham.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    /// <summary>
    /// The window of a container that is being searched: the backpack on the left, the container on the right.
    /// Unlike the other windows it does not stop the game: while it is open the container is first looked over
    /// (its grid is covered by "Осматриваю…" and nothing of what is inside shows), then the places of the things
    /// show as "?" and they are found one by one. Whatever walks the level keeps walking. The searcher stands still and looks at the
    /// container; Esc, E, a step aside or a hit closes the window, and what was found stays found.
    /// </summary>
    public class SearchWindow : TransferWindow
    {
        // Farther from the container than this the search is over (the searcher was pushed away).
        private const float MaxDistance = 2.5f;

        private const string LookingHint = "Осматриваю… E или шаг в сторону — закрыть.";
        private const string SearchingHint = "Идёт обыск: вещи находятся по одной. Правый клик — забрать. E или шаг в сторону — закрыть.";
        private const string SearchedHint = "Обыскано. Правый клик — забрать или положить. E или шаг в сторону — закрыть.";
        // As wide as the largest container, so the window is the same for every one of them.
        private const float StorageWidth = 224f;

        private LootContainer container;
        private GameObject actor;
        private PlayerController controller;
        private PlayerStats stats;
        // The strip on the thing that is found next; it fills up while the search goes on.
        private RectTransform bar;
        private int openedFrame;
        private bool moveReleased;
        // Over the grid of the container during the first look.
        private RectTransform cover;
        private Image coverImage;
        private TMP_Text coverLabel;
        private RectTransform coverTrack;
        private RectTransform coverBar;

        protected override bool PausesGame => false;
        // The level stays in sight: danger has to be seen coming.
        protected override Color DimColor => new Color(0f, 0f, 0f, 0.2f);
        // To the right of the searcher, who stands in the middle of the screen.
        protected override Vector2 PanelOffset => new Vector2(470f, 0f);
        protected override string Hint =>
            container == null ? SearchedHint : container.IsLookingIn ? LookingHint : container.HiddenCount > 0 ? SearchingHint : SearchedHint;
        protected override bool StorageBusy => container != null && container.IsLookingIn;
        // During the first look nothing of what is inside shows, not even where it lies.
        protected override bool ShowSealed => container == null || !container.IsLookingIn;
        protected override float HintSize => 20f;
        protected override float MinStorageWidth => StorageWidth;

        /// <summary>Opens the container for whoever is searching it.</summary>
        public static void Open(GameObject actor, LootContainer container)
        {
            if (actor == null || container == null || !actor.TryGetComponent<GridInventory>(out var backpack)) return;

            var window = Get<SearchWindow>();
            if (window.IsOpen) return;
            window.actor = actor;
            window.container = container;
            window.Prepare(backpack, container.Inventory, container.Title);
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            container.Open();
            UpdateCover();
            openedFrame = Time.frameCount;
            moveReleased = false;

            controller = actor.GetComponent<PlayerController>();
            // A locked character keeps the speed it had: without this someone who pressed the key on the run
            // would slide away from the container.
            if (actor.TryGetComponent<Rigidbody2D>(out var body)) body.linearVelocity = Vector2.zero;
            if (controller != null)
            {
                controller.MovementLocked = true;
                // The nearest copy of the container: the level wraps around.
                Vector2 origin = actor.transform.position;
                var wrap = WrapWorld.Active;
                controller.LookAt = origin + (wrap != null ? wrap.Delta(origin, container.Position) : container.Position - origin);
            }

            stats = actor.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.Damaged += OnDamaged;
                stats.Died += Close;
            }
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            if (controller != null)
            {
                controller.MovementLocked = false;
                controller.LookAt = null;
            }
            if (stats != null)
            {
                stats.Damaged -= OnDamaged;
                stats.Died -= Close;
            }
            controller = null;
            stats = null;
            bar = null;
        }

        private void OnDamaged(float amount) => Close();

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;

            if (container == null || actor == null || ShouldClose())
            {
                Close();
                return;
            }

            bool wasLooking = container.IsLookingIn;
            container.Search(Time.deltaTime);
            // The look is over: the places of the things to find show now.
            if (wasLooking && !container.IsLookingIn) Redraw();
            if (bar != null) bar.anchorMax = new Vector2(container.SearchProgress, 1f);
            UpdateCover();
        }

        private void UpdateCover()
        {
            var grid = StorageGrid;
            if (grid == null) return;
            if (cover == null) BuildCover(grid);

            bool looking = container.IsLookingIn;
            cover.gameObject.SetActive(looking);
            if (!looking) return;
            cover.SetAsLastSibling();
            coverBar.anchorMax = new Vector2(container.LookProgress, 1f);
        }

        // Made in the grid of the container; a window built anew for another container gets a new one.
        private void BuildCover(RectTransform grid)
        {
            coverImage = UIKit.Panel(grid, "Cover", new Color(0.05f, 0.06f, 0.08f, 1f));
            coverImage.raycastTarget = false;
            cover = coverImage.rectTransform;
            UIKit.Stretch(cover);

            coverLabel = UIKit.Label(cover, "Осматриваю…", 18f, TextAlignmentOptions.Center, UIKit.Muted);
            UIKit.Stretch(coverLabel.rectTransform, 4f);
            coverLabel.textWrappingMode = TextWrappingModes.NoWrap;
            coverLabel.overflowMode = TextOverflowModes.Overflow;

            var track = UIKit.Panel(cover, "Track", new Color(0f, 0f, 0f, 0.6f));
            track.raycastTarget = false;
            coverTrack = track.rectTransform;
            coverTrack.anchorMin = new Vector2(0f, 0f);
            coverTrack.anchorMax = new Vector2(1f, 0f);
            coverTrack.pivot = new Vector2(0.5f, 0f);
            coverTrack.anchoredPosition = new Vector2(0f, 8f);
            coverTrack.sizeDelta = new Vector2(-16f, 6f);

            var fill = UIKit.Panel(coverTrack, "Fill", UIKit.Accent);
            fill.raycastTarget = false;
            coverBar = fill.rectTransform;
            coverBar.anchorMin = Vector2.zero;
            coverBar.anchorMax = new Vector2(0f, 1f);
            coverBar.offsetMin = coverBar.offsetMax = Vector2.zero;
        }

        private bool ShouldClose()
        {
            // The press that opened the window is not a press to close it.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame && Time.frameCount != openedFrame) return true;

            // A step ends the search. A key that was held when the window opened has to be let go first.
            Vector2 move = controller != null ? controller.MoveInput : Vector2.zero;
            if (move.sqrMagnitude < 0.01f) moveReleased = true;
            else if (moveReleased) return true;

            Vector2 origin = actor.transform.position;
            var wrap = WrapWorld.Active;
            Vector2 delta = wrap != null ? wrap.Delta(origin, container.Position) : container.Position - origin;
            return delta.sqrMagnitude > MaxDistance * MaxDistance;
        }

        protected override void OnWidgetBuilt(GridInventory inventory, GridInventory.Stack stack, RectTransform widget)
        {
            if (container == null || inventory != container.Inventory || !ReferenceEquals(stack, container.Next)) return;

            var track = UIKit.Panel(widget, "Search", new Color(0f, 0f, 0f, 0.6f));
            track.raycastTarget = false;
            var rect = track.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 5f);
            rect.sizeDelta = new Vector2(-12f, 6f);

            var fill = UIKit.Panel(rect, "Fill", UIKit.Accent);
            fill.raycastTarget = false;
            bar = fill.rectTransform;
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(container.SearchProgress, 1f);
            bar.offsetMin = bar.offsetMax = Vector2.zero;
        }
    }
}
