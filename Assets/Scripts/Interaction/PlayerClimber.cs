using System.Collections;
using ProjectMayham.Level;
using ProjectMayham.Player;
using ProjectMayham.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.Interaction
{
    /// <summary>
    /// Gets the player past things that cannot be walked through: over the nearest <see cref="Barricade"/> on Space
    /// and through a <see cref="WallHole"/> when the hole asks for it (E held on it). Either takes a moment during
    /// which the player cannot steer and needs free floor on the other side; climbing also costs stamina.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(Rigidbody2D))]
    public class PlayerClimber : MonoBehaviour
    {
        // How fast the player steps in front of a hole before going in, in units per second.
        private const float AlignSpeed = 3f;
        // The opening of a hole is hardly wider than the player: farther off its axis than this and there is no way in.
        private const float AlignTolerance = 0.2f;

        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("How far the barricade may be from the edge of the player, in world units.")]
        [SerializeField] private float reach = 0.5f;
        [Tooltip("Layers that make the landing spot unusable: walls, windows, furniture.")]
        [SerializeField] private LayerMask landingBlockMask;
        [Tooltip("Free floor left between the barricade and the player after the climb.")]
        [SerializeField] private float landingGap = 0.1f;
        [Tooltip("Seconds the reason for a refused climb stays on screen.")]
        [SerializeField] private float messageSeconds = 1.5f;

        private readonly Collider2D[] overlaps = new Collider2D[8];
        private PlayerController controller;
        private PlayerStats stats;
        private Rigidbody2D body;
        private Collider2D ownCollider;
        private InputAction climbAction;
        // Colliders of the thing being crossed; the player passes through them until the crossing is over.
        private Collider2D[] passedThrough;
        private float radius;
        private float messageTimer;

        /// <summary>True while the player is on the way over a barricade or through a hole.</summary>
        public bool IsClimbing { get; private set; }
        /// <summary>Why the last climb did not start; empty when there is nothing to tell.</summary>
        public string Message { get; private set; } = string.Empty;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            stats = GetComponent<PlayerStats>();
            body = GetComponent<Rigidbody2D>();
            ownCollider = GetComponent<Collider2D>();
            climbAction = inputActions.FindAction("Player/Climb", true);

            var circle = ownCollider as CircleCollider2D;
            radius = circle != null ? circle.radius * Mathf.Abs(transform.lossyScale.x) : ownCollider.bounds.extents.x;
        }

        private void OnEnable() => climbAction.Enable();

        private void OnDisable()
        {
            climbAction.Disable();
            if (IsClimbing) Finish();
        }

        private void Update()
        {
            if (messageTimer > 0f)
            {
                messageTimer -= Time.deltaTime;
                if (messageTimer <= 0f) Message = string.Empty;
            }

            if (!IsClimbing && climbAction.WasPressedThisFrame()) TryClimb();
        }

        // ---- barricades ----

        /// <summary>Starts climbing over the nearest barricade in reach. False when there is none or it cannot be climbed now.</summary>
        public bool TryClimb()
        {
            if (IsClimbing) return false;

            var barricade = FindBarricade();
            if (barricade == null) return false;

            // The barricade is crossed along its own axis, away from the side the player stands on.
            Vector2 position = body.position;
            Vector2 across = barricade.Across;
            float offset = Vector2.Dot(barricade.Position - position, across);
            Vector2 direction = offset >= 0f ? across : -across;
            float distance = Mathf.Abs(offset) + barricade.HalfThickness + radius + landingGap;

            if (!LandingIsFree(position + direction * distance, barricade.transform)) return Refuse("Некуда перелезть");
            if (stats != null && !stats.TrySpendStamina(barricade.ClimbStamina)) return Refuse("Не хватает выносливости");

            StartCoroutine(Climb(barricade, direction, distance));
            return true;
        }

        private Barricade FindBarricade()
        {
            Vector2 position = body.position;
            Barricade best = null;
            float bestDistance = radius + reach;
            foreach (var barricade in Barricade.All)
            {
                if (!barricade.CanClimb) continue;
                float distance = Vector2.Distance(position, barricade.ClosestPoint(position));
                if (distance > bestDistance) continue;
                best = barricade;
                bestDistance = distance;
            }
            return best;
        }

        private IEnumerator Climb(Barricade barricade, Vector2 direction, float distance)
        {
            Begin(barricade.GetComponentsInChildren<Collider2D>());

            // Driven by velocity, not by a target position: a wrap of the world in the middle of the climb changes nothing.
            float duration = barricade.ClimbSeconds;
            float speed = distance / duration;
            var wait = new WaitForFixedUpdate();
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.fixedDeltaTime)
            {
                body.linearVelocity = direction * speed;
                yield return wait;
            }

            Finish();
        }

        // ---- holes ----

        /// <summary>True when the player could go through the hole from where they stand: not busy and room on the far side.</summary>
        public bool CanCrawl(WallHole hole)
        {
            if (IsClimbing || hole == null) return false;
            CrawlPath(hole, out Vector2 entry, out Vector2 direction, out float distance);
            return LandingIsFree(entry + direction * distance, hole.transform);
        }

        /// <summary>Sends the player through the hole to the other side of the wall. False when that is not possible now.</summary>
        public bool TryCrawl(WallHole hole)
        {
            if (IsClimbing || hole == null) return false;

            CrawlPath(hole, out Vector2 entry, out Vector2 direction, out float distance);
            if (!LandingIsFree(entry + direction * distance, hole.transform)) return Refuse("С той стороны завалено");

            StartCoroutine(Crawl(hole, entry, direction, distance));
            return true;
        }

        /// <summary>
        /// The way through a hole: the spot right in front of it on the player's side, the direction through the
        /// wall and how far it is from that spot to the one behind the wall.
        /// </summary>
        private void CrawlPath(WallHole hole, out Vector2 entry, out Vector2 direction, out float distance)
        {
            Vector2 position = body.position;
            Vector2 toHole = Offset(position, hole.Position);
            Vector2 across = hole.Across;
            direction = Vector2.Dot(toHole, across) >= 0f ? across : -across;

            float clear = hole.HalfThickness + radius + landingGap;
            entry = position + toHole - direction * clear;
            distance = clear * 2f;
        }

        private IEnumerator Crawl(WallHole hole, Vector2 entry, Vector2 direction, float distance)
        {
            Begin(hole.GetComponentsInChildren<Collider2D>());
            var wait = new WaitForFixedUpdate();
            float step = Time.fixedDeltaTime;
            Vector2 along = new Vector2(-direction.y, direction.x);

            // First in front of the hole. As everywhere here the body is driven by velocity, in whole physics steps.
            Vector2 toEntry = entry - body.position;
            int steps = Mathf.Max(1, Mathf.CeilToInt(toEntry.magnitude / (AlignSpeed * step)));
            for (int i = 0; i < steps; i++)
            {
                body.linearVelocity = toEntry / (steps * step);
                yield return wait;
            }

            // Something stood in the way.
            if (hole == null || Mathf.Abs(Vector2.Dot(Offset(body.position, hole.Position), along)) > AlignTolerance)
            {
                Finish();
                Refuse("Не пролезть");
                yield break;
            }

            // Then straight through, kept on the axis of the hole.
            steps = Mathf.Max(1, Mathf.RoundToInt(hole.CrawlSeconds / step));
            float speed = distance / (steps * step);
            for (int i = 0; i < steps && hole != null; i++)
            {
                float drift = Vector2.Dot(Offset(body.position, hole.Position), along);
                body.linearVelocity = direction * speed + along * (drift * 0.5f / step);
                yield return wait;
            }

            Finish();
        }

        // ---- shared ----

        /// <summary>Shortest vector between two points of the level, across its seam when that is nearer.</summary>
        private static Vector2 Offset(Vector2 from, Vector2 to)
        {
            var wrap = WrapWorld.Active;
            return wrap != null ? wrap.Delta(from, to) : to - from;
        }

        /// <summary>True when the player fits at the spot. Colliders of the thing being crossed do not count.</summary>
        private bool LandingIsFree(Vector2 landing, Transform crossed)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(landingBlockMask);
            int count = Physics2D.OverlapCircle(landing, radius * 0.9f, filter, overlaps);
            for (int i = 0; i < count; i++)
            {
                var other = overlaps[i];
                if (other == ownCollider || other.transform.IsChildOf(crossed)) continue;
                return false;
            }
            return true;
        }

        private bool Refuse(string reason)
        {
            Message = reason;
            messageTimer = messageSeconds;
            return false;
        }

        private void Begin(Collider2D[] crossed)
        {
            IsClimbing = true;
            controller.MovementLocked = true;
            passedThrough = crossed;
            foreach (var other in passedThrough) Physics2D.IgnoreCollision(ownCollider, other, true);
        }

        private void Finish()
        {
            body.linearVelocity = Vector2.zero;
            if (passedThrough != null)
            {
                foreach (var other in passedThrough)
                    if (other != null) Physics2D.IgnoreCollision(ownCollider, other, false);
                passedThrough = null;
            }
            controller.MovementLocked = false;
            IsClimbing = false;
        }
    }
}
