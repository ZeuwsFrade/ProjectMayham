using ProjectMayham.Core;
using ProjectMayham.Player;
using ProjectMayham.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.Level
{
    /// <summary>
    /// Rules of leaving a raid. Esc asks whether to go back to the shelter (the player keeps what they carry);
    /// dying costs everything in the inventory and also ends in the shelter.
    /// </summary>
    public class RaidController : MonoBehaviour
    {
        [SerializeField] private PlayerStats stats;
        [SerializeField] private PlayerPersistence persistence;
        [Tooltip("Esc closes this window first; it only asks about leaving when the window was not open.")]
        [SerializeField] private InventoryWindow inventoryWindow;

        private bool inventoryWasOpen;
        private bool dying;

        private void OnEnable()
        {
            if (stats != null) stats.Died += OnDied;
        }

        private void OnDisable()
        {
            if (stats != null) stats.Died -= OnDied;
        }

        private void Update()
        {
            bool inventoryOpen = inventoryWindow != null && inventoryWindow.IsOpen;
            var keyboard = Keyboard.current;

            // The inventory may have used this Esc press already, then it was open a frame ago.
            bool free = !inventoryOpen && !inventoryWasOpen && !ModalState.IsOpen && !ModalWindow.EscapeConsumed
                        && !SceneLoader.IsLoading && !dying;
            if (free && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) AskToLeave();

            inventoryWasOpen = inventoryOpen;
        }

        private static void AskToLeave()
        {
            ConfirmDialog.Ask(
                "Вернуться в убежище?",
                "Вы покинете рейд. Всё, что в рюкзаке, останется при вас.",
                () => SceneLoader.Load(SceneLoader.Shelter, "Убежище"),
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
                () => SceneLoader.Load(SceneLoader.Shelter, "Убежище"));
        }
    }
}
