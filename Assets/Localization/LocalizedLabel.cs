using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace ProjectMayham.Localization
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedLabel : MonoBehaviour
    {
        [SerializeField] private LocalizedString text = new LocalizedString();
        private TMP_Text label;

        private void Awake() => label = GetComponent<TMP_Text>();

        private void OnEnable() => text.StringChanged += UpdateText;

        private void OnDisable() => text.StringChanged -= UpdateText;

        private void UpdateText(string value) => label.text = value;
    }
}
