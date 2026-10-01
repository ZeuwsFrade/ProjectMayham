using UnityEngine;

namespace ProjectMayham.Player
{
    /// <summary>Smoothly follows a target in 2D, keeping the camera's Z.</summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float smoothTime = 0.15f;

        private Vector3 velocity;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 goal = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
        }
    }
}
