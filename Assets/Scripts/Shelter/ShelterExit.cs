using ProjectMayham.Core;
using ProjectMayham.Player;
using ProjectMayham.UI;
using UnityEngine;

namespace ProjectMayham.Shelter
{
    /// <summary>
    /// The way out of the shelter: a trigger zone. Walking into it asks "go on a raid?"; yes loads the raid
    /// scene with the loading screen. To be asked again after "no" the player has to leave the zone and re-enter it.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ShelterExit : MonoBehaviour
    {
        [SerializeField] private string raidScene = SceneLoader.Raid;

        private void Reset() => GetComponent<Collider2D>().isTrigger = true;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (ModalState.IsOpen || SceneLoader.IsLoading) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;

            ConfirmDialog.Ask(
                "Выйти в рейд?",
                "Вы покинете убежище. Всё, что лежит в вашем рюкзаке, отправится с вами.",
                () => SceneLoader.Load(raidScene, "Рейд"),
                yesText: "Выйти", noText: "Остаться");
        }
    }
}
