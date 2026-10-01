using UnityEngine;

namespace ProjectMayham.World
{
    /// <summary>
    /// Put on anything that lives in a wrapped level besides the player (enemies, items, trigger zones):
    /// <see cref="WrapWorld"/> keeps it at the copy of its position that is nearest to the player.
    /// </summary>
    public class WrapEntity : MonoBehaviour
    {
        public Rigidbody2D Body { get; private set; }

        private void Awake() => Body = GetComponent<Rigidbody2D>();
        private void OnEnable() => WrapWorld.Register(this);
        private void OnDisable() => WrapWorld.Unregister(this);
    }
}
