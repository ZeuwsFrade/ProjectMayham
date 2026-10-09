using ProjectMayham.Items;
using ProjectMayham.Player;
using UnityEngine;

namespace ProjectMayham.Core
{
    /// <summary>
    /// Put on the player (in the shelter and in the raid): fills the inventory and health from the game session
    /// when the scene starts and writes them back whenever the game is saved, so the player carries
    /// their things from the shelter into the raid and back.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(GridInventory), typeof(PlayerStats))]
    public class PlayerPersistence : MonoBehaviour
    {
        private GridInventory inventory;
        private PlayerStats stats;

        private void Awake()
        {
            inventory = GetComponent<GridInventory>();
            stats = GetComponent<PlayerStats>();
        }

        private void OnEnable() => GameSession.SaveRequested += Export;
        private void OnDisable() => GameSession.SaveRequested -= Export;

        private void Start()
        {
            var data = GameSession.Data;
            inventory.Import(data.player);
            if (data.health > 0f) stats.RestoreHealth(data.health);
        }

        private void Export()
        {
            var data = GameSession.Data;
            data.player = inventory.Export();
            // After dying the player comes round in the shelter, healed.
            data.health = stats.IsDead ? -1f : stats.Health;
        }

        /// <summary>The player died in a raid: they lose everything they carried.</summary>
        public void LoseEverything() => inventory.Clear();
    }
}
