using System;
using System.Collections;
using System.Collections.Generic;
using ProjectMayham.Combat;
using ProjectMayham.Player;
using ProjectMayham.Vision;
using UnityEngine;

namespace ProjectMayham.AI
{
    public enum EnemyState
    {
        /// <summary>Knows nothing: wanders around its post.</summary>
        Patrol,
        /// <summary>Saw or heard something: goes to look, then searches.</summary>
        Suspicious,
        /// <summary>Hunts the player.</summary>
        Chase,
        /// <summary>Winds up and strikes.</summary>
        Attack,
        /// <summary>Lost its footing after a blow and does nothing for a moment.</summary>
        Stagger,
        Dead
    }

    /// <summary>
    /// The mind of an enemy: a state machine of patrol, suspicion, chase, attack and stagger, driven by what it
    /// sees (<see cref="EnemyDefinition.SightRange"/>) and hears (<see cref="Noise"/>). The numbers come from an
    /// <see cref="EnemyDefinition"/>; the legs are <see cref="EnemyMotor"/>. A kind of enemy that behaves
    /// differently derives from this and overrides the few hooks marked virtual (see <see cref="Dokhodyaga"/>).
    /// What the enemy feels is published through <see cref="AlertDirector"/> for the music.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(EnemyMotor))]
    public class EnemyAI : MonoBehaviour
    {
        private static readonly List<EnemyAI> all = new List<EnemyAI>();
        private static PlayerStats cachedPlayer;

        // Awareness at which a patrolling enemy starts to look into it.
        private const float SuspicionThreshold = 0.3f;
        private const float SenseInterval = 0.1f;
        // The last share of the wind-up, in which the aim of the blow is no longer corrected.
        private const float LockAimShare = 0.25f;

        [SerializeField] private EnemyDefinition definition;
        [Tooltip("Child that holds the sprite. It is what pulls back and lunges in an attack. The root when empty.")]
        [SerializeField] private Transform body;
        [Tooltip("Layers that block sight. Empty: the ones the player's own view is blocked by.")]
        [SerializeField] private LayerMask sightMask;
        [Tooltip("Layers that block movement and sound. Empty: Obstacle, Furniture and Window.")]
        [SerializeField] private LayerMask moveMask;

        private Health health;
        private EnemyMotor motor;
        private PlayerStats player;
        private LayerMask wallMask;

        private EnemyState state = EnemyState.Patrol;
        private float stateTime;
        private float senseTimer;

        // What it knows about the player.
        private float awareness;
        private bool canSee;
        private float distanceToPlayer = float.PositiveInfinity;
        private bool hasLastKnown;
        private Vector2 lastKnown;
        private float lostTimer;
        private bool hasHeard;

        // Patrol and search.
        private Vector2 home;
        private bool patrolMoving;
        private Vector2 patrolTarget;
        private float patrolWait;
        private Vector2 investigate;
        private bool searching;
        private float searchTime;
        private float searchBaseAngle;

        // Attack.
        private float attackCooldown;
        private float attackTime;
        private bool striking;
        private Vector2 strikeDirection;
        private float staggerLeft;

        // A shout from an ally that has not arrived yet.
        private float alertDelay = -1f;
        private Vector2 alertPoint;
        private float shoutCooldown;
        private Vector3 bodyScale = Vector3.one;
        private Vector3 bodyOffset;

        public static IReadOnlyList<EnemyAI> All => all;

        public EnemyState State => state;
        public EnemyDefinition Definition => definition;
        public bool CanSeePlayer => canSee;
        public float Awareness => awareness;
        public Vector2 Position => motor.Position;
        public bool IsAlive => state != EnemyState.Dead;
        /// <summary>Whether the enemy is after the player right now (chasing, attacking, or recovering from a blow).</summary>
        public bool IsHunting => state == EnemyState.Chase || state == EnemyState.Attack || state == EnemyState.Stagger;

        /// <summary>The state changed: (from, to).</summary>
        public event Action<EnemyState, EnemyState> StateChanged;

        protected EnemyMotor Motor => motor;
        /// <summary>The player, or null while none is in the scene.</summary>
        protected PlayerStats Target => player;
        protected bool HasLastKnown => hasLastKnown;
        protected Vector2 LastKnown => lastKnown;
        protected float DistanceToPlayer => distanceToPlayer;

        // ---- setup ----

        protected virtual void Awake()
        {
            health = GetComponent<Health>();
            motor = GetComponent<EnemyMotor>();
            if (body == null) body = transform;
            bodyScale = body.localScale;
            bodyOffset = body.localPosition;

            if (definition == null)
            {
                Debug.LogError($"{name}: an EnemyAI needs an EnemyDefinition.", this);
                enabled = false;
                return;
            }

            if (moveMask == 0) moveMask = LayerMask.GetMask("Obstacle", "Furniture", "Window");
            wallMask = LayerMask.GetMask("Obstacle", "Window");
            health.Configure(definition.MaxHealth, Team.Enemy);
            motor.Configure(definition.Acceleration, definition.TurnSpeed, definition.SeparationRadius, moveMask);
        }

        private void Start()
        {
            home = motor.Position;
            patrolWait = UnityEngine.Random.Range(0f, definition.PatrolPause.y);

            if (sightMask == 0)
            {
                var cone = VisionCone.Active;
                sightMask = cone != null && cone.Settings != null ? cone.Settings.obstacleMask : LayerMask.GetMask("Obstacle", "Furniture");
            }
            senseTimer = UnityEngine.Random.value * SenseInterval;
        }

        private void OnEnable()
        {
            all.Add(this);
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            Noise.Emitted += OnNoise;
        }

        private void OnDisable()
        {
            all.Remove(this);
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            Noise.Emitted -= OnNoise;
            AlertDirector.Remove(this);
        }

        // ---- the loop ----

        private void Update()
        {
            if (state == EnemyState.Dead) return;

            float dt = Time.deltaTime;
            stateTime += dt;
            attackCooldown -= dt;
            shoutCooldown -= dt;
            TickShout(dt);

            ResolvePlayer();
            Sense(dt);

            switch (state)
            {
                case EnemyState.Patrol: TickPatrol(dt); break;
                case EnemyState.Suspicious: TickSuspicious(dt); break;
                case EnemyState.Chase: TickChase(dt); break;
                case EnemyState.Attack: TickAttack(dt); break;
                case EnemyState.Stagger: TickStagger(dt); break;
            }
        }

        private void ResolvePlayer()
        {
            if (player == null)
            {
                if (cachedPlayer == null) cachedPlayer = FindFirstObjectByType<PlayerStats>();
                player = cachedPlayer;
            }
        }

        // ---- senses ----

        private void Sense(float dt)
        {
            if (player == null || player.IsDead)
            {
                canSee = false;
                distanceToPlayer = float.PositiveInfinity;
                if (player != null && player.IsDead) ForgetPlayer();
                return;
            }

            Vector2 me = motor.Position;
            Vector2 target = EnemyMotor.Near(player.transform.position, me);
            distanceToPlayer = Vector2.Distance(me, target);

            senseTimer -= dt;
            if (senseTimer <= 0f)
            {
                senseTimer = SenseInterval;
                canSee = LooksAt(me, target);
            }

            if (canSee)
            {
                // Nearer and it is quicker sure; far off it takes longer.
                float closeness = 1f - Mathf.Clamp01(distanceToPlayer / definition.SightRange);
                awareness = Mathf.Min(1f, awareness + (0.6f + 1.4f * closeness) / definition.NoticeSeconds * dt);
                lastKnown = target;
                hasLastKnown = true;
                lostTimer = 0f;
            }
            else if (state == EnemyState.Patrol || state == EnemyState.Suspicious)
            {
                awareness = Mathf.Max(0f, awareness - dt / definition.ForgetSeconds);
            }
        }

        private bool LooksAt(Vector2 me, Vector2 target)
        {
            Vector2 to = target - me;
            float distance = to.magnitude;
            if (distance > definition.SightRange) return false;

            bool near = distance <= definition.SenseRange;
            if (!near && Vector2.Angle(transform.up, to) > definition.SightAngle * 0.5f) return false;
            return !Physics2D.Linecast(me, target, sightMask);
        }

        private void OnNoise(NoiseEvent noise)
        {
            if (state == EnemyState.Dead || noise.Team != Team.Player || definition.Hearing <= 0f) return;

            Vector2 me = motor.Position;
            Vector2 at = EnemyMotor.Near(noise.Position, me);
            float distance = Vector2.Distance(me, at);
            float radius = noise.Radius * definition.Hearing;
            // A wall in between muffles it.
            if (distance <= radius && Physics2D.Linecast(me, at, wallMask)) radius *= 0.5f;
            if (distance > radius) return;

            float loudness = 1f - distance / radius;
            Hear(at, loudness);
        }

        private void Hear(Vector2 point, float loudness)
        {
            hasHeard = true;
            awareness = Mathf.Max(awareness, Mathf.Lerp(SuspicionThreshold + 0.05f, 0.7f, loudness));

            switch (state)
            {
                case EnemyState.Patrol:
                    EnterSuspicious(point);
                    break;
                case EnemyState.Suspicious:
                    investigate = point;
                    searching = false;
                    break;
                case EnemyState.Chase:
                    // It follows the sound while it has lost sight.
                    if (!canSee)
                    {
                        lastKnown = point;
                        hasLastKnown = true;
                        lostTimer = 0f;
                    }
                    break;
            }
        }

        private void ForgetPlayer()
        {
            awareness = 0f;
            hasLastKnown = false;
            if (IsHunting || state == EnemyState.Suspicious) SetState(EnemyState.Patrol);
        }

        // ---- patrol ----

        private void TickPatrol(float dt)
        {
            if (canSee && awareness >= 1f)
            {
                EnterChase(true);
                return;
            }
            if (awareness >= SuspicionThreshold && (canSee || hasHeard))
            {
                EnterSuspicious(hasLastKnown ? lastKnown : motor.Position);
                return;
            }

            if (patrolMoving)
            {
                motor.MoveTo(patrolTarget, definition.PatrolSpeed, 0.4f);
                bool arrived = Vector2.Distance(motor.Position, EnemyMotor.Near(patrolTarget, motor.Position)) < 0.6f;
                if (arrived || motor.Unreachable || stateTime > 20f)
                {
                    patrolMoving = false;
                    patrolWait = UnityEngine.Random.Range(definition.PatrolPause.x, definition.PatrolPause.y);
                    motor.Stop();
                }
                return;
            }

            patrolWait -= dt;
            if (patrolWait > 0f) return;

            Vector2 here = motor.Position;
            Vector2 anchor = EnemyMotor.Near(home, here);
            var grid = NavGrid.Active;
            if (grid != null && grid.IsReady)
            {
                if (!grid.TryRandomPoint(anchor, definition.PatrolRadius, out patrolTarget))
                {
                    patrolWait = 1f;
                    return;
                }
            }
            else patrolTarget = anchor + UnityEngine.Random.insideUnitCircle * Mathf.Min(3f, definition.PatrolRadius);
            patrolMoving = true;
            stateTime = 0f;
        }

        // ---- suspicion ----

        private void EnterSuspicious(Vector2 point)
        {
            investigate = point;
            searching = false;
            SetState(EnemyState.Suspicious);
        }

        private void TickSuspicious(float dt)
        {
            if (canSee)
            {
                investigate = lastKnown;
                searching = false;
                if (awareness >= 1f)
                {
                    EnterChase(true);
                    return;
                }
            }

            if (!searching)
            {
                motor.MoveTo(investigate, definition.InvestigateSpeed, 0.5f);
                bool arrived = Vector2.Distance(motor.Position, EnemyMotor.Near(investigate, motor.Position)) < 0.8f;
                if (arrived || motor.Unreachable)
                {
                    searching = true;
                    searchTime = 0f;
                    Vector2 look = EnemyMotor.Near(investigate, motor.Position) - motor.Position;
                    searchBaseAngle = look.sqrMagnitude > 0.01f ? Mathf.Atan2(look.y, look.x) * Mathf.Rad2Deg : transform.eulerAngles.z + 90f;
                    motor.Stop();
                }
                return;
            }

            // Looks around where it came to: left, right, left.
            searchTime += dt;
            float angle = (searchBaseAngle + Mathf.Sin(searchTime * 1.6f) * 85f) * Mathf.Deg2Rad;
            motor.LookAt(motor.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 3f);
            if (searchTime >= definition.SearchSeconds) SetState(EnemyState.Patrol);
        }

        // ---- chase ----

        private void EnterChase(bool noticed)
        {
            bool wasHunting = IsHunting;
            SetState(EnemyState.Chase);
            lostTimer = 0f;
            if (noticed && !wasHunting && hasLastKnown) OnPlayerNoticed(lastKnown);
        }

        private void TickChase(float dt)
        {
            if (!canSee) lostTimer += dt;

            if (canSee && distanceToPlayer <= definition.AttackRange && attackCooldown <= 0f)
            {
                SetState(EnemyState.Attack);
                return;
            }

            if (!hasLastKnown)
            {
                SetState(EnemyState.Patrol);
                return;
            }

            Vector2 destination = ChaseDestination(lastKnown, canSee);
            motor.MoveTo(destination, definition.ChaseSpeed, canSee ? 0.1f : 0.4f);

            if (!canSee)
            {
                bool atLastKnown = Vector2.Distance(motor.Position, EnemyMotor.Near(lastKnown, motor.Position)) < 0.9f;
                if (atLastKnown || motor.Unreachable || lostTimer > definition.LoseTargetSeconds)
                {
                    awareness = Mathf.Min(awareness, 0.6f);
                    EnterSuspicious(lastKnown);
                }
            }
            else
            {
                // Right up against the player and cannot strike yet: face them.
                motor.LookForward();
            }
        }

        /// <summary>Where the enemy walks to while it hunts. A kind that surrounds its prey overrides this.</summary>
        protected virtual Vector2 ChaseDestination(Vector2 playerPosition, bool visible) => playerPosition;

        /// <summary>
        /// The enemy has just made sure it is the player it sees (it was not hunting before). It shouts, which wakes
        /// the others of its kind; override to do more or to keep quiet.
        /// </summary>
        protected virtual void OnPlayerNoticed(Vector2 playerPosition)
        {
            if (!definition.Shouts || shoutCooldown > 0f) return;

            shoutCooldown = 6f;
            Vector2 here = motor.Position;
            CombatEvents.RaiseEnemyShouted(gameObject, here);
            foreach (var ally in all)
            {
                if (ally == this || !ally.IsAlive || ally.IsHunting) continue;
                float distance = Vector2.Distance(here, EnemyMotor.Near(ally.motor.Position, here));
                if (distance > definition.ShoutRadius) continue;
                // The cry takes time to carry.
                ally.Alert(playerPosition, 0.25f + distance / 16f);
            }
        }

        /// <summary>
        /// Somebody wakes this enemy: it comes to the point, hunting. It does not know the player is there, so it will
        /// search if the player is not.
        /// </summary>
        public void Alert(Vector2 point, float delay = 0f)
        {
            if (state == EnemyState.Dead) return;
            alertPoint = point;
            alertDelay = Mathf.Max(0f, delay);
        }

        private void TickShout(float dt)
        {
            if (alertDelay < 0f) return;

            alertDelay -= dt;
            if (alertDelay >= 0f) return;
            alertDelay = -1f;

            if (state != EnemyState.Patrol && state != EnemyState.Suspicious) return;
            lastKnown = alertPoint;
            hasLastKnown = true;
            awareness = Mathf.Max(awareness, 0.6f);
            EnterChase(false);
        }

        // ---- attack ----

        private void EnterAttack()
        {
            attackTime = 0f;
            striking = false;
            motor.Stop();

            Vector2 to = EnemyMotor.Near(player.transform.position, motor.Position) - motor.Position;
            strikeDirection = to.sqrMagnitude > 0.0001f ? to.normalized : (Vector2)transform.up;
            CombatEvents.RaiseSwingStarted(new SwingEvent { Attacker = gameObject, Position = motor.Position, Direction = strikeDirection });
        }

        private void TickAttack(float dt)
        {
            attackTime += dt;
            float windup = definition.WindupSeconds;

            if (attackTime < windup)
            {
                // Follows the player until the last moment, then commits: a quick player steps out of the blow.
                if (attackTime < windup * (1f - LockAimShare) && player != null && !player.IsDead)
                {
                    Vector2 to = EnemyMotor.Near(player.transform.position, motor.Position) - motor.Position;
                    if (to.sqrMagnitude > 0.0001f) strikeDirection = to.normalized;
                }
                motor.LookAt(motor.Position + strikeDirection * 3f);
                ShowWindup(attackTime / windup);
                return;
            }

            if (!striking)
            {
                striking = true;
                Strike();
                ShowWindup(0f);
                // The blow also carries the body forward.
                motor.Dash(strikeDirection, definition.Lunge, 0.15f);
            }

            if (attackTime >= windup + definition.RecoverSeconds)
            {
                attackCooldown = definition.CooldownSeconds;
                SetState(EnemyState.Chase);
            }
        }

        private void Strike()
        {
            Vector2 me = motor.Position;
            var damageable = player as IDamageable;
            var swing = new SwingEvent { Attacker = gameObject, Position = me, Direction = strikeDirection };
            if (damageable == null || !damageable.IsAlive)
            {
                CombatEvents.RaiseSwingMissed(swing);
                return;
            }

            Vector2 target = EnemyMotor.Near(player.transform.position, me);
            Vector2 to = target - me;
            bool inReach = to.magnitude <= definition.AttackRange + 0.3f
                           && Vector2.Angle(strikeDirection, to) <= definition.AttackArc * 0.5f
                           && !Physics2D.Linecast(me, target, wallMask);
            if (!inReach)
            {
                CombatEvents.RaiseSwingMissed(swing);
                return;
            }

            Vector2 direction = to.sqrMagnitude > 0.0001f ? to.normalized : strikeDirection;
            var info = new DamageInfo
            {
                Amount = definition.Damage,
                Point = target,
                Direction = direction,
                Knockback = definition.AttackKnockback,
                Attacker = gameObject
            };
            damageable.TakeDamage(info);
            CombatEvents.RaiseHitLanded(new HitEvent
            {
                Attacker = gameObject, Target = damageable, Point = target, Direction = direction,
                Damage = info.Amount, Killed = !damageable.IsAlive
            });
        }

        /// <summary>The body pulls back and swells as the blow gathers, so the player can read it.</summary>
        private void ShowWindup(float progress)
        {
            if (body == null) return;

            float eased = progress * progress;
            body.localScale = bodyScale * (1f + 0.22f * eased);
            body.localPosition = bodyOffset - Vector3.up * (0.16f * eased);
        }

        // ---- stagger ----

        private void EnterStagger(float seconds)
        {
            if (state == EnemyState.Stagger)
            {
                // A second blow in the middle of the first only extends it, never shortens.
                staggerLeft = Mathf.Max(staggerLeft, seconds);
                return;
            }

            staggerLeft = seconds;
            SetState(EnemyState.Stagger);
        }

        private void TickStagger(float dt)
        {
            staggerLeft -= dt;
            if (staggerLeft > 0f) return;

            // Whoever hit it is in its mind now.
            attackCooldown = Mathf.Max(attackCooldown, 0.2f);
            if (hasLastKnown) EnterChase(false);
            else SetState(EnemyState.Suspicious);
        }

        // ---- reaction to blows ----

        private void OnDamaged(DamageInfo info)
        {
            if (state == EnemyState.Dead) return;

            // Hit from the dark: it knows where from.
            if (info.Attacker != null)
            {
                lastKnown = EnemyMotor.Near(info.Attacker.transform.position, motor.Position);
                hasLastKnown = true;
            }
            awareness = 1f;
            lostTimer = 0f;

            motor.Shove(info.Direction * info.Knockback * (1f - definition.KnockbackResistance));

            float stagger = info.StaggerSeconds * (1f - definition.StaggerResistance);
            if (stagger > 0.05f) EnterStagger(stagger);
            else if (state == EnemyState.Patrol || state == EnemyState.Suspicious) EnterChase(true);
        }

        private void OnDied(DamageInfo info)
        {
            SetState(EnemyState.Dead);
            motor.Stop();
            motor.Shove(info.Direction * info.Knockback * (1f - definition.KnockbackResistance));
            ShowWindup(0f);
            StartCoroutine(Fall());
        }

        // Placeholder death: the body topples over, shrinks and goes. Art replaces it.
        private IEnumerator Fall()
        {
            foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;

            const float seconds = 0.7f;
            Vector3 start = transform.localScale;
            var rb = GetComponent<Rigidbody2D>();
            // The legs are done; what is left of the shove dies down by itself.
            motor.enabled = false;
            rb.linearDamping = 7f;
            float rotation = rb.rotation;
            float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            var wait = new WaitForFixedUpdate();
            for (float t = 0f; t < seconds; t += Time.fixedDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                rb.MoveRotation(rotation + side * 80f * k);
                transform.localScale = start * Mathf.Lerp(1f, 0.6f, k);
                yield return wait;
            }
            Destroy(gameObject);
        }

        // ---- state ----

        private void SetState(EnemyState next)
        {
            if (state == next) return;

            var previous = state;
            // What the old state changed in the body is put back.
            if (previous == EnemyState.Attack) ShowWindup(0f);
            if (previous == EnemyState.Suspicious || previous == EnemyState.Attack) motor.LookForward();
            if (previous == EnemyState.Patrol) patrolMoving = false;

            state = next;
            stateTime = 0f;
            hasHeard = false;

            switch (next)
            {
                case EnemyState.Attack:
                    EnterAttack();
                    break;
                case EnemyState.Stagger:
                case EnemyState.Dead:
                    motor.Stop();
                    break;
                case EnemyState.Patrol:
                    patrolWait = UnityEngine.Random.Range(definition.PatrolPause.x, definition.PatrolPause.y);
                    motor.Stop();
                    break;
            }

            AlertDirector.Report(this, AlertFor(next));
            OnStateEntered(previous);
            StateChanged?.Invoke(previous, next);
        }

        /// <summary>Called right after a state has been entered. For a kind of enemy that wants to react (animation, sound).</summary>
        protected virtual void OnStateEntered(EnemyState previous) { }

        private static AlertLevel AlertFor(EnemyState value)
        {
            switch (value)
            {
                case EnemyState.Suspicious: return AlertLevel.Suspicious;
                case EnemyState.Chase:
                case EnemyState.Attack:
                case EnemyState.Stagger: return AlertLevel.Alarm;
                default: return AlertLevel.Calm;
            }
        }

        /// <summary>Gives a definition to an enemy made in code (before it wakes up).</summary>
        public void Assign(EnemyDefinition value) => definition = value;
    }
}
