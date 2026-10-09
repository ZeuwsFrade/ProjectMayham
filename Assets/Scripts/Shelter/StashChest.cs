using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.UI;
using UnityEngine;

namespace ProjectMayham.Shelter
{
    /// <summary>
    /// The stash where supplies wait between raids: a 10x10 grid kept in the save file. E opens it next to the
    /// player's inventory. Needs a <see cref="GridInventory"/> on the same object (set to 10x10 with no practical weight limit).
    /// </summary>
    [RequireComponent(typeof(GridInventory))]
    public class StashChest : ShelterInteractable
    {
        [SerializeField] private string title = "Склад";

        private GridInventory stash;

        public GridInventory Inventory => stash;
        public override string Prompt => $"Открыть: {title}";

        private void Awake() => stash = GetComponent<GridInventory>();

        protected override void OnEnable()
        {
            base.OnEnable();
            GameSession.SaveRequested += Export;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            GameSession.SaveRequested -= Export;
        }

        private void Start() => stash.Import(GameSession.Data.stash);

        private void Export() => GameSession.Data.stash = stash.Export();

        public override void Interact(GameObject actor)
        {
            if (actor.TryGetComponent<GridInventory>(out var player)) TransferWindow.Open(player, stash, title);
        }
    }
}
