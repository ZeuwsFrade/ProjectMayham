using ProjectMayham.Player;
using UnityEngine;

namespace ProjectMayham.Combat
{
    /// <summary>
    /// The noise the player makes by moving: nothing when standing, a little when walking, a lot when sprinting.
    /// Enemies that hear it come to look. A raised weapon slows the steps, which is also quieter; crouching
    /// makes no sound at all.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerNoise : MonoBehaviour
    {
        [Tooltip("How far a walking player is heard, in world units.")]
        [SerializeField, Min(0f)] private float walkRadius = 3.5f;
        [Tooltip("How far a sprinting player is heard.")]
        [SerializeField, Min(0f)] private float sprintRadius = 9f;
        [Tooltip("Seconds between two steps' worth of noise.")]
        [SerializeField, Min(0.1f)] private float interval = 0.4f;
        [Tooltip("Speed below this makes no noise at all.")]
        [SerializeField, Min(0f)] private float minSpeed = 0.8f;

        private PlayerController controller;
        private float timer;

        private void Awake() => controller = GetComponent<PlayerController>();

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            if (controller.IsCrouching || controller.Velocity.magnitude < minSpeed) return;
            timer = interval;
            Noise.Emit(transform.position, controller.IsSprinting ? sprintRadius : walkRadius, gameObject);
        }
    }
}
