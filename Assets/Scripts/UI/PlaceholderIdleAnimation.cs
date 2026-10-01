using UnityEngine;

namespace ProjectMayham.UI
{
    // Плейсхолдер-анимация для заглушки арта персонажа.
    // Заменить на реальный спрайт/Animator, когда появится арт.
    public class PlaceholderIdleAnimation : MonoBehaviour
    {
        [SerializeField] private float bobAmplitude = 15f;
        [SerializeField] private float bobSpeed = 1.5f;
        [SerializeField] private float scalePulseAmount = 0.04f;
        [SerializeField] private float scalePulseSpeed = 1.2f;

        private RectTransform _rectTransform;
        private Vector2 _initialAnchoredPosition;
        private Vector3 _initialScale;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _initialAnchoredPosition = _rectTransform.anchoredPosition;
            _initialScale = _rectTransform.localScale;
        }

        private void Update()
        {
            float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            _rectTransform.anchoredPosition = _initialAnchoredPosition + new Vector2(0f, bobOffset);

            float pulse = 1f + Mathf.Sin(Time.time * scalePulseSpeed) * scalePulseAmount;
            _rectTransform.localScale = _initialScale * pulse;
        }
    }
}
