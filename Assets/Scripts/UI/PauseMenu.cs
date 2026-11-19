using ProjectMayham.Core;
using ProjectMayham.Level;
using ProjectMayham.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    [DefaultExecutionOrder(200)]
    public sealed class PauseMenu : ModalWindow
    {
        private const string LossWarning = "Вы потеряете всё снаряжение при себе и прогресс заданий за этот рейд. Тайник и результаты прошлых рейдов сохранятся.";
        private RaidController raid;
        private GameObject settings;
        private RectTransform card;
        private TMP_Text title;
        private Button resumeButton;
        private Button settingsButton;
        private Button abandonButton;
        private Button mainMenuButton;

        protected override int SortingOrder => 260;

        public static void Install(RaidController raid = null) => Get<PauseMenu>().raid = raid;

        protected override void Update()
        {
            if (IsOpen)
            {
                base.Update();
                if (IsOpen && !card.gameObject.activeSelf && (settings == null || !settings.activeSelf))
                {
                    RefreshLabels();
                    card.gameObject.SetActive(true);
                }
                return;
            }

            if (!SceneLoader.IsLoading && !ModalState.IsOpen && !EscapeConsumed && (raid == null || raid.CanPause)
                && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ConsumeEscape();
                Show();
            }
        }

        protected override void Build(Canvas canvas)
        {
            UIKit.Stretch(UIKit.Panel(canvas.transform, "PauseDim", UIKit.Dim).rectTransform);
            card = UIKit.Panel(canvas.transform, "PauseCard", UIKit.PanelColor).rectTransform;
            var center = new Vector2(0.5f, 0.5f);
            UIKit.Place(card, center, Vector2.zero, new Vector2(480f, raid == null ? 304f : 368f));

            title = UIKit.Label(card, "Пауза", 40f, TextAlignmentOptions.Center, UIKit.Accent);
            var top = new Vector2(0.5f, 1f);
            UIKit.Place(title.rectTransform, top, new Vector2(0f, -24f), new Vector2(416f, 52f));
            resumeButton = AddButton("Продолжить", Close, -94f);
            settingsButton = AddButton("Настройки", OpenSettings, -158f);
            mainMenuButton = AddButton("Главное меню", () => ConfirmLeave(true), -222f);
            if (raid != null)
            {
                abandonButton = AddButton("Покинуть рейд", () => ConfirmLeave(false), -286f);
                var colors = abandonButton.colors;
                colors.normalColor = new Color(0.43f, 0.20f, 0.22f);
                colors.selectedColor = colors.normalColor;
                colors.highlightedColor = new Color(0.56f, 0.26f, 0.28f);
                abandonButton.colors = colors;
            }

            var prefab = Resources.Load<GameObject>("UI/GameSettings");
            if (prefab != null)
            {
                settings = Instantiate(prefab, canvas.transform, false);
                settings.SetActive(false);
                UIKit.Stretch((RectTransform)settings.transform);
            }
            settingsButton.interactable = settings != null;
        }

        private Button AddButton(string text, UnityEngine.Events.UnityAction action, float offset)
        {
            var button = UIKit.MakeButton(card, text, action, 28f);
            UIKit.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, offset), new Vector2(392f, 52f));
            return button;
        }

        protected override void OnOpened() => RefreshLabels();

        private void RefreshLabels()
        {
            title.text = GameText.Translate("Пауза");
            UIKit.SetButtonText(resumeButton, "Продолжить");
            UIKit.SetButtonText(settingsButton, "Настройки");
            UIKit.SetButtonText(mainMenuButton, "Главное меню");
            if (abandonButton != null) UIKit.SetButtonText(abandonButton, "Покинуть рейд");
        }

        private void OpenSettings()
        {
            if (settings == null) return;
            card.gameObject.SetActive(false);
            settings.SetActive(true);
        }

        protected override void OnCancel()
        {
            if (settings != null && settings.activeSelf) settings.GetComponent<SettingsPanelController>().OnCloseClicked();
            else Close();
        }

        private void ConfirmLeave(bool toMainMenu)
        {
            ConfirmDialog.Ask(
                toMainMenu ? "Выйти в главное меню?" : "Покинуть рейд?",
                raid != null ? LossWarning : "Игра будет сохранена.",
                () =>
                {
                    Close();
                    if (raid != null) raid.Abandon(toMainMenu);
                    else SceneLoader.Load(SceneLoader.MainMenu, "Главное меню");
                },
                yesText: "Выйти", noText: "Остаться");
        }
    }
}
