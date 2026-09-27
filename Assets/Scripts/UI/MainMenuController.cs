using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string newGameSceneName = "MainScene";
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject settingsPanel;

        private void Awake()
        {
            if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGameClicked);
            if (loadGameButton != null) loadGameButton.onClick.AddListener(OnLoadGameClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
        }

        public void OnNewGameClicked()
        {
            SceneManager.LoadScene(newGameSceneName);
        }

        // Заглушка: система сохранений ещё не реализована.
        public void OnLoadGameClicked()
        {
            Debug.Log("Load Game is not implemented yet.");
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
