using ProjectMayham.Combat;
using ProjectMayham.Core;
using ProjectMayham.Player;
using ProjectMayham.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.Level
{
    /// <summary>
    /// Rules of leaving a raid. The way out is the exit of the raid (<see cref="RaidExit"/>): it asks whether to go
    /// back to the shelter, the player keeps what they carry. Esc asks the same from anywhere only in test scenes
    /// (<see cref="leaveAnywhere"/>) or in a raid that has no exit. Dying costs everything in the inventory and also
    /// ends in the shelter.
    /// </summary>
    public class RaidController : MonoBehaviour
    {
        [SerializeField] private PlayerStats stats;
        [SerializeField] private PlayerPersistence persistence;
        [Tooltip("Esc closes this window first; it only asks about leaving when the window was not open.")]
        [SerializeField] private InventoryWindow inventoryWindow;
        [Tooltip("Scene the raid ends in: the shelter (a test raid names its test shelter here).")]
        [SerializeField] private string shelterScene = SceneLoader.Shelter;
        [Tooltip("For test scenes: Esc leaves the raid from anywhere. Off: only through the exit (Esc still leaves a raid that has no exit).")]
        [SerializeField] private bool leaveAnywhere;

        private bool inventoryWasOpen;
        private bool dying;
        // The raid ends when the death animation has played; a player without one ends it at the moment of death.
        private PlayerDeath death;

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

        private void Update()
        {
            bool inventoryOpen = inventoryWindow != null && inventoryWindow.IsOpen;
            var keyboard = Keyboard.current;

            // The inventory may have used this Esc press already, then it was open a frame ago.
            bool free = !inventoryOpen && !inventoryWasOpen && !ModalState.IsOpen && !ModalWindow.EscapeConsumed
                        && !SceneLoader.IsLoading && !dying && (death == null || !death.IsDying);
            if (free && CanLeaveByEscape(leaveAnywhere, RaidExit.Active != null) && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                AskToLeave();

            inventoryWasOpen = inventoryOpen;
        }

        /// <summary>Esc leaves the raid in a test scene, or when there is no exit to leave through.</summary>
        public static bool CanLeaveByEscape(bool leaveAnywhere, bool hasExit) => leaveAnywhere || !hasExit;

        /// <summary>Asks whether to go back to the shelter; yes ends the raid with what the player carries.</summary>
        public void AskToLeave()
        {
            ConfirmDialog.Ask(
                "Вернуться в убежище?",
                "Вы покинете рейд. Всё, что в рюкзаке, останется при вас.",
                () => SceneLoader.Load(shelterScene, "Убежище"),
                yesText: "Вернуться", noText: "Остаться");
        }

        private void OnDied()
        {
            dying = true;
            if (persistence != null) persistence.LoseEverything();
            ConfirmDialog.Notice(
                "Вы погибли",
                "Всё, что было при вас, потеряно. Вы очнулись в убежище.",
                "В убежище",
                () => SceneLoader.Load(shelterScene, "Убежище"));
        }
    }
}
