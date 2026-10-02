using System;
using ProjectMayham.Player;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    /// <summary>Health and stamina bars in the top-left corner of the HUD.</summary>
    public class StatsHUD : MonoBehaviour
    {
        [Serializable]
        public class Bar
        {
            [Tooltip("Stretched from the left edge of its parent; its right anchor follows the value.")]
            public RectTransform fill;
            public Image fillImage;
            public Color normalColor = Color.white;
            [Tooltip("Color while the bar is in its special state (out of breath / recovering health).")]
            public Color stateColor = Color.gray;

            [NonSerialized] public float target = 1f;
            [NonSerialized] public float shown = 1f;
        }

        [SerializeField] private PlayerStats stats;
        [SerializeField] private Bar health = new Bar();
        [SerializeField] private Bar stamina = new Bar();
        [Tooltip("How fast the bars catch up with the real value, in bar widths per second.")]
        [SerializeField, Min(0.01f)] private float fillSpeed = 2f;

        private void OnEnable()
        {
            if (stats == null) return;
            stats.HealthChanged += OnHealthChanged;
            stats.StaminaChanged += OnStaminaChanged;
            stats.ExhaustedChanged += OnExhaustedChanged;
            stats.HealthRecoveryChanged += OnHealthRecoveryChanged;

            health.target = health.shown = stats.HealthFraction;
            stamina.target = stamina.shown = stats.StaminaFraction;
            Apply(health);
            Apply(stamina);
            SetState(health, stats.IsRecoveringHealth);
            SetState(stamina, stats.IsExhausted);
        }

        private void OnDisable()
        {
            if (stats == null) return;
            stats.HealthChanged -= OnHealthChanged;
            stats.StaminaChanged -= OnStaminaChanged;
            stats.ExhaustedChanged -= OnExhaustedChanged;
            stats.HealthRecoveryChanged -= OnHealthRecoveryChanged;
        }

        private void Update()
        {
            Animate(health);
            Animate(stamina);
        }

        private void OnHealthChanged(float current, float max) => health.target = current / max;
        private void OnStaminaChanged(float current, float max) => stamina.target = current / max;
        private void OnExhaustedChanged(bool value) => SetState(stamina, value);
        private void OnHealthRecoveryChanged(bool value) => SetState(health, value);

        private void Animate(Bar bar)
        {
            if (Mathf.Approximately(bar.shown, bar.target)) return;
            bar.shown = Mathf.MoveTowards(bar.shown, bar.target, fillSpeed * Time.deltaTime);
            Apply(bar);
        }

        private static void Apply(Bar bar)
        {
            if (bar.fill == null) return;
            var max = bar.fill.anchorMax;
            max.x = Mathf.Clamp01(bar.shown);
            bar.fill.anchorMax = max;
        }

        private static void SetState(Bar bar, bool special)
        {
            if (bar.fillImage != null) bar.fillImage.color = special ? bar.stateColor : bar.normalColor;
        }
    }
}
