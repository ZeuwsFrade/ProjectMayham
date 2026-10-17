using System.Collections.Generic;
using ProjectMayham.World;
using UnityEngine;

namespace ProjectMayham.AI
{
    /// <summary>
    /// Legs of an enemy: walks to a point along a path from <see cref="NavGrid"/> (straight when nothing is in the
    /// way), keeps off its neighbours so a crowd spreads out instead of piling up, turns to where it goes and takes
    /// shoves. It decides nothing: <see cref="EnemyAI"/> tells it where to go and how fast.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyMotor : MonoBehaviour
    {
        private static readonly List<EnemyMotor> all = new List<EnemyMotor>();

        [Tooltip("Degrees added to the heading. -90 for a sprite that looks up (+Y).")]
        [SerializeField] private float rotationOffset = -90f;
        [Tooltip("Radius of the body for the check whether the way ahead is clear.")]
        [SerializeField, Min(0.05f)] private float bodyRadius = 0.32f;
        [Tooltip("How hard it brakes when it has nowhere to go (also how fast a shove dies down), units/s^2.")]
        [SerializeField, Min(1f)] private float braking = 16f;
        [Tooltip("Seconds between two looks whether a path is still the way to go.")]
        [SerializeField, Min(0.1f)] private float repathSeconds = 0.6f;

        private readonly List<Vector2> path = new List<Vector2>();
        private Rigidbody2D body;
        private LayerMask obstacleMask;

        private bool hasGoal;
        private Vector2 goal;
        private float speed;
        private float stopDistance;
        private int pathIndex;
        private bool pathValid;
        private bool hasPathGoal;
        private Vector2 pathGoal;
        private float repathTimer;
        private float stuckTimer;
        private float clearTimer;
        private bool lineClear;
        private Vector2 lastPosition;

        private float acceleration = 10f;
        private float turnSpeed = 300f;
        private float separationRadius = 0.9f;
        private bool hasLook;
        private Vector2 lookAt;

        public Vector2 Position => body.position;
        public Vector2 Velocity => body.linearVelocity;
        /// <summary>True when it has a goal and has not got there yet.</summary>
        public bool IsMoving => hasGoal;
        /// <summary>The goal was reached (or there is none).</summary>
        public bool Arrived => !hasGoal;
        /// <summary>The goal cannot be reached: no path was found.</summary>
        public bool Unreachable { get; private set; }
        public IReadOnlyList<Vector2> Path => path;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            lastPosition = body.position;
            repathTimer = Random.value * repathSeconds;
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        public void Configure(float accelerationValue, float turnSpeedValue, float separation, LayerMask obstacles)
        {
            acceleration = accelerationValue;
            turnSpeed = turnSpeedValue;
            separationRadius = separation;
            obstacleMask = obstacles;
        }

        // ---- orders ----

        /// <summary>Go to a point. Call it again every time the point moves; the path is only searched anew when needed.</summary>
        public void MoveTo(Vector2 point, float moveSpeed, float stopWithin = 0.15f)
        {
            hasGoal = true;
            speed = moveSpeed;
            stopDistance = stopWithin;
            goal = point;
        }

        /// <summary>Stand still (the body brakes). Keeps looking where it looks.</summary>
        public void Stop()
        {
            hasGoal = false;
            Unreachable = false;
            pathValid = false;
            hasPathGoal = false;
        }

        /// <summary>Turn towards a point and stay turned that way, whatever the legs do. See <see cref="LookForward"/>.</summary>
        public void LookAt(Vector2 point)
        {
            hasLook = true;
            lookAt = point;
        }

        /// <summary>Turn the way it walks again.</summary>
        public void LookForward() => hasLook = false;

        /// <summary>A shove: adds speed that dies down by itself.</summary>
        public void Shove(Vector2 velocity) => body.linearVelocity += velocity;

        /// <summary>Quick step in a direction without a path: the lunge of an attack.</summary>
        public void Dash(Vector2 direction, float distance, float seconds)
        {
            if (seconds <= 0f) return;
            body.linearVelocity += direction.normalized * (distance / seconds);
        }

        // ---- movement ----

        private void FixedUpdate()
        {
            Vector2 position = body.position;
            // The world moved the body to another copy of the level: the path was laid in the old one.
            if ((position - lastPosition).sqrMagnitude > 9f)
            {
                pathValid = false;
                hasPathGoal = false;
            }
            lastPosition = position;

            Vector2 desired = Vector2.zero;
            if (hasGoal) desired = Steer(position);

            float rate = desired.sqrMagnitude > 0.01f ? acceleration : braking;
            Vector2 velocity = Vector2.MoveTowards(body.linearVelocity, desired, rate * Time.fixedDeltaTime);
            body.linearVelocity = velocity;

            Turn(velocity);
        }

        private Vector2 Steer(Vector2 position)
        {
            Vector2 target = Near(goal, position);
            if ((target - position).magnitude <= stopDistance)
            {
                hasGoal = false;
                return Vector2.zero;
            }

            Vector2 heading = target - position;
            clearTimer -= Time.fixedDeltaTime;
            if (clearTimer <= 0f)
            {
                clearTimer = 0.12f;
                lineClear = IsClear(position, heading);
            }

            if (lineClear)
            {
                // Nothing in the way: no path is needed, and a new one is searched the moment something is.
                pathValid = false;
                hasPathGoal = false;
                stuckTimer = 0f;
            }
            else
            {
                heading = FollowPath(position, target);
                if (heading == Vector2.zero) return Vector2.zero;
            }

            Vector2 direction = heading.normalized * speed + Separation(position);
            WatchForStuck(position, direction);
            return Vector2.ClampMagnitude(direction, speed * 1.1f);
        }

        /// <summary>Direction to the next corner of the path (zero if there is none and no way is known).</summary>
        private Vector2 FollowPath(Vector2 position, Vector2 target)
        {
            repathTimer -= Time.fixedDeltaTime;
            bool goalMoved = !hasPathGoal || (target - pathGoal).sqrMagnitude > 1.5f * 1.5f;
            if (repathTimer <= 0f || goalMoved)
            {
                var grid = NavGrid.Active;
                if (grid == null || !grid.IsReady)
                {
                    // No map: walk at the target and hope.
                    pathValid = false;
                    return target - position;
                }
                if (grid.HasSearchBudget)
                {
                    repathTimer = repathSeconds * (0.8f + Random.value * 0.4f);
                    hasPathGoal = true;
                    pathGoal = target;
                    pathValid = grid.TryFindPath(position, target, path);
                    pathIndex = 0;
                    Unreachable = !pathValid;
                }
            }

            // No path (not found, or waiting for its turn at the grid): stand until there is one.
            if (!pathValid) return Vector2.zero;

            while (pathIndex < path.Count - 1 && (path[pathIndex] - position).sqrMagnitude < 0.3f * 0.3f) pathIndex++;
            if (pathIndex >= path.Count) return target - position;
            return path[pathIndex] - position;
        }

        private bool IsClear(Vector2 from, Vector2 toTarget)
        {
            float distance = toTarget.magnitude;
            if (distance < 0.01f) return true;
            return Physics2D.CircleCast(from, bodyRadius, toTarget / distance, distance, obstacleMask).collider == null;
        }

        private Vector2 Separation(Vector2 position)
        {
            if (separationRadius <= 0f) return Vector2.zero;

            Vector2 push = Vector2.zero;
            for (int i = 0; i < all.Count; i++)
            {
                var other = all[i];
                if (other == this) continue;
                Vector2 away = position - other.body.position;
                float distance = away.magnitude;
                if (distance >= separationRadius) continue;
                // Closer neighbours push harder; exactly on top of each other the direction is random.
                push += (distance > 0.001f ? away / distance : Random.insideUnitCircle.normalized) * (1f - distance / separationRadius);
            }
            return push * speed * 0.8f;
        }

        // A body that wants to move and does not: ask for a new path, and if that does not help, wiggle.
        private void WatchForStuck(Vector2 position, Vector2 wanted)
        {
            if (body.linearVelocity.magnitude > wanted.magnitude * 0.25f)
            {
                stuckTimer = 0f;
                return;
            }

            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer < 0.7f) return;
            stuckTimer = 0f;
            repathTimer = 0f;
            body.linearVelocity += Random.insideUnitCircle * speed;
        }

        private void Turn(Vector2 velocity)
        {
            Vector2 face;
            if (hasLook) face = Near(lookAt, body.position) - body.position;
            else if (velocity.sqrMagnitude > 0.09f) face = velocity;
            else return;

            float targetAngle = Mathf.Atan2(face.y, face.x) * Mathf.Rad2Deg + rotationOffset;
            body.MoveRotation(Mathf.MoveTowardsAngle(body.rotation, targetAngle, turnSpeed * Time.fixedDeltaTime));
        }

        /// <summary>The copy of a point that is nearest to another, across the seam of a wrapped level.</summary>
        public static Vector2 Near(Vector2 point, Vector2 reference)
        {
            var wrapWorld = WrapWorld.Active;
            return wrapWorld != null ? wrapWorld.NearestImage(point, reference) : point;
        }

        private void OnDrawGizmosSelected()
        {
            if (!pathValid) return;
            Gizmos.color = Color.yellow;
            Vector3 previous = transform.position;
            for (int i = pathIndex; i < path.Count; i++)
            {
                Gizmos.DrawLine(previous, path[i]);
                previous = path[i];
            }
        }
    }
}
