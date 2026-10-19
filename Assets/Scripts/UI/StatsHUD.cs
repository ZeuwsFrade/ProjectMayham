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

        private Image locked;

        private void OnEnable()
        {
            if (stats == null) return;
            stats.HealthChanged += OnHealthChanged;
            stats.StaminaChanged += OnStaminaChanged;
            stats.StaminaCapChanged += OnStaminaCapChanged;
            ShowCap(stats.StaminaCapFraction);
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
            stats.StaminaCapChanged -= OnStaminaCapChanged;
            stats.ExhaustedChanged -= OnExhaustedChanged;
            stats.HealthRecoveryChanged -= OnHealthRecoveryChanged;
        }

        private void OnStaminaCapChanged(float usable, float max) => ShowCap(usable / max);

        /// <summary>Hunger takes the right part of the stamina bar away: it is covered with a dark strip.</summary>
        private void ShowCap(float fraction)
        {
            if (stamina.fill == null || stamina.fill.parent == null) return;
            if (locked == null)
            {
                locked = UIKit.Panel(stamina.fill.parent, "HungerLock", new Color(0.32f, 0.16f, 0.04f, 0.9f));
                locked.raycastTarget = false;
            }
            var rect = locked.rectTransform;
            rect.anchorMin = new Vector2(Mathf.Clamp01(fraction), 0f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            locked.enabled = fraction < 0.999f;
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
