using UnityEngine;

namespace ProjectMayham.Combat
{
    /// <summary>
    /// Shakes the camera by "trauma": a blow adds some, it fades out by itself and the shake grows with its square,
    /// so small knocks are barely felt and big ones are. It runs on real time, so the freeze of a hit shakes too.
    /// The offset is taken off at the start of every frame and put on at the end, after the camera follow has
    /// moved the camera, so the two do not fight.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class CameraShake : MonoBehaviour
    {
        private static CameraShake instance;

        [Tooltip("Largest offset of the camera, in world units, at full trauma.")]
        [SerializeField, Min(0f)] private float maxOffset = 0.45f;
        [Tooltip("How fast trauma fades, per second.")]
        [SerializeField, Min(0.1f)] private float decay = 2.2f;
        [SerializeField, Min(1f)] private float frequency = 28f;

        private float trauma;
        private Vector3 applied;
        private float seed;

        /// <summary>Adds a shake to the main camera; the component is put on it when it has none.</summary>
        /// <param name="amount">0..1, small knock to heavy blow.</param>
        public static void Add(float amount)
        {
            if (amount <= 0f) return;

            if (instance == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                instance = cam.GetComponent<CameraShake>();
                if (instance == null) instance = cam.gameObject.AddComponent<CameraShake>();
            }
            instance.trauma = Mathf.Clamp01(instance.trauma + amount);
        }

        // Not called Reset: on a MonoBehaviour that name is Unity's own message.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private void Awake()
        {
            instance = this;
            seed = Random.value * 100f;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnDisable() => Remove();

        // Before the camera follow: it must see the camera where it left it.
        private void Update() => Remove();

        private void LateUpdate()
        {
            if (trauma <= 0f) return;

            float dt = Time.unscaledDeltaTime;
            trauma = Mathf.Max(0f, trauma - decay * dt);

            float power = trauma * trauma * maxOffset;
            float t = Time.unscaledTime * frequency;
            applied = new Vector3(Noise(t, 0f), Noise(t, 17.3f), 0f) * power;
            transform.position += applied;
        }

        private float Noise(float t, float salt) => Mathf.PerlinNoise(seed + salt, t) * 2f - 1f;

        private void Remove()
        {
            if (applied == Vector3.zero) return;
            transform.position -= applied;
            applied = Vector3.zero;
        }
    }
}
