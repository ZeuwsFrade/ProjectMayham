using ProjectMayham.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    /// <summary>
    /// What the player needs to know in a fight, drawn on the HUD canvas it is put on: the weapon in the hands with
    /// its durability, the charge of a heavy blow and short messages (out of breath, the weapon broke). Built from
    /// code like the other windows. Shows nothing while the player holds no weapon.
    /// </summary>
    public class CombatHUD : MonoBehaviour
    {
        private static readonly Color Normal = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color Worn = new Color(1f, 0.55f, 0.3f, 1f);
        private static readonly Color Charged = new Color(1f, 0.82f, 0.35f, 1f);

        [SerializeField] private MeleeCombat combat;
        [Tooltip("Distance of the panel from the bottom edge of the screen, in reference pixels.")]
        [SerializeField] private float bottomOffset = 190f;

        private RectTransform panel;
        private TMP_Text weaponLabel;
        private RectTransform durabilityFill;
        private Image durabilityImage;
        private RectTransform chargeFrame;
        private RectTransform chargeFill;
        private Image chargeImage;
        private TMP_Text messageLabel;

        private void Awake()
        {
            panel = UIKit.Rect("CombatPanel", transform);
            UIKit.Place(panel, new Vector2(0.5f, 0f), new Vector2(0f, bottomOffset), new Vector2(420f, 90f));

            weaponLabel = UIKit.Label(panel, string.Empty, 26f, TextAlignmentOptions.Center, Normal);
            weaponLabel.gameObject.name = "Weapon";
            UIKit.Place(weaponLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(420f, 34f));
            weaponLabel.rectTransform.pivot = new Vector2(0.5f, 1f);

            durabilityFill = MakeBar("Durability", new Vector2(0f, -42f), 260f, 8f, out durabilityImage);
            chargeFill = MakeBar("Charge", new Vector2(0f, -58f), 260f, 10f, out chargeImage);
            chargeFrame = (RectTransform)chargeFill.parent;

            messageLabel = UIKit.Label(transform, string.Empty, 30f, TextAlignmentOptions.Center, UIKit.Bad);
            messageLabel.gameObject.name = "CombatMessage";
            UIKit.Place(messageLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(700f, 44f));
        }

        // A thin bar: a dark frame and a fill that grows from the left by its right anchor.
        private RectTransform MakeBar(string barName, Vector2 position, float width, float height, out Image fillImage)
        {
            var frame = UIKit.Panel(panel, barName, new Color(0f, 0f, 0f, 0.55f));
            UIKit.Place(frame.rectTransform, new Vector2(0.5f, 1f), position, new Vector2(width, height));
            frame.raycastTarget = false;

            fillImage = UIKit.Panel(frame.transform, "Fill", Color.white);
            fillImage.raycastTarget = false;
            var fill = fillImage.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(1f, 1f);
            fill.offsetMax = new Vector2(-1f, -1f);
            return fill;
        }

        private void Update()
        {
            if (combat == null)
            {
                combat = FindFirstObjectByType<MeleeCombat>();
                panel.gameObject.SetActive(false);
                messageLabel.text = string.Empty;
                return;
            }

            var weapon = combat.Weapon;
            panel.gameObject.SetActive(weapon != null);
            messageLabel.text = combat.Message;
            if (weapon == null) return;

            weaponLabel.text = weapon.DisplayName;
            float durability = combat.DurabilityFraction;
            SetFill(durabilityFill, durability);
            durabilityImage.color = durability < 0.25f ? Worn : Normal;
            durabilityImage.transform.parent.gameObject.SetActive(weapon.Wears);

            float charge = combat.ChargeProgress;
            bool charging = combat.Phase == MeleePhase.Charging;
            chargeFrame.gameObject.SetActive(charging);
            SetFill(chargeFill, charge);
            chargeImage.color = charge >= 1f ? Charged : Normal;
        }

        private static void SetFill(RectTransform fill, float share)
        {
            var max = fill.anchorMax;
            max.x = Mathf.Clamp01(share);
            fill.anchorMax = max;
        }
    }
}
