using ProjectMayham.Dialogue;
using ProjectMayham.Items;
using ProjectMayham.Quests;
using ProjectMayham.Trade;
using UnityEngine;

namespace ProjectMayham.Shelter
{
    /// <summary>
    /// The trader / quest giver of the shelter (Tyler). E starts a dialogue; from it the player can trade
    /// (buy, sell, barter) or ask for work; the talk about work is built by <see cref="QuestDialogue"/>.
    /// </summary>
    public class NpcTrader : ShelterInteractable
    {
        [SerializeField] private TraderDefinition trader;

        private TraderBook book;

        public override string Prompt => $"Поговорить: {trader.DisplayName}";

        private void Awake() => book = new TraderBook(trader);

        private void OnDestroy() => book?.Dispose();

        public override void Interact(GameObject actor)
        {
            if (!actor.TryGetComponent<GridInventory>(out var inventory)) return;

            var root = new DialogueNode { Text = trader.Greeting };

            root.Choices.Add(new DialogueChoice { Text = "Давай посмотрим товары", Action = () => TradeWindow.Open(book, inventory) });
            root.Choices.Add(QuestDialogue.WorkChoice(trader.Id, inventory, root));
            root.Choices.Add(new DialogueChoice { Text = "Я пойду", Closes = true });

            var portrait = trader.Portrait;
            if (portrait == null && TryGetComponent<SpriteRenderer>(out var body)) portrait = body.sprite;
            DialogueWindow.Open(trader.DisplayName, portrait, root);
        }
    }
}
