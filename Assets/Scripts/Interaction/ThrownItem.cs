using ProjectMayham.Combat;
using ProjectMayham.Items;
using UnityEngine;

namespace ProjectMayham.Interaction
{
    /// <summary>
    /// A thing in flight: thrown by the player, it sails along an arc to where it was aimed, lands with a noise and
    /// lies there to be picked up again. The noise is what it is for: every enemy that hears it goes to look, so a
    /// thrown thing lures them away from the player.
    /// </summary>
    public class ThrownItem : MonoBehaviour
    {
        private const float Spin = 540f;
        // How much bigger it looks at the top of the arc.
        private const float Lift = 0.35f;

        private Vector2 from;
        private Vector2 to;
        private float duration;
        private float time;
        private float noise;
        private Vector3 scale;
        private float spinSide;
        private ItemPickup pickup;

        /// <summary>Something has landed, at this point, making its noise.</summary>
        public static event System.Action<ThrownItem, Vector2> Landed;

        /// <summary>The noise a thing makes when it lands: heavier things are louder.</summary>
        public static float NoiseOf(ItemDefinition item) => Mathf.Clamp(4f + (item != null ? item.Weight : 0f) * 1.5f, 4f, 12f);

        /// <summary>Sends an item lying in the world (its pickup prefab) flying from one point to another.</summary>
        public static ThrownItem Launch(GameObject instance, Vector2 from, Vector2 to, float speed, float noiseRadius)
        {
            var thrown = instance.AddComponent<ThrownItem>();
            thrown.from = from;
            thrown.to = to;
            thrown.duration = Mathf.Max(0.12f, Vector2.Distance(from, to) / Mathf.Max(0.1f, speed));
            thrown.noise = noiseRadius;
            thrown.scale = instance.transform.localScale;
            thrown.spinSide = Random.value < 0.5f ? -1f : 1f;
            // Nobody picks it out of the air.
            thrown.pickup = instance.GetComponent<ItemPickup>();
            if (thrown.pickup != null) thrown.pickup.enabled = false;
            instance.transform.position = from;
            return thrown;
        }

        private void Update()
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / duration);
            transform.position = Vector2.Lerp(from, to, k);
            transform.localScale = scale * (1f + Lift * Mathf.Sin(k * Mathf.PI));
            transform.Rotate(0f, 0f, Spin * spinSide * Time.deltaTime);
            if (k >= 1f) Land();
        }

        private void Land()
        {
            transform.position = to;
            transform.localScale = scale;
            if (pickup != null) pickup.enabled = true;
            // Whose noise it is nobody can tell: everyone that hears it comes, and finds a thing on the floor.
            Noise.Emit(to, noise, gameObject, Team.Neutral);
            Landed?.Invoke(this, to);
            Destroy(this);
        }
    }
}
