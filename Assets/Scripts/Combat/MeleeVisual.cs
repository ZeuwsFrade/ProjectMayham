using UnityEngine;

namespace ProjectMayham.Combat
{
    /// <summary>
    /// Two thin lines on the ground that tell where a blow will land: the reach of the raised weapon, and the
    /// path of a swing that fades behind it. Made by <see cref="MeleeCombat"/>; nothing to set up.
    /// </summary>
    public class MeleeVisual : MonoBehaviour
    {
        private const int Segments = 20;
        private static readonly Color ReachColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color ReadyColor = new Color(1f, 0.82f, 0.35f, 1f);
        private static readonly Color LightColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color HeavyColor = new Color(1f, 0.55f, 0.35f, 1f);

        private LineRenderer reach;
        private LineRenderer trail;

        public static MeleeVisual Create(Transform owner)
        {
            // Not a child: the lines are drawn in world space and must not turn with the player. It lives in the
            // scene of the player and goes with it.
            var go = new GameObject("MeleeVisual");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, owner.gameObject.scene);
            var visual = go.AddComponent<MeleeVisual>();
            visual.reach = visual.MakeLine("Reach", 0.04f);
            visual.trail = visual.MakeLine("Trail", 0.09f);
            return visual;
        }

        public void Hide()
        {
            if (reach != null) reach.enabled = false;
            if (trail != null) trail.enabled = false;
        }

        /// <summary>The reach of the raised weapon: an arc of the weapon's width around the aim.</summary>
        public void ShowReach(Vector2 origin, float centerAngle, float arc, float range, float alpha, bool ready)
        {
            if (trail != null) trail.enabled = false;
            DrawArc(reach, origin, centerAngle - arc * 0.5f, centerAngle + arc * 0.5f, range);
            var color = ready ? ReadyColor : ReachColor;
            color.a = alpha;
            SetColor(reach, color);
        }

        /// <summary>The part of the arc the weapon has passed. <paramref name="fade"/> 0..1 dims it after the blow.</summary>
        public void ShowTrail(Vector2 origin, float centerAngle, float arc, int sign, float sweep, float range, bool heavy, float fade = 0f)
        {
            if (reach != null) reach.enabled = false;
            float start = centerAngle - sign * arc * 0.5f;
            float end = start + sign * arc * Mathf.Clamp01(sweep);
            DrawArc(trail, origin, start, end, range * 0.92f);

            var color = heavy ? HeavyColor : LightColor;
            color.a = 0.75f * (1f - Mathf.Clamp01(fade));
            SetColor(trail, color);
        }

        private LineRenderer MakeLine(string lineName, float width)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = Segments + 1;
            line.widthMultiplier = width;
            line.numCapVertices = 3;
            line.sortingOrder = 30;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.enabled = false;
            return line;
        }

        private static void DrawArc(LineRenderer line, Vector2 origin, float fromAngle, float toAngle, float radius)
        {
            if (line == null) return;

            line.enabled = true;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = Mathf.Lerp(fromAngle, toAngle, i / (float)Segments) * Mathf.Deg2Rad;
                line.SetPosition(i, new Vector3(origin.x + Mathf.Cos(angle) * radius, origin.y + Mathf.Sin(angle) * radius, 0f));
            }
        }

        private static void SetColor(LineRenderer line, Color color)
        {
            if (line == null) return;
            line.startColor = line.endColor = color;
        }
    }
}
