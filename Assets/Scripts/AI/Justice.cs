using ProjectMayham.Combat;
using UnityEngine;

namespace ProjectMayham.AI
{
    /// <summary>
    /// "Justice": blind, slow and very hard to bring down. It walks the doorways of a room and finds the player only by
    /// ear and by touch (its sight reaches no farther than its arms, see its <see cref="EnemyDefinition"/>). It goes to
    /// every loud noise, the din of the others too, and does not know who made it: stirred up, it strikes whatever it
    /// runs into, enemies included. Easier to slip past or lure away with a thrown thing than to fight.
    /// </summary>
    public class Justice : EnemyAI
    {
        private const float FeelSeconds = 0.2f;

        private readonly Collider2D[] touched = new Collider2D[12];
        private ContactFilter2D bodies;
        private LayerMask wallMask;
        private float feelTimer;

        protected override void Awake()
        {
            base.Awake();
            bodies = new ContactFilter2D { useTriggers = false };
            bodies.SetLayerMask(LayerMask.GetMask("Enemy"));
            wallMask = LayerMask.GetMask("Obstacle", "Window");
        }

        protected override void Think(float dt)
        {
            // Calm, it lets the others pass; stirred up by a noise, it hits whatever it bumps into in the dark.
            if (State != EnemyState.Suspicious && State != EnemyState.Chase) return;
            if (!AttackReady || CanSeePlayer) return;

            feelTimer -= dt;
            if (feelTimer > 0f) return;
            feelTimer = FeelSeconds;

            var victim = FeelForVictim();
            if (victim != null) BeginAttack(victim);
        }

        /// <summary>The nearest living thing within arm's reach that is not of its own kind, or null.</summary>
        private Health FeelForVictim()
        {
            Vector2 me = Motor.Position;
            float reach = Definition.AttackRange;
            int count = Physics2D.OverlapCircle(me, reach, bodies, touched);

            Health best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var health = touched[i] != null ? touched[i].GetComponentInParent<Health>() : null;
                if (health == null || !health.IsAlive || health.gameObject == gameObject) continue;
                // Its own kind it knows by the step.
                if (health.GetComponent<Justice>() != null) continue;

                Vector2 at = EnemyMotor.Near(health.transform.position, me);
                float distance = Vector2.Distance(me, at);
                if (distance >= bestDistance || Physics2D.Linecast(me, at, wallMask)) continue;
                best = health;
                bestDistance = distance;
            }
            return best;
        }
    }
}
