using System.Collections.Generic;
using ProjectMayham.Core;
using ProjectMayham.Interaction;
using ProjectMayham.Items;
using ProjectMayham.Player;
using ProjectMayham.UI;
using ProjectMayham.Vision;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.Combat
{
    public enum MeleePhase
    {
        /// <summary>The weapon hangs in the hands (or the hands are empty).</summary>
        Idle,
        /// <summary>Right mouse button held: the weapon is up.</summary>
        Raised,
        /// <summary>Left mouse button held: the blow is being charged.</summary>
        Charging,
        /// <summary>The blow sweeps its arc and hurts whatever it passes.</summary>
        Swinging,
        /// <summary>The blow is over and the weapon comes back.</summary>
        Recovering
    }

    /// <summary>
    /// Melee fighting of the player with the weapon in their hands. Right mouse button raises the weapon (slower
    /// walk, narrower view), left mouse button swings it along an arc, held it charges a heavy blow. Every blow
    /// costs stamina and every landed one wears the weapon. Damage goes through <see cref="IDamageable"/>; a swing
    /// and a hit are published in <see cref="CombatEvents"/> and as noise for the enemies.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(PlayerStats), typeof(GridInventory))]
    public class MeleeCombat : MonoBehaviour
    {
        // A press during the end of a blow is remembered this long, so a quick second click is not lost.
        private const float BufferSeconds = 0.25f;
        // The sweep may be this many degrees ahead of the weapon, so a target at the very edge of the arc is not missed.
        private const float ArcMargin = 4f;
        // A target is hit when it is within this much farther than the weapon's range.
        private const float RangeMargin = 0.05f;

        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("Layers of the things that can be hurt. Empty: the layer Enemy.")]
        [SerializeField] private LayerMask hitMask;
        [Tooltip("Layers that stop a blow: walls and the glass of windows. Empty: Obstacle and Window.")]
        [SerializeField] private LayerMask wallMask;
        [Tooltip("The window of the inventory; combat is off while it is open. Looked for in the scene when empty.")]
        [SerializeField] private InventoryWindow inventoryWindow;
        [Tooltip("Show the reach of the weapon on the ground while it is raised, and the path of a swing.")]
        [SerializeField] private bool showArc = true;
        [Tooltip("Seconds a message (out of breath, the weapon broke) stays on screen.")]
        [SerializeField, Min(0.5f)] private float messageSeconds = 1.6f;

        private readonly Collider2D[] found = new Collider2D[24];
        private readonly HashSet<IDamageable> struck = new HashSet<IDamageable>();
        private PlayerController controller;
        private PlayerStats stats;
        private GridInventory inventory;
        private PlayerClimber climber;
        private HeldItemView heldView;
        private MeleeVisual visual;
        private Rigidbody2D body;
        private InputAction aimAction;
        private InputAction attackAction;
        private ContactFilter2D hitFilter;

        private MeleePhase phase;
        private WeaponDefinition weapon;
        private float chargeTime;
        private float phaseTime;
        private float bufferedPress = -1f;
        private float messageTimer;
        private bool attackWasHeld;

        // The swing in progress.
        private bool heavy;
        private float centerAngle;
        private float arc;
        private float sweep;
        private int sweepSign = -1;
        private bool anyHit;
        private bool breakAfterSwing;

        public MeleePhase Phase => phase;
        /// <summary>The weapon in the hands, or null.</summary>
        public WeaponDefinition Weapon => weapon;
        public bool IsRaised => phase != MeleePhase.Idle;
        /// <summary>0..1 while a blow is being charged; 1 = the blow will be a heavy one.</summary>
        public float ChargeProgress => phase == MeleePhase.Charging && weapon != null ? Mathf.Clamp01(chargeTime / weapon.ChargeSeconds) : 0f;
        /// <summary>Durability left in the weapon, 0..1 (1 when there is none or it does not wear out).</summary>
        public float DurabilityFraction => WeaponWear.Fraction(weapon);
        /// <summary>Why the last blow did not happen, or what happened to the weapon. Empty when there is nothing to tell.</summary>
        public string Message { get; private set; } = string.Empty;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            stats = GetComponent<PlayerStats>();
            inventory = GetComponent<GridInventory>();
            climber = GetComponent<PlayerClimber>();
            heldView = GetComponent<HeldItemView>();
            body = GetComponent<Rigidbody2D>();
            aimAction = inputActions.FindAction("Player/Aim", true);
            attackAction = inputActions.FindAction("Player/Attack", true);

            if (hitMask == 0) hitMask = LayerMask.GetMask("Enemy");
            if (wallMask == 0) wallMask = LayerMask.GetMask("Obstacle", "Window");
            hitFilter = new ContactFilter2D { useTriggers = false };
            hitFilter.SetLayerMask(hitMask);

            if (inventoryWindow == null) inventoryWindow = FindFirstObjectByType<InventoryWindow>(FindObjectsInactive.Include);
            visual = MeleeVisual.Create(transform);
        }

        private void OnEnable()
        {
            aimAction.Enable();
            attackAction.Enable();
        }

        private void OnDisable()
        {
            aimAction.Disable();
            attackAction.Disable();
            Lower();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (messageTimer > 0f)
            {
                messageTimer -= Time.unscaledDeltaTime;
                if (messageTimer <= 0f) Message = string.Empty;
            }

            // A press is also seen when the button is down now and was not at the last look: the frame in which the
            // click happened can be one that the action system reports no "this frame" for.
            bool attackHeld = attackAction.IsPressed();
            bool pressed = attackAction.WasPressedThisFrame() || (attackHeld && !attackWasHeld);
            attackWasHeld = attackHeld;

            weapon = inventory.HeldItem as WeaponDefinition;
            if (weapon == null || IsBlocked())
            {
                // A swing is let finish only while the weapon is still there; anything else lowers it.
                if (phase != MeleePhase.Idle) Lower();
                return;
            }

            bool aim = aimAction.IsPressed();
            if (pressed && phase != MeleePhase.Raised && phase != MeleePhase.Idle) bufferedPress = Time.unscaledTime;

            switch (phase)
            {
                case MeleePhase.Idle:
                    if (aim) Enter(MeleePhase.Raised);
                    break;

                case MeleePhase.Raised:
                    if (!aim) Lower();
                    else if (pressed || HasBufferedPress()) BeginCharge();
                    break;

                case MeleePhase.Charging:
                    if (!aim) { Lower(); break; }
                    chargeTime += dt;
                    if (!attackAction.IsPressed()) Release();
                    break;

                case MeleePhase.Swinging:
                    TickSwing(dt);
                    break;

                case MeleePhase.Recovering:
                    phaseTime += dt;
                    if (phaseTime >= weapon.RecoverSecondsOf(heavy))
                    {
                        Enter(aim ? MeleePhase.Raised : MeleePhase.Idle);
                        if (!aim) Lower();
                    }
                    break;
            }

            ApplyStance();
            ShowPose();
        }

        // ---- phases ----

        private bool IsBlocked()
        {
            return stats.IsDead
                || ModalState.IsOpen
                || SceneLoader.IsLoading
                || (inventoryWindow != null && inventoryWindow.IsOpen)
                || (climber != null && climber.IsClimbing);
        }

        private bool HasBufferedPress() => bufferedPress > 0f && Time.unscaledTime - bufferedPress <= BufferSeconds;

        private void Enter(MeleePhase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        private void BeginCharge()
        {
            bufferedPress = -1f;
            if (stats.IsExhausted || stats.Stamina < weapon.StaminaCost)
            {
                Say("Не хватает сил");
                return;
            }

            chargeTime = 0f;
            Enter(MeleePhase.Charging);
        }

        private void Release()
        {
            // Too tired for the charged blow: a normal one is better than none.
            bool charged = chargeTime >= weapon.ChargeSeconds;
            if (charged && stats.TrySpendStamina(weapon.HeavyStaminaCost)) heavy = true;
            else if (stats.TrySpendStamina(weapon.StaminaCost)) heavy = false;
            else
            {
                Say("Не хватает сил");
                Enter(MeleePhase.Raised);
                return;
            }

            StartSwing();
        }

        private void StartSwing()
        {
            Vector2 origin = body.position;
            Vector2 aim = controller.AimPoint - origin;
            if (aim.sqrMagnitude < 0.0001f) aim = transform.up;

            centerAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            arc = weapon.ArcOf(heavy);
            // Every blow comes from the other side than the one before: it reads as a fight, not as a stamp.
            sweepSign = -sweepSign;
            sweep = 0f;
            anyHit = false;
            breakAfterSwing = false;
            struck.Clear();
            Enter(MeleePhase.Swinging);

            Vector2 direction = new Vector2(Mathf.Cos(centerAngle * Mathf.Deg2Rad), Mathf.Sin(centerAngle * Mathf.Deg2Rad));
            CombatEvents.RaiseSwingStarted(new SwingEvent { Attacker = gameObject, Weapon = weapon, Position = origin, Direction = direction, Heavy = heavy });
            Noise.Emit(origin, weapon.SwingNoise * (heavy ? 1.3f : 1f), gameObject);
        }

        private void TickSwing(float dt)
        {
            phaseTime += dt;
            float t = Mathf.Clamp01(phaseTime / weapon.SwingSecondsOf(heavy));
            sweep = Mathf.SmoothStep(0f, 1f, t);
            Strike();

            if (t < 1f) return;

            if (!anyHit)
            {
                CombatEvents.RaiseSwingMissed(new SwingEvent
                {
                    Attacker = gameObject, Weapon = weapon, Position = body.position,
                    Direction = new Vector2(Mathf.Cos(centerAngle * Mathf.Deg2Rad), Mathf.Sin(centerAngle * Mathf.Deg2Rad)), Heavy = heavy
                });
            }
            if (breakAfterSwing)
            {
                Break();
                return;
            }
            Enter(MeleePhase.Recovering);
        }

        /// <summary>Hurts everything the weapon has passed on its way so far, once each.</summary>
        private void Strike()
        {
            Vector2 origin = body.position;
            float range = weapon.Range;
            int count = Physics2D.OverlapCircle(origin, range + 0.6f, hitFilter, found);

            // The part of the arc that has been swept, measured from the centre along the direction of the swing.
            float from = -arc * 0.5f - ArcMargin;
            float to = -arc * 0.5f + arc * sweep + ArcMargin;

            for (int i = 0; i < count; i++)
            {
                var target = found[i].GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || target.Team == Team.Player || struck.Contains(target)) continue;

                Vector2 nearest = found[i].ClosestPoint(origin);
                Vector2 offset = nearest - origin;
                if (offset.magnitude > range + RangeMargin) continue;

                float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                float relative = sweepSign * Mathf.DeltaAngle(centerAngle, angle);
                if (relative < from || relative > to) continue;
                if (Physics2D.Linecast(origin, nearest, wallMask)) continue;

                struck.Add(target);
                Land(target, nearest, offset);
            }
        }

        private void Land(IDamageable target, Vector2 point, Vector2 offset)
        {
            Vector2 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : (Vector2)transform.up;
            var info = new DamageInfo
            {
                Amount = weapon.DamageOf(heavy),
                Point = point,
                Direction = direction,
                Knockback = heavy ? weapon.HeavyKnockback : weapon.Knockback,
                StaggerSeconds = heavy ? weapon.HeavyStagger : weapon.Stagger,
                Heavy = heavy,
                Attacker = gameObject,
                Weapon = weapon
            };
            target.TakeDamage(info);
            bool killed = !target.IsAlive;
            anyHit = true;

            // The weight of the blow: a stop of the whole game, a jolt of the screen, a sound that carries.
            float stop = heavy ? weapon.HeavyHitStop : weapon.HitStop;
            HitStop.Request(killed ? stop * 1.4f : stop);
            CameraShake.Add(heavy ? weapon.HeavyShake : weapon.Shake);
            Noise.Emit(point, weapon.HitNoise, gameObject);
            CombatEvents.RaiseHitLanded(new HitEvent
            {
                Attacker = gameObject, Weapon = weapon, Target = target, Point = point,
                Direction = direction, Damage = info.Amount, Heavy = heavy, Killed = killed
            });

            if (WeaponWear.Wear(weapon, weapon.WearPerHit(heavy))) breakAfterSwing = true;
        }

        private void Break()
        {
            var broken = weapon;
            if (inventory.TakeHeld(out _) && broken.BrokenItem != null && inventory.Add(broken.BrokenItem, 1) > 0)
            {
                // No room in the backpack: the scrap falls to the floor.
                if (TryGetComponent<PlayerInteractor>(out var interactor)) interactor.DropToWorld(broken.BrokenItem, 1);
            }

            weapon = null;
            Lower();
            Say($"{broken.DisplayName}: сломалось");
            CameraShake.Add(0.3f);
            CombatEvents.RaiseWeaponBroken(broken);
        }

        /// <summary>Back to the plain stance without any cost: the weapon is put down, the movement is free again.</summary>
        private void Lower()
        {
            phase = MeleePhase.Idle;
            phaseTime = 0f;
            chargeTime = 0f;
            bufferedPress = -1f;
            struck.Clear();

            if (controller != null)
            {
                controller.SpeedMultiplier = 1f;
                controller.TurnMultiplier = 1f;
                controller.SprintBlocked = false;
            }
            var cone = VisionCone.Active;
            if (cone != null) cone.ConeScale = 1f;
            if (heldView != null) heldView.ClearPose();
            if (visual != null) visual.Hide();
        }

        // ---- stance ----

        private void ApplyStance()
        {
            if (phase == MeleePhase.Idle) return;

            float speed = weapon.RaisedSpeedMultiplier;
            float turn = 1f;
            switch (phase)
            {
                case MeleePhase.Charging:
                    // The heavier the weapon is held back, the slower the legs.
                    speed *= Mathf.Lerp(1f, 0.8f, ChargeProgress);
                    break;
                case MeleePhase.Swinging:
                    speed = weapon.SwingSpeedMultiplier;
                    turn = 0.25f;
                    break;
            }

            controller.SpeedMultiplier = speed;
            controller.TurnMultiplier = turn;
            controller.SprintBlocked = true;
            var cone = VisionCone.Active;
            if (cone != null) cone.ConeScale = weapon.RaisedConeScale;
        }

        // ---- picture ----

        private void ShowPose()
        {
            if (phase == MeleePhase.Idle) return;

            Vector2 origin = body.position;
            Vector2 aim = controller.AimPoint - origin;
            float aimAngle = aim.sqrMagnitude > 0.0001f ? Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg : transform.eulerAngles.z + 90f;
            // The blow after this one comes from the side opposite to the last one.
            int next = -sweepSign;
            float restRelative = -0.7f;
            float worldAngle;

            switch (phase)
            {
                case MeleePhase.Charging:
                {
                    float pull = Mathf.Lerp(restRelative, -1.2f, ChargeProgress);
                    // At full charge the weapon trembles: it is ready.
                    if (ChargeProgress >= 1f) pull += Mathf.Sin(Time.unscaledTime * 55f) * 0.04f;
                    worldAngle = aimAngle + next * pull * weapon.ArcOf(false) * 0.5f;
                    visual.ShowReach(origin, aimAngle, weapon.ArcOf(ChargeProgress >= 1f), weapon.Range, 0.12f + 0.3f * ChargeProgress, ChargeProgress >= 1f);
                    break;
                }
                case MeleePhase.Swinging:
                    worldAngle = centerAngle + sweepSign * (-arc * 0.5f + arc * sweep);
                    visual.ShowTrail(origin, centerAngle, arc, sweepSign, sweep, weapon.Range, heavy);
                    break;
                case MeleePhase.Recovering:
                {
                    float back = Mathf.Clamp01(phaseTime / Mathf.Max(0.01f, weapon.RecoverSecondsOf(heavy)));
                    float endAngle = centerAngle + sweepSign * arc * 0.5f;
                    float restAngle = aimAngle + next * restRelative * weapon.ArcOf(false) * 0.5f;
                    worldAngle = Mathf.LerpAngle(endAngle, restAngle, Mathf.SmoothStep(0f, 1f, back));
                    visual.ShowTrail(origin, centerAngle, arc, sweepSign, 1f, weapon.Range, heavy, fade: back);
                    break;
                }
                default:
                    worldAngle = aimAngle + next * restRelative * weapon.ArcOf(false) * 0.5f;
                    if (showArc) visual.ShowReach(origin, aimAngle, weapon.Arc, weapon.Range, 0.12f, false);
                    break;
            }

            if (heldView == null) return;
            Vector2 direction = new Vector2(Mathf.Cos(worldAngle * Mathf.Deg2Rad), Mathf.Sin(worldAngle * Mathf.Deg2Rad));
            heldView.SetPose(origin + direction * (weapon.Range * 0.55f), worldAngle - 90f + weapon.HeldAngle);
        }

        private void Say(string text)
        {
            Message = text;
            messageTimer = messageSeconds;
        }
    }
}
