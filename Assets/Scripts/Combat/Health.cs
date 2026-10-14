using System;
using UnityEngine;

namespace ProjectMayham.Combat
{
    /// <summary>
    /// Health of everything that is not the player: enemies, props that break. The player has their own
    /// (<see cref="Player.PlayerStats"/>), which also feeds stamina and saving.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private Team team = Team.Enemy;
        [SerializeField, Min(1f)] private float maxHealth = 30f;
        [Tooltip("Takes no damage (a target dummy that must not fall).")]
        [SerializeField] private bool invulnerable;

        private float current;
        private bool alive = true;

        /// <summary>A blow that did not kill. A killing blow raises only <see cref="Died"/>.</summary>
        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;

        public Team Team => team;
        public bool IsAlive => alive;
        public float Current => current;
        public float Max => maxHealth;
        public float Fraction => maxHealth > 0f ? current / maxHealth : 0f;
        public bool Invulnerable { get => invulnerable; set => invulnerable = value; }

        private void Awake() => current = maxHealth;

        /// <summary>Sets the health from outside (an enemy definition) and fills it.</summary>
        public void Configure(float max, Team side)
        {
            maxHealth = Mathf.Max(1f, max);
            team = side;
            current = maxHealth;
            alive = true;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!alive || invulnerable || info.Amount <= 0f) return;

            current = Mathf.Max(0f, current - info.Amount);
            if (current > 0f)
            {
                Damaged?.Invoke(info);
                return;
            }

            alive = false;
            Died?.Invoke(info);
            CombatEvents.RaiseActorDied(gameObject);
        }

        /// <summary>Back to full health and alive: for a respawn or a test.</summary>
        public void Revive()
        {
            current = maxHealth;
            alive = true;
        }
    }
}
