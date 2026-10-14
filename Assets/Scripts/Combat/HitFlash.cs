using ProjectMayham.Player;
using UnityEngine;

namespace ProjectMayham.Combat
{
    /// <summary>
    /// Flashes the sprites red when what they belong to is hurt. Wires itself to the <see cref="Health"/> or the
    /// <see cref="PlayerStats"/> next to it. Only the colour is changed, never the alpha, which the darkness of the
    /// vision system owns.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        [SerializeField] private Color flashColor = new Color(1f, 0.3f, 0.25f, 1f);
        [SerializeField, Min(0.02f)] private float seconds = 0.14f;

        private SpriteRenderer[] renderers;
        private Color[] original;
        private float timer;
        private Health health;
        private PlayerStats stats;

        private void Awake()
        {
            health = GetComponent<Health>();
            stats = GetComponent<PlayerStats>();
        }

        private void OnEnable()
        {
            if (health != null) health.Damaged += OnHealthDamaged;
            if (stats != null) stats.Damaged += OnStatsDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnHealthDamaged;
            if (stats != null) stats.Damaged -= OnStatsDamaged;
            Restore();
        }

        private void OnHealthDamaged(DamageInfo _) => Play();
        private void OnStatsDamaged(float _) => Play();

        /// <summary>Starts a flash; one that is running starts over.</summary>
        public void Play()
        {
            if (timer <= 0f) Capture();
            timer = seconds;
        }

        private void Capture()
        {
            // Looked for now, not at the start: the held weapon makes its sprite when it is taken in the hands.
            renderers = GetComponentsInChildren<SpriteRenderer>();
            original = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) original[i] = renderers[i].color;
        }

        private void Update()
        {
            if (timer <= 0f) return;

            // Unscaled: the flash is seen through the freeze of the hit that caused it, and ends on time.
            timer -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / seconds);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color from = original[i];
                Color mixed = Color.Lerp(from, flashColor, t);
                // The alpha is whatever it is now; vision may be fading the sprite meanwhile.
                mixed.a = renderers[i].color.a;
                renderers[i].color = mixed;
            }
            if (timer <= 0f) Restore();
        }

        private void Restore()
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = original[i];
                c.a = renderers[i].color.a;
                renderers[i].color = c;
            }
            timer = 0f;
        }
    }
}
