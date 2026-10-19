using ProjectMayham.Core;
using ProjectMayham.Interaction;
using ProjectMayham.UI;
using ProjectMayham.World;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// A plan of the floor on a corridor wall. Studied for a few seconds (the Interact key held), it tells which way
    /// the exit of the raid lies: one of eight points of the compass, north being up, the short way round the wrapped
    /// maze. Put on corridor walls by <see cref="RaidGenerator"/>.
    /// </summary>
    public class WallMap : MonoBehaviour, IHoldInteractable
    {
        [Tooltip("Seconds the Interact key is held to read the map.")]
        [SerializeField, Min(0.5f)] private float studySeconds = 5f;
        [Tooltip("Seconds the answer stays on the screen.")]
        [SerializeField, Min(1f)] private float noteSeconds = 5f;

        public string Prompt => "Изучить карту";
        public Vector2 Position => transform.position;
        public float HoldSeconds => studySeconds;
        public string HoldLabel => "Изучаю карту";

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        public bool CanInteract(GameObject actor) => !ModalState.IsOpen;

        public void Interact(GameObject actor)
        {
            var exit = RaidExit.Active;
            ScreenNote.Show(Reading(transform.position, exit != null ? exit.transform.position : (Vector2?)null), noteSeconds);
        }

        /// <summary>What a map at <paramref name="from"/> says about the exit: "Выход — на северо-востоке".</summary>
        public static string Reading(Vector2 from, Vector2? exit)
        {
            if (!exit.HasValue) return "Выхода на карте нет";
            var wrap = WrapWorld.Active;
            Vector2 delta = wrap != null ? wrap.Delta(from, exit.Value) : exit.Value - from;
            return $"Выход — {CompassText.Describe(delta)}";
        }
    }
}
