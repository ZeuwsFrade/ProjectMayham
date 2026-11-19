using ProjectMayham.Combat;
using ProjectMayham.Core;
using ProjectMayham.Player;
using ProjectMayham.Quests;
using ProjectMayham.UI;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// Only extraction via <see cref="RaidExit"/> banks loot and raid objectives.
    /// </summary>
    public class RaidController : MonoBehaviour
    {
        [SerializeField] private PlayerStats stats;
        [SerializeField] private PlayerPersistence persistence;
        [Tooltip("Scene the raid ends in: the shelter (a test raid names its test shelter here).")]
        [SerializeField] private string shelterScene = SceneLoader.Shelter;
        private bool ending;
        // The raid ends when the death animation has played; a player without one ends it at the moment of death.
        private PlayerDeath death;

        public bool CanPause => !ending && (stats == null || !stats.IsDead) && (death == null || !death.IsDying);

        private void Start() => PauseMenu.Install(this);

        private void OnEnable()
        {
            if (stats == null) return;

            death = stats.GetComponent<PlayerDeath>();
            if (death != null) death.OnDied += OnDied;
            else stats.Died += OnDied;
        }

        private void OnDisable()
        {
            if (stats == null) return;

            if (death != null) death.OnDied -= OnDied;
            stats.Died -= OnDied;
            death = null;
        }

        /// <summary>Asks whether to go back to the shelter; yes ends the raid with what the player carries.</summary>
        public void AskToLeave()
        {
            if (!CanPause || SceneLoader.IsLoading || ModalState.IsOpen) return;
            ConfirmDialog.Ask(
                "Вернуться в убежище?",
                "Вы покинете рейд. Всё, что в рюкзаке, останется при вас.",
                Extract,
                yesText: "Вернуться", noText: "Остаться");
        }

        private void Extract()
        {
            if (!CanPause || SceneLoader.IsLoading) return;
            ending = true;
            FindFirstObjectByType<RaidQuestTracker>()?.CommitExtraction();
            SceneLoader.Load(shelterScene, "Убежище");
        }

        public void Abandon(bool toMainMenu)
        {
            if (!CanPause || SceneLoader.IsLoading) return;
            ending = true;
            if (persistence != null) persistence.LoseEverything();
            SceneLoader.Load(toMainMenu ? SceneLoader.MainMenu : shelterScene, toMainMenu ? "Главное меню" : "Убежище");
        }

        private void OnDied()
        {
            if (ending) return;
            ending = true;
            if (persistence != null) persistence.LoseEverything();
            ConfirmDialog.Notice(
                "Вы погибли",
                "Всё, что было при вас, потеряно. Вы очнулись в убежище.",
                "В убежище",
                () => SceneLoader.Load(shelterScene, "Убежище"));
        }
    }
}
