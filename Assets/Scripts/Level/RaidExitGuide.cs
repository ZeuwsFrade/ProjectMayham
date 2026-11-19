using ProjectMayham.Core;
using ProjectMayham.Localization;
using ProjectMayham.Player;
using ProjectMayham.UI;
using ProjectMayham.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectMayham.Level
{
    [RequireComponent(typeof(PlayerStats))]
    public sealed class RaidExitGuide : MonoBehaviour
    {
        public const float ActiveSeconds = 5f;
        public const float CooldownSeconds = 10f;

        private RaidExit exit;
        private PlayerStats player;
        private Canvas canvas;
        private RectTransform panel;
        private TMP_Text status;
        private RectTransform arrow;
        private float activeUntil;
        private float readyAt;

        public bool IsGuiding => exit != null && Time.time < activeUntil;
        public float CooldownRemaining => Mathf.Clamp(readyAt - Time.time, 0f, CooldownSeconds);

        private void Awake() => player = GetComponent<PlayerStats>();

        public void ReadSign(RaidExit target)
        {
            if (target == null) return;
            exit = target;
            TryActivate();
        }

        private void TryActivate()
        {
            if (exit == null || Time.time < readyAt || player.IsDead || ModalState.IsOpen || SceneLoader.IsLoading) return;
            activeUntil = Time.time + ActiveSeconds;
            readyAt = activeUntil + CooldownSeconds;
            if (canvas == null) Build();
        }

        private void LateUpdate()
        {
            bool available = exit != null && !player.IsDead && !ModalState.IsOpen && !SceneLoader.IsLoading;
            if (available && Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame) TryActivate();
            if (canvas == null) return;

            canvas.enabled = available;
            if (!available) return;

            bool guiding = IsGuiding;
            if (arrow.gameObject.activeSelf != guiding)
            {
                arrow.gameObject.SetActive(guiding);
                panel.sizeDelta = new Vector2(310f, guiding ? 122f : 48f);
                status.rectTransform.anchoredPosition = new Vector2(0f, guiding ? -42f : 0f);
            }
            var text = guiding ? GameText.Translate("Направление к выходу") : CooldownRemaining > 0f
                ? GameText.Format("Указатель: {0} с", Mathf.CeilToInt(CooldownRemaining))
                : GameText.Translate("[O] Указать выход");
            if (status.text != text) status.text = text;
            if (!guiding) return;

            Vector2 delta = Direction(transform.position, exit.Position);
            if (delta.sqrMagnitude < 0.0001f) return;
            var camera = Camera.main;
            if (camera != null) delta = new Vector2(Vector2.Dot(delta, camera.transform.right), Vector2.Dot(delta, camera.transform.up));
            arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f);
        }

        public static Vector2 Direction(Vector2 origin, Vector2 target) => WrapWorld.Active != null
            ? WrapWorld.Active.Delta(origin, target)
            : target - origin;

        private void Build()
        {
            canvas = UIKit.NewCanvas("RaidExitGuide", 55, transform);
            canvas.GetComponent<GraphicRaycaster>().enabled = false;
            panel = UIKit.Panel(canvas.transform, "ExitCompass", UIKit.PanelColor).rectTransform;
            UIKit.Place(panel, Vector2.one, new Vector2(-36f, -120f), new Vector2(310f, 122f));

            var center = new Vector2(0.5f, 0.5f);
            arrow = UIKit.Rect("ExitArrow", panel);
            UIKit.Place(arrow, center, new Vector2(0f, 18f), new Vector2(62f, 62f));
            Stroke("Shaft", new Vector2(0f, -5f), 35f, 0f);
            Stroke("Left", new Vector2(-9f, 10f), 26f, -45f);
            Stroke("Right", new Vector2(9f, 10f), 26f, 45f);
            status = UIKit.Label(panel, string.Empty, 24f, TextAlignmentOptions.Center, UIKit.Accent);
            UIKit.Place(status.rectTransform, center, new Vector2(0f, -42f), new Vector2(292f, 32f));

            void Stroke(string name, Vector2 offset, float length, float angle)
            {
                var line = UIKit.Panel(arrow, name, UIKit.Accent).rectTransform;
                UIKit.Place(line, center, offset, new Vector2(7f, length));
                line.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }
}
