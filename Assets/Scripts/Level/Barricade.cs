using System.Collections.Generic;
using ProjectMayham.Interaction;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// A pile of furniture across a corridor. There are three ways past it, each of which can be switched off:
    /// push it (walk into it, it is heavy and moves slowly), take it apart (hold E, takes a while and removes it for
    /// good) or climb over it (Space, quick but costs stamina and leaves it in place).
    /// The prefab lies along its local X axis, so it is crossed along its local Y axis.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class Barricade : MonoBehaviour, IHoldInteractable, IPromptHints
    {
        private static readonly List<Barricade> all = new List<Barricade>();

        [Header("Push")]
        [Tooltip("Off: the barricade stands still no matter what walks into it.")]
        [SerializeField] private bool canPush = true;

        [Header("Take apart (hold E)")]
        [SerializeField] private bool canDismantle = true;
        [SerializeField, Min(0.1f)] private float dismantleSeconds = 4f;

        [Header("Climb over (Space)")]
        [SerializeField] private bool canClimb = true;
        [SerializeField, Min(0.1f)] private float climbSeconds = 0.9f;
        [SerializeField, Min(0f)] private float climbStamina = 25f;

        private Rigidbody2D body;
        private BoxCollider2D box;

        /// <summary>Every enabled barricade.</summary>
        public static IReadOnlyList<Barricade> All => all;

        public bool CanPush => canPush;
        public bool CanDismantle => canDismantle;
        public bool CanClimb => canClimb;
        public float ClimbSeconds => climbSeconds;
        public float ClimbStamina => climbStamina;

        public Vector2 Position => transform.position;
        /// <summary>The direction in which the barricade is crossed (and its opposite).</summary>
        public Vector2 Across => transform.up;
        public float HalfThickness => box.size.y * 0.5f * Mathf.Abs(transform.lossyScale.y);

        public string Prompt => "Разобрать (удерживать)";
        public float HoldSeconds => dismantleSeconds;
        public string HoldLabel => "Разбираю баррикаду";

        public string Hints
        {
            get
            {
                if (canClimb && canPush) return "[Пробел] Перелезть · толкать: идти на неё";
                if (canClimb) return "[Пробел] Перелезть";
                return canPush ? "Толкать: идти на неё" : string.Empty;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.bodyType = canPush ? RigidbodyType2D.Dynamic : RigidbodyType2D.Static;
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

        /// <summary>The point of the barricade that is nearest to <paramref name="point"/>.</summary>
        public Vector2 ClosestPoint(Vector2 point) => box.ClosestPoint(point);

        public bool CanInteract(GameObject actor) => canDismantle;

        public void Interact(GameObject actor)
        {
            if (!canDismantle) return;
            Destroy(gameObject);
        }
    }
}
