using ProjectMayham.Player;
using UnityEngine;

namespace ProjectMayham.Shelter
{
    /// <summary>The bed: E fades the screen out and restores the player's health (and stamina).</summary>
    public class Bed : ShelterInteractable
    {
        public override string Prompt => "Лечь спать";

        public override void Interact(GameObject actor)
        {
            actor.TryGetComponent<PlayerStats>(out var stats);
            SleepScreen.Sleep(stats);
        }
    }
}
