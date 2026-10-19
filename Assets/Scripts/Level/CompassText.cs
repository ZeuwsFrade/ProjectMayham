using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>Which way something lies, in words: one of eight points of the compass, north being up on the screen.</summary>
    public static class CompassText
    {
        /// <summary>Closer than this (world units, a tile is one) a thing is "right here", whatever its direction.</summary>
        public const float Near = 3f;
        public const string Here = "совсем рядом";

        // From the east, turning left; said as "where": "на севере".
        private static readonly string[] Where =
            { "на востоке", "на северо-востоке", "на севере", "на северо-западе", "на западе", "на юго-западе", "на юге", "на юго-востоке" };

        /// <summary>"на северо-востоке" for a thing up and to the right of where one stands; <see cref="Here"/> when it is close.</summary>
        public static string Describe(Vector2 delta)
        {
            if (delta.magnitude < Near) return Here;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            int sector = Mathf.RoundToInt(angle / 45f);
            return Where[((sector % 8) + 8) % 8];
        }
    }
}
