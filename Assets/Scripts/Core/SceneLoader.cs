using ProjectMayham.UI;
using UnityEngine;

namespace ProjectMayham.Core
{
    /// <summary>Scene names and the one way to change scene: through the loading screen, saving first.</summary>
    public static class SceneLoader
    {
        public const string MainMenu = "MainMenu";
        public const string Shelter = "Shelter";
        public const string Raid = "MainScene";

        public static bool IsLoading => LoadingScreen.IsBusy;

        /// <summary>Saves the game, shows the loading screen and switches scene. Does nothing while a load is running.</summary>
        public static void Load(string sceneName, string caption = null, bool save = true)
        {
            if (IsLoading) return;

            if (save) GameSession.Save();
            ModalState.Reset();
            LoadingScreen.Show(sceneName, caption);
        }
    }
}
