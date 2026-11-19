using ProjectMayham.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectMayham.Level
{
    public sealed class RaidAtmosphere : MonoBehaviour
    {
        private static readonly Color[] Tints =
        {
            new Color(0.84f, 1f, 0.91f),
            new Color(1f, 0.81f, 0.52f),
            new Color(1f, 0.54f, 0.49f)
        };

        private static readonly float[] Brightness = { 1f, 0.87f, 0.72f };
        private RaidGenerator raid;
        private Transform player;
        private Light2D ambient;
        private Color baseColor;
        private float baseIntensity;
        private RaidZone? previous;

        public void Begin(RaidGenerator generator, Transform anchor)
        {
            raid = generator;
            player = anchor;
            foreach (var light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (light.lightType != Light2D.LightType.Global || light.gameObject.scene != gameObject.scene) continue;
                ambient = light;
                baseColor = light.color;
                baseIntensity = light.intensity;
                break;
            }
        }

        private void Update()
        {
            if (raid == null || player == null) return;
            var zone = raid.ZoneAt(player.position);
            if (previous != zone)
            {
                previous = zone;
                ScreenNote.Show(RaidZones.Title(zone), 3f);
            }
            if (ambient == null) return;
            int index = (int)zone;
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3.5f);
            ambient.color = Color.Lerp(ambient.color, baseColor * Tints[index], blend);
            ambient.intensity = Mathf.Lerp(ambient.intensity, baseIntensity * Brightness[index], blend);
        }
    }
}
