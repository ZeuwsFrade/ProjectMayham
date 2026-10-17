using ProjectMayham.AI;
using ProjectMayham.Core;
using ProjectMayham.Interaction;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.World;
using UnityEngine;

namespace ProjectMayham.Npcs
{
    /// <summary>
    /// An NPC lost in the maze (<see cref="NpcDefinition.LostInMaze"/>): sits where the raid put them and does not
    /// walk. E talks to them: their dialogue, their quests, loyalty. From the loyalty level of their definition they
    /// go with the player as a heavy thing in the grid or in the hands; put down (G) they sit where they are put.
    /// When an enemy comes near they slip away and turn up somewhere else, out of the player's sight.
    /// </summary>
    public class LostNpc : MonoBehaviour, IInteractable
    {
        private const float CheckEvery = 0.5f;

        [SerializeField] private NpcDefinition npc;

        private float nextCheck;

        public NpcDefinition Npc => npc;
        public string Prompt => npc != null ? $"Поговорить: {npc.DisplayName}" : string.Empty;
        public Vector2 Position => transform.position;

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        public bool CanInteract(GameObject actor) => npc != null && !ModalState.IsOpen;

        public void Interact(GameObject actor)
        {
            if (npc == null || !actor.TryGetComponent<GridInventory>(out var backpack)) return;

            var body = GetComponentInChildren<SpriteRenderer>();
            var context = new ConversationContext { Npc = npc, Backpack = backpack, InRaid = true };
            context.TakeAlong = () => TakeAlong(backpack);
            NpcConversation.Open(context, body != null ? body.sprite : null);
        }

        private string TakeAlong(GridInventory backpack)
        {
            if (Loyalty.Level(npc.Id) < npc.TakeAlongLevel) return "Я тебя почти не знаю. Помоги мне сначала — тогда поговорим.";

            string refusal = TryCarry(backpack, npc.CarryItem);
            if (refusal != null) return refusal;
            NpcRescue.StartRescue(npc.Id);
            Destroy(gameObject);
            return null;
        }

        /// <summary>
        /// Puts a person into the grid when there is room and the weight allows it, else into empty hands. Returns
        /// null when it is done, or what keeps the player from taking them.
        /// </summary>
        public static string TryCarry(GridInventory backpack, ItemDefinition person)
        {
            if (backpack == null || person == null) return "Не получится.";
            if (!backpack.FitsByWeight(person))
                return $"Слишком тяжело: это ещё {person.Weight:0.#} кг. Освободись от лишнего.";
            if (backpack.CanFit(person, 1))
            {
                backpack.Add(person, 1);
                return null;
            }
            if (backpack.HoldNew(person)) return null;
            return $"Некуда взять: нужно место {person.GridSize.x}×{person.GridSize.y} в рюкзаке или свободные руки.";
        }

        private void Update()
        {
            if (npc == null || Time.time < nextCheck) return;
            nextCheck = Time.time + CheckEvery;
            if (!EnemyNear()) return;

            var generator = FindFirstObjectByType<RaidGenerator>();
            if (generator != null) generator.Relocate(this);
        }

        private bool EnemyNear()
        {
            Vector2 here = transform.position;
            var wrap = WrapWorld.Active;
            foreach (var enemy in EnemyAI.All)
            {
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                Vector2 there = enemy.transform.position;
                Vector2 delta = wrap != null ? wrap.Delta(here, there) : there - here;
                if (delta.sqrMagnitude < npc.FleeDistance * npc.FleeDistance) return true;
            }
            return false;
        }
    }
}
