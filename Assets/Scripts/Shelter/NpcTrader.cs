using ProjectMayham.Dialogue;
using ProjectMayham.Items;
using ProjectMayham.Quests;
using ProjectMayham.Trade;
using UnityEngine;

namespace ProjectMayham.Shelter
{
    /// <summary>
    /// The trader / quest giver of the shelter (Tyler). E starts a dialogue; from it the player can trade
    /// (buy, sell, barter) or ask for work. The quest part is a stub, see <see cref="QuestService"/>.
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
            var work = new DialogueNode();
            var quests = QuestService.GetAvailable(trader.Id);
            work.Text = quests.Count == 0
                ? "Работы пока нет, [ИГРОК]. Загляни позже. (Квестовая система — заглушка)"
                : "Есть пара дел.";
            work.Choices.Add(new DialogueChoice { Text = "Понятно", Next = root });

            root.Choices.Add(new DialogueChoice { Text = "Давай посмотрим товары", Action = () => TradeWindow.Open(book, inventory) });
            root.Choices.Add(new DialogueChoice { Text = "Есть для меня работа?", Next = work });
            root.Choices.Add(new DialogueChoice { Text = "Я пойду", Closes = true });

            var portrait = trader.Portrait;
            if (portrait == null && TryGetComponent<SpriteRenderer>(out var body)) portrait = body.sprite;
            DialogueWindow.Open(trader.DisplayName, portrait, root);
        }
    }
}
