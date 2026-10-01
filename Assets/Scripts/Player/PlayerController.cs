using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.Player
{
    /// <summary>
    /// Top-down controller: WASD movement with inertia, Shift to sprint, body turns toward the mouse cursor.
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

        [Header("Rotation")]
        [Tooltip("Degrees added to the aim angle. -90 for a sprite that looks up (+Y), 0 for one that looks right (+X).")]
        [SerializeField] private float rotationOffset = -90f;
        [Tooltip("Max turn speed in degrees per second.")]
        [SerializeField] private float turnSpeed = 540f;

        private Rigidbody2D body;
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
        public bool IsSprinting => sprintHeld && moveInput.sqrMagnitude > 0.01f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
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
            float speed = IsSprinting ? sprintSpeed : walkSpeed;
            Vector2 target = moveInput * speed;
            float rate = moveInput.sqrMagnitude > 0.01f ? acceleration : deceleration;
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, target, rate * Time.fixedDeltaTime);
        }

        private void ApplyRotation()
        {
            Vector2 dir = AimPoint - body.position;
            if (dir.sqrMagnitude < 0.0001f) return;

            float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + rotationOffset;
            float angle = Mathf.MoveTowardsAngle(body.rotation, targetAngle, turnSpeed * Time.fixedDeltaTime);
            body.MoveRotation(angle);
        }
    }
}
