using System.Collections.Generic;
using ProjectMayham.Interaction;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// A hole broken through a wall: a shortcut that costs time. It cannot be walked through; holding E for a while
    /// makes the player crawl to the other side. It does not stop sight, so the other side can be looked at first.
    /// The prefab lies along its local X axis like the wall it is in, so it is crossed along its local Y axis.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class WallHole : MonoBehaviour, IHoldInteractable, IPromptHints
    {
        private static readonly List<WallHole> all = new List<WallHole>();

        [Tooltip("Seconds E has to be held before the player goes in.")]
        [SerializeField, Min(0.1f)] private float holdSeconds = 2f;
        [Tooltip("Seconds the way through takes; the player cannot steer meanwhile.")]
        [SerializeField, Min(0.1f)] private float crawlSeconds = 0.6f;

        [Header("Picture")]
        [SerializeField] private SpriteRenderer picture;
        [Tooltip("Shown instead when the hole is in a wall that runs up and down: the light lies differently on such a wall. " +
                 "Drawn lying like the other one, the quarter turn of the hole stands it up.")]
        [SerializeField] private Sprite turnedPicture;

        private BoxCollider2D box;
        // The far side was not free the last time someone asked: said in the prompt.
        private bool blocked;

        /// <summary>Every enabled hole.</summary>
        public static IReadOnlyList<WallHole> All => all;

        public float CrawlSeconds => crawlSeconds;

        public Vector2 Position => transform.position;
        /// <summary>The direction in which the hole is crossed (and its opposite).</summary>
        public Vector2 Across => transform.up;
        public float HalfThickness => box.size.y * 0.5f * Mathf.Abs(transform.lossyScale.y);

        public string Prompt => "Пролезть через дыру (удерживать)";
        public float HoldSeconds => holdSeconds;
        public string HoldLabel => "Пролезаю через дыру";
        public string Hints => blocked ? "С той стороны завалено" : string.Empty;

        private void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            if (picture != null && turnedPicture != null && Mathf.Abs(Across.x) > 0.5f) picture.sprite = turnedPicture;
        }

        private void OnEnable()
        {
            all.Add(this);
            Interactables.Register(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
            Interactables.Unregister(this);
        }

        public bool CanInteract(GameObject actor)
        {
            blocked = false;
            if (!actor.TryGetComponent<PlayerClimber>(out var climber) || climber.IsClimbing) return false;

            blocked = !climber.CanCrawl(this);
            return !blocked;
        }

        public void Interact(GameObject actor)
        {
            if (actor.TryGetComponent<PlayerClimber>(out var climber)) climber.TryCrawl(this);
        }
    }
}
