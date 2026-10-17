using System.Linq;
using ProjectMayham.Items;
using ProjectMayham.Player;
using UnityEngine;

namespace ProjectMayham.Core
{
    /// <summary>
    /// Put on the player (in the shelter and in the raid): fills the inventory, what is worn and health from the
    /// game session when the scene starts and writes them back whenever the game is saved, so the player carries
    /// their things from the shelter into the raid and back.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(GridInventory), typeof(PlayerStats))]
    public class PlayerPersistence : MonoBehaviour
    {
        private GridInventory inventory;
        private PlayerEquipment equipment;
        private PlayerStats stats;

        private void Awake()
        {
            inventory = GetComponent<GridInventory>();
            stats = GetComponent<PlayerStats>();
            // Added here when the player has none, so that no scene has to be changed for it.
            equipment = GetComponent<PlayerEquipment>();
            if (equipment == null) equipment = gameObject.AddComponent<PlayerEquipment>();
        }

        private void OnEnable() => GameSession.SaveRequested += Export;
        private void OnDisable() => GameSession.SaveRequested -= Export;

        private void Start()
        {
            var data = GameSession.Data;
            // What is worn first: the backpack decides how big the grid is that the things are put into.
            equipment.Import(data.player);
            inventory.Import(data.player);
            if (data.health > 0f) stats.RestoreHealth(data.health);
        }

        private void Export()
        {
            var data = GameSession.Data;
            data.player = inventory.Export();
            equipment.Export(data.player);
            // After dying the player comes round in the shelter, healed.
            data.health = stats.IsDead ? -1f : stats.Health;
        }

        /// <summary>The player died in a raid: they lose everything they carried.</summary>
        public void LoseEverything()
        {
            inventory.Clear();
            equipment.Clear();

            // What was lost took its wear with it. Weapons lying in the stash keep theirs.
            var stash = GameSession.Data.stash;
            var owned = stash.stacks.Select(s => s.item).Append(stash.held).Where(id => !string.IsNullOrEmpty(id));
            Combat.WeaponWear.ForgetExcept(owned);
        }
    }
}
