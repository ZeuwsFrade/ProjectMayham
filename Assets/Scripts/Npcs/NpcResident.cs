using ProjectMayham.Core;
using ProjectMayham.Interaction;
using ProjectMayham.Items;
using ProjectMayham.Trade;
using UnityEngine;

namespace ProjectMayham.Npcs
{
    /// <summary>An NPC living in the shelter after the player brought them home: talks, gives quests, trades.</summary>
    public class NpcResident : MonoBehaviour, IInteractable
    {
        [SerializeField] private NpcDefinition npc;

        private TraderBook book;

        public NpcDefinition Npc => npc;
        public string Prompt => npc != null ? $"Поговорить: {npc.DisplayName}" : string.Empty;
        public Vector2 Position => transform.position;

        private void Awake()
        {
            if (npc != null && npc.Trader != null) book = new TraderBook(npc.Trader);
        }

        private void OnDestroy() => book?.Dispose();
        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        public bool CanInteract(GameObject actor) => npc != null && !ModalState.IsOpen;

        public void Interact(GameObject actor)
        {
            if (npc == null || !actor.TryGetComponent<GridInventory>(out var backpack)) return;
            var body = GetComponentInChildren<SpriteRenderer>();
            NpcConversation.Open(new ConversationContext { Npc = npc, Backpack = backpack, Book = book }, body != null ? body.sprite : null);
        }
    }
}
