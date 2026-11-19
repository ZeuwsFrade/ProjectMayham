using UnityEngine;

namespace ProjectMayham.Level
{
    public enum RaidZone
    {
        Entry,
        Risk,
        Deep
    }

    public static class RaidZones
    {
        public static RaidZone Classify(float distance, float entryRadius, float riskRadius) =>
            distance < entryRadius ? RaidZone.Entry : distance < riskRadius ? RaidZone.Risk : RaidZone.Deep;

        public static Vector2 Delta(Vector2 origin, Vector2 position, float period)
        {
            var delta = position - origin;
            if (period <= 0f) return delta;
            delta.x -= period * Mathf.Round(delta.x / period);
            delta.y -= period * Mathf.Round(delta.y / period);
            return delta;
        }

        public static string Title(RaidZone zone) => zone switch
        {
            RaidZone.Entry => "Зона 1 · Внешние кабинеты",
            RaidZone.Risk => "Зона 2 · Опасный сектор",
            _ => "Зона 3 · Глубокий сектор"
        };
    }
}
