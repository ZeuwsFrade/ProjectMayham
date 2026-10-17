using System;
using System.Collections;
using ProjectMayham.Interaction;
using ProjectMayham.Player;
using UnityEngine;

namespace ProjectMayham.Combat
{
    /// <summary>
    /// What happens when the health of the player reaches zero: control is taken away, a placeholder animation plays
    /// (the character topples over and darkens; real art replaces it) and then <see cref="OnDied"/> tells the game
    /// loop (<see cref="Level.RaidController"/>) that it is over. <see cref="PlayerStats.Died"/> fires at the
    /// moment of death, this one at the end of the animation.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerDeath : MonoBehaviour
    {
        [Header("Placeholder animation")]
        [SerializeField, Min(0.1f)] private float fallSeconds = 0.9f;
        [Tooltip("Seconds the player lies still before the game loop is told.")]
        [SerializeField, Min(0f)] private float lieSeconds = 0.8f;
        [Tooltip("Degrees the character topples over.")]
        [SerializeField] private float fallAngle = 90f;
        [SerializeField] private Color deadTint = new Color(0.45f, 0.1f, 0.1f, 1f);
        [Tooltip("Share of the size the character shrinks to.")]
        [SerializeField, Range(0.3f, 1f)] private float shrinkTo = 0.85f;

        private PlayerStats stats;
        private Rigidbody2D body;
        private SpriteRenderer sprite;

        /// <summary>The animation is over: the game loop takes over. Raised once.</summary>
        public event Action OnDied;
        /// <summary>True from the moment of death.</summary>
        public bool IsDying { get; private set; }
        /// <summary>True once the animation has finished and <see cref="OnDied"/> has been raised.</summary>
        public bool IsDead { get; private set; }

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            body = GetComponent<Rigidbody2D>();
            sprite = GetComponent<SpriteRenderer>();
        }

        private void OnEnable() => stats.Died += Begin;
        private void OnDisable() => stats.Died -= Begin;

        private void Begin()
        {
            if (IsDying) return;
            IsDying = true;
            StartCoroutine(Die());
        }

        private IEnumerator Die()
        {
            TakeControlAway();

            float startRotation = body.rotation;
            Vector3 startScale = transform.localScale;
            Color startColor = sprite != null ? sprite.color : Color.white;
            // Falls to the side the last blow pushed it, or to the right when there was none.
            float direction = Mathf.Sign(body.linearVelocity.x + 0.0001f);
            var wait = new WaitForFixedUpdate();

            for (float elapsed = 0f; elapsed < fallSeconds; elapsed += Time.fixedDeltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / fallSeconds);
                body.linearVelocity = Vector2.Lerp(body.linearVelocity, Vector2.zero, 0.25f);
                body.MoveRotation(startRotation - direction * fallAngle * t);
                transform.localScale = startScale * Mathf.Lerp(1f, shrinkTo, t);
                if (sprite != null)
                {
                    Color tinted = Color.Lerp(startColor, deadTint, t);
                    tinted.a = sprite.color.a;
                    sprite.color = tinted;
                }
                yield return wait;
            }
            body.linearVelocity = Vector2.zero;

            yield return new WaitForSeconds(lieSeconds);
            IsDead = true;
            OnDied?.Invoke();
        }

        /// <summary>No more walking, fighting or picking up: the player is down.</summary>
        private void TakeControlAway()
        {
            if (TryGetComponent<MeleeCombat>(out var combat)) combat.enabled = false;
            if (TryGetComponent<PlayerInteractor>(out var interactor)) interactor.enabled = false;
            if (TryGetComponent<PlayerClimber>(out var climber)) climber.enabled = false;
            if (TryGetComponent<PlayerNoise>(out var noise)) noise.enabled = false;
            if (TryGetComponent<PlayerController>(out var controller))
            {
                controller.SpeedMultiplier = 1f;
                controller.TurnMultiplier = 1f;
                controller.SprintBlocked = false;
                // Off, so the body is not turned back towards the cursor every physics step.
                controller.enabled = false;
            }
        }
    }
}
