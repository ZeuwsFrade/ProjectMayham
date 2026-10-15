using System.Collections.Generic;
using UnityEngine;

namespace ProjectMayham.AI
{
    /// <summary>
    /// Divides a ring around the player among the enemies that are after them, so a crowd closes in from all
    /// sides instead of queuing up in a corridor-wide line. Each enemy gets one place on the ring; the places are
    /// spread evenly and handed out in the order the enemies already stand in, so nobody has to cross the ring.
    /// </summary>
    public static class Swarm
    {
        private static readonly List<(EnemyAI enemy, float angle)> ring = new List<(EnemyAI, float)>();

        /// <summary>
        /// The angle, in degrees around <paramref name="center"/>, of the place on the ring that belongs to
        /// <paramref name="me"/>. Alone, an enemy keeps the angle it already has.
        /// </summary>
        /// <param name="reach">Only enemies within this distance of the centre share the ring.</param>
        public static float SlotAngle(EnemyAI me, Vector2 center, float reach)
        {
            ring.Clear();
            foreach (var other in EnemyAI.All)
            {
                if (!other.IsAlive || !other.IsHunting) continue;
                Vector2 offset = EnemyMotor.Near(other.Position, center) - center;
                if (offset.magnitude > reach) continue;
                ring.Add((other, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg));
            }

            Vector2 mine = EnemyMotor.Near(me.Position, center) - center;
            float myAngle = Mathf.Atan2(mine.y, mine.x) * Mathf.Rad2Deg;

            int count = ring.Count;
            if (count <= 1) return myAngle;

            ring.Sort((a, b) => a.angle.CompareTo(b.angle));
            float step = 360f / count;

            // The turn of the whole ring that moves the enemies least: the circular mean of how far each stands from its place.
            float sin = 0f, cos = 0f;
            int myRank = 0;
            for (int i = 0; i < count; i++)
            {
                float off = (ring[i].angle - i * step) * Mathf.Deg2Rad;
                sin += Mathf.Sin(off);
                cos += Mathf.Cos(off);
                if (ring[i].enemy == me) myRank = i;
            }
            float turn = Mathf.Atan2(sin, cos) * Mathf.Rad2Deg;
            return turn + myRank * step;
        }
    }
}
