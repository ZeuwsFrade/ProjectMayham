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

        // A new game starts in the shelter with a fresh save.
        public void OnNewGameClicked()
        {
            GameSession.NewGame();
            SceneLoader.Load(SceneLoader.Shelter, "Убежище", save: false);
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
