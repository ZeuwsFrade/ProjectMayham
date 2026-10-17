using UnityEngine;

namespace ProjectMayham.AI
{
    /// <summary>
    /// The "Dokhodyaga" (the wretch): the first enemy. Alone it is easy meat, slow, frail and quick to lose its
    /// footing. Its strength is in numbers: the one that finds the player cries out and the others come, and
    /// when they are near they do not queue up but spread round the player, so that many weak blows land at once.
    /// All the numbers are in its <see cref="EnemyDefinition"/>; this class only adds the going round.
    /// </summary>
    public class Dokhodyaga : EnemyAI
    {
        // How long a place on the ring is kept before it is worked out again.
        private const float SlotSeconds = 0.35f;

        private float slotAngle;
        private float slotTimer;

        protected override Vector2 ChaseDestination(Vector2 playerPosition, bool visible)
        {
            // Out of sight there is nothing to surround: it goes to where the player was.
            if (!visible || Definition.SurroundRange <= 0f || DistanceToPlayer > Definition.SurroundRange) return playerPosition;

            slotTimer -= Time.deltaTime;
            if (slotTimer <= 0f)
            {
                slotTimer = SlotSeconds * (0.8f + Random.value * 0.4f);
                slotAngle = Swarm.SlotAngle(this, playerPosition, Definition.SurroundRange + 2f);
            }

            float radians = slotAngle * Mathf.Deg2Rad;
            Vector2 place = playerPosition + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * (Definition.AttackRange * 0.8f);

            // A place inside a wall or behind it is no place: it goes straight for the player instead.
            var grid = NavGrid.Active;
            if (grid != null && grid.IsReady && !grid.IsWalkable(place)) return playerPosition;
            return place;
        }
    }
}
