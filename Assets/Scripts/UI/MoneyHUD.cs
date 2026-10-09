using ProjectMayham.Core;
using TMPro;
using UnityEngine;

namespace ProjectMayham.UI
{
    /// <summary>Shows the player's money in the top-right corner of the HUD canvas it is put on.</summary>
    public class MoneyHUD : MonoBehaviour
    {
        private TMP_Text label;

        private void Awake()
        {
            label = UIKit.Label(transform, string.Empty, 34f, TextAlignmentOptions.Right, UIKit.Accent);
            label.gameObject.name = "Money";
            UIKit.Place(label.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(420f, 50f));
            label.rectTransform.pivot = new Vector2(1f, 1f);
        }

        private void OnEnable()
        {
            GameSession.MoneyChanged += Show;
            Show(GameSession.Money);
        }

        private void OnDisable() => GameSession.MoneyChanged -= Show;

        private void Show(int money) => label.text = $"{money} $";
    }
}
