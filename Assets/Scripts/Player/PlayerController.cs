using ProjectMayham.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.Player
{
    /// <summary>
    /// Top-down controller: WASD movement with inertia, Shift to sprint, body turns toward the mouse cursor.
    /// Sprinting needs stamina (<see cref="PlayerStats"/>); carried weight above the comfortable limit slows the
    /// player down and forbids sprinting.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 6f;
        [Tooltip("How fast velocity grows toward the target (units/s^2). Lower = heavier start.")]
        [SerializeField] private float acceleration = 14f;
        [Tooltip("How fast velocity drops when no input is held (units/s^2). Lower = longer slide.")]
        [SerializeField] private float deceleration = 10f;
        [Tooltip("Speed multiplier at the inventory's max weight. Between the comfortable and max weight speed drops linearly to it.")]
        [SerializeField, Range(0f, 1f)] private float speedAtMaxWeight = 0.35f;

        [Header("Rotation")]
        [Tooltip("Degrees added to the aim angle. -90 for a sprite that looks up (+Y), 0 for one that looks right (+X).")]
        [SerializeField] private float rotationOffset = -90f;
        [Tooltip("Max turn speed in degrees per second.")]
        [SerializeField] private float turnSpeed = 540f;

        private Rigidbody2D body;
        private PlayerStats stats;
        private Inventory inventory;
        private Camera cam;
        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction sprintAction;
        private InputAction lookAction;

        private Vector2 moveInput;
        private bool sprintHeld;

        public Vector2 Velocity => body.linearVelocity;
        /// <summary>Cursor position in world space, refreshed every frame.</summary>
        public Vector2 AimPoint { get; private set; }
        public bool IsSprinting => !MovementLocked && !SprintBlocked && sprintHeld && moveInput.sqrMagnitude > 0.01f && CanSprint;
        /// <summary>Walking speed is multiplied by this (a raised weapon slows the player down). 1 = no change.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        /// <summary>Turning speed is multiplied by this (a swing commits the player to a direction). 1 = no change.</summary>
        public float TurnMultiplier { get; set; } = 1f;
        /// <summary>True while sprinting is not allowed whatever the stamina is (weapon raised).</summary>
        public bool SprintBlocked { get; set; }
        /// <summary>False while out of breath or carrying more than the comfortable weight.</summary>
        public bool CanSprint => (stats == null || stats.CanSprint) && (inventory == null || !inventory.IsOverloaded);
        /// <summary>
        /// While true the movement keys are ignored and the velocity is left to whoever set the lock
        /// (climbing over a barricade). Aiming keeps working.
        /// </summary>
        public bool MovementLocked { get; set; }
        /// <summary>What the move keys ask for right now, whether or not the character can move.</summary>
        public Vector2 MoveInput => moveInput;
        /// <summary>
        /// While set the character looks at this point instead of the mouse cursor: for a window that needs the
        /// mouse while the game goes on.
        /// </summary>
        public Vector2? LookAt { get; set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            stats = GetComponent<PlayerStats>();
            inventory = GetComponent<Inventory>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            playerMap = inputActions.FindActionMap("Player", true);
            moveAction = playerMap.FindAction("Move", true);
            sprintAction = playerMap.FindAction("Sprint", true);
            lookAction = playerMap.FindAction("Look", true);
        }

        private void OnEnable() => playerMap.Enable();
        private void OnDisable() => playerMap.Disable();

        private void Update()
        {
            moveInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            sprintHeld = sprintAction.IsPressed();
            UpdateAimPoint();
        }

        private void UpdateAimPoint()
        {
            if (LookAt.HasValue)
            {
                AimPoint = LookAt.Value;
                return;
            }

            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Vector2 screen = lookAction.ReadValue<Vector2>();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            AimPoint = world;
        }

        private void FixedUpdate()
        {
            ApplyMovement();
            ApplyRotation();
        }

        private void ApplyMovement()
        {
            if (MovementLocked) return;

            float speed = IsSprinting ? sprintSpeed : walkSpeed;
            if (inventory != null) speed *= Mathf.Lerp(1f, speedAtMaxWeight, inventory.Encumbrance);
            speed *= SpeedMultiplier;
            Vector2 target = moveInput * speed;
            float rate = moveInput.sqrMagnitude > 0.01f ? acceleration : deceleration;
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, target, rate * Time.fixedDeltaTime);
        }

        private void ApplyRotation()
        {
            Vector2 dir = AimPoint - body.position;
            if (dir.sqrMagnitude < 0.0001f) return;

            float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + rotationOffset;
            float angle = Mathf.MoveTowardsAngle(body.rotation, targetAngle, turnSpeed * TurnMultiplier * Time.fixedDeltaTime);
            body.MoveRotation(angle);
        }

        /// <summary>Throws the player away from a blow. The movement keys pull the velocity back to normal within a moment.</summary>
        public void Knock(Vector2 velocity)
        {
            if (MovementLocked) return;
            body.linearVelocity += velocity;
        }
    }
}
