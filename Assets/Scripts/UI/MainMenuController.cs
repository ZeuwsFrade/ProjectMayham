using ProjectMayham.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject settingsPanel;

        private void Awake()
        {
            if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGameClicked);
            if (loadGameButton != null)
            {
                loadGameButton.onClick.AddListener(OnLoadGameClicked);
                loadGameButton.interactable = GameSession.HasSave;
            }
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
        }

        // A new game starts with a fresh save, in the tutorial or (skipping it) straight in the shelter.
        public void OnNewGameClicked()
        {
            ConfirmDialog.Ask(
                "Новая игра",
                "Пройти обучение? Оно займёт несколько минут и закончится в убежище.",
                Tutorial.TutorialRun.Begin,
                () =>
                {
                    GameSession.NewGame();
                    SceneLoader.Load(SceneLoader.Shelter, "Убежище", save: false);
                },
                yesText: "Пройти", noText: "Пропустить");
        }

        public void OnLoadGameClicked()
        {
            if (!GameSession.Load()) return;
            SceneLoader.Load(SceneLoader.Shelter, "Убежище", save: false);
        }

        public void OnSettingsClicked()
        {
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        public void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
