using ProjectMayham.Localization;
using ProjectMayham.Core;
using ProjectMayham.Interaction;
using ProjectMayham.UI;
using ProjectMayham.Player;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// A corridor sign that unlocks the exit guide for this raid.
    /// </summary>
    public class WallMap : MonoBehaviour, IInteractable
    {
        public string Prompt => GameText.Translate("Прочитать указатель выхода");
        public Vector2 Position => transform.position;

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        public bool CanInteract(GameObject actor) => !ModalState.IsOpen;

        public void Interact(GameObject actor)
        {
            var exit = RaidExit.Active;
            if (exit == null)
            {
                ScreenNote.Show("Выхода на карте нет", 5f);
                return;
            }

            var player = actor != null ? actor.GetComponentInParent<PlayerStats>() : null;
            if (player == null || player.IsDead) return;
            if (!player.TryGetComponent<RaidExitGuide>(out var guide)) guide = player.gameObject.AddComponent<RaidExitGuide>();
            guide.ReadSign(exit);
        }

        /// <summary>What a map at <paramref name="from"/> says about the exit: "Выход — на северо-востоке".</summary>
        public static string Reading(Vector2 from, Vector2? exit)
        {
            if (!exit.HasValue) return "Выхода на карте нет";
            Vector2 delta = RaidExitGuide.Direction(from, exit.Value);
            return GameText.Format("Выход — {0}", CompassText.Describe(delta));
        }
    }
}
