using System.Collections.Generic;
using ProjectMayham.Combat;
using ProjectMayham.Level;
using UnityEngine;

namespace ProjectMayham.AI
{
    /// <summary>
    /// The "Ram": a hulk that sees but is deaf. Footsteps, blows and the cries of the others mean nothing to it; only
    /// what is in front of its eyes. When it sees the player it stops and aims, following them with its eyes while it
    /// sees them and no farther (whoever leaves its view in time is aimed past), then runs straight at where it aimed
    /// and nothing stops it: barricades fly apart, enemies in the way are trampled, the player is run down. A wall stops
    /// it, and then it stands dazed for a while: the time to hit it. Its numbers are in a <see cref="RamDefinition"/>.
    /// </summary>
    public class Ram : EnemyAI
    {
        private enum Phase
        {
            Aim,
            Charge,
            Brake
        }

        // Seconds it slows down for after a run that hit no wall, before it looks for the player again.
        private const float BrakeSeconds = 0.6f;
        private const float StepSeconds = 0.3f;
        // A run that has gone on this long and is still this slow (share of the full speed) is stuck on something.
        private const float StuckAfter = 0.35f;
        private const float StuckShare = 0.2f;

        private readonly HashSet<Object> trampled = new HashSet<Object>();
        private readonly Collider2D[] hits = new Collider2D[16];

        private RamDefinition ram;
        private ContactFilter2D trampleFilter;
        private LayerMask crashMask;

        private Phase phase;
        private float phaseTime;
        private Vector2 chargeDirection;
        private float travelled;
        private Vector2 lastPosition;
        private float stepTimer;

        /// <summary>True while it runs.</summary>
        public bool IsCharging => State == EnemyState.Attack && phase == Phase.Charge;
        /// <summary>True while it stands still and aims.</summary>
        public bool IsAiming => State == EnemyState.Attack && phase == Phase.Aim;
        public Vector2 ChargeDirection => chargeDirection;

        protected override bool Unstoppable => IsCharging;

        protected override void Awake()
        {
            base.Awake();
            if (!enabled) return;

            ram = Definition as RamDefinition;
            if (ram == null)
            {
                Debug.LogError($"{name}: a Ram needs a RamDefinition.", this);
                enabled = false;
                return;
            }
            // Barricades are furniture; what is not a barricade among the furniture stops the run.
            trampleFilter = new ContactFilter2D { useTriggers = false };
            trampleFilter.SetLayerMask(LayerMask.GetMask("Enemy", "Player", "Furniture"));
            crashMask = LayerMask.GetMask("Obstacle", "Window", "Furniture");
        }

        // ---- the attack is the charge ----

        protected override void EnterAttack()
        {
            phase = Phase.Aim;
            phaseTime = 0f;
            trampled.Clear();
            Motor.Stop();
            CombatEvents.RaiseSwingStarted(new SwingEvent { Attacker = gameObject, Position = Motor.Position, Direction = transform.up });
        }

        protected override void TickAttack(float dt)
        {
            phaseTime += dt;
            switch (phase)
            {
                case Phase.Aim:
                    // Turns after the player while it sees them; the moment they are out of its view it stops turning.
                    if (CanSeePlayer && Target != null) Motor.LookAt(EnemyMotor.Near(Target.transform.position, Motor.Position));
                    ShowWindup(phaseTime / Definition.WindupSeconds);
                    if (phaseTime >= Definition.WindupSeconds) StartCharge();
                    break;

                case Phase.Charge:
                    stepTimer -= dt;
                    if (stepTimer <= 0f)
                    {
                        stepTimer = StepSeconds;
                        Noise.Emit(Motor.Position, ram.ChargeNoise, gameObject, Team.Enemy);
                    }
                    break;

                case Phase.Brake:
                    if (phaseTime >= BrakeSeconds) FinishAttack();
                    break;
            }
        }

        private void StartCharge()
        {
            ShowWindup(0f);
            phase = Phase.Charge;
            phaseTime = 0f;
            stepTimer = 0f;
            travelled = 0f;
            chargeDirection = transform.up;
            lastPosition = Motor.Position;
            Motor.Drive(chargeDirection * ram.ChargeSpeed, ram.ChargeAcceleration);
        }

        private void FixedUpdate()
        {
            if (!IsCharging) return;

            Vector2 position = Motor.Position;
            Vector2 step = position - lastPosition;
            // A jump of several units is the world moving it to another copy of the level, not a run.
            if (step.sqrMagnitude < 9f) travelled += step.magnitude;
            lastPosition = position;

            TrampleAhead(position);
            if (!IsCharging) return;

            float speed = Motor.Velocity.magnitude;
            float radius = Motor.BodyRadius;
            var wall = Physics2D.CircleCast(position, radius * 0.9f, chargeDirection, 0.08f + speed * Time.fixedDeltaTime, crashMask);
            if (wall.collider != null && wall.collider.GetComponentInParent<Barricade>() == null)
            {
                Crash();
                return;
            }
            if (phaseTime > StuckAfter && speed < ram.ChargeSpeed * StuckShare)
            {
                Crash();
                return;
            }
            if (travelled >= ram.ChargeDistance) Brake();
        }

        // Whatever is right in front of it: barricades fly apart, bodies are thrown aside. Each is hit once a run.
        private void TrampleAhead(Vector2 position)
        {
            float radius = Motor.BodyRadius;
            int count = Physics2D.OverlapCircle(position + chargeDirection * radius, radius + 0.2f, trampleFilter, hits);
            for (int i = 0; i < count; i++)
            {
                var other = hits[i];
                if (other == null || other.transform.IsChildOf(transform)) continue;

                var barricade = other.GetComponentInParent<Barricade>();
                if (barricade != null)
                {
                    barricade.Smash();
                    Noise.Emit(position, ram.CrashNoise, gameObject, Team.Enemy);
                    ShakeNearPlayer(0.5f);
                    continue;
                }

                var damageable = other.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive || !trampled.Add((Object)damageable)) continue;
                RunDown(damageable, other.transform.position, position);
            }
        }

        private void RunDown(IDamageable victim, Vector2 at, Vector2 position)
        {
            Vector2 target = EnemyMotor.Near(at, position);
            // Thrown forward and to the side it was on: out of the way of the run.
            // Right in the middle it picks a side: a body pushed straight ahead would only be shoved along in front.
            Vector2 away = target - position;
            Vector2 side = away - Vector2.Dot(away, chargeDirection) * chargeDirection;
            if (side.sqrMagnitude < 0.01f) side = new Vector2(-chargeDirection.y, chargeDirection.x) * (Random.value < 0.5f ? -1f : 1f);
            Vector2 direction = (chargeDirection * 0.7f + side.normalized).normalized;

            var info = new DamageInfo
            {
                Amount = victim.Team == Team.Player ? ram.ChargeDamage : ram.TrampleDamage,
                Point = target,
                Direction = direction,
                Knockback = ram.ChargeKnockback,
                StaggerSeconds = ram.ChargeStagger,
                Heavy = true,
                Attacker = gameObject
            };
            victim.TakeDamage(info);
            CombatEvents.RaiseHitLanded(new HitEvent
            {
                Attacker = gameObject, Target = victim, Point = target, Direction = direction,
                Damage = info.Amount, Heavy = true, Killed = !victim.IsAlive
            });
            Noise.Emit(target, ram.CrashNoise * 0.6f, gameObject, Team.Enemy);
        }

        // Into a wall: it stands dazed.
        private void Crash()
        {
            Motor.Stop();
            Noise.Emit(Motor.Position, ram.CrashNoise, gameObject, Team.Enemy);
            ShakeNearPlayer(1f);
            EnterStagger(ram.StunSeconds);
        }

        // The run is over and nothing stopped it: it slows down and looks round.
        private void Brake()
        {
            phase = Phase.Brake;
            phaseTime = 0f;
            Motor.Stop();
        }

        private void ShakeNearPlayer(float strength)
        {
            if (Target == null) return;
            float distance = Vector2.Distance(Motor.Position, EnemyMotor.Near(Target.transform.position, Motor.Position));
            float share = 1f - Mathf.Clamp01(distance / 12f);
            if (share > 0f) CameraShake.Add(0.45f * strength * share);
        }

        private void OnDrawGizmosSelected()
        {
            if (!IsCharging && !IsAiming) return;
            Gizmos.color = IsCharging ? Color.red : new Color(1f, 0.6f, 0f);
            Vector2 from = transform.position;
            Gizmos.DrawLine(from, from + (IsCharging ? chargeDirection : (Vector2)transform.up) * 4f);
        }
    }
}
