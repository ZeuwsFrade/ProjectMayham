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
    /// walk. E talks to them: their dialogue, their quests, loyalty. When their dialogue lets them (their rescue is
    /// open) they go with the player as a heavy thing in the grid or in the hands; put down (G) they sit where they
    /// are put. When an enemy comes near they hide: fade away where they sit, cannot be talked to or bumped into,
    /// and come back once no enemy has been near for a while.
    /// </summary>
    public class LostNpc : MonoBehaviour, IInteractable
    {
        private const float CheckEvery = 0.25f;
        // Seconds it takes to fade away or back.
        private const float FadeTime = 0.3f;

        [SerializeField] private NpcDefinition npc;

        private readonly NpcHiding hiding = new NpcHiding();
        private SpriteRenderer[] bodies;
        private Collider2D[] colliders;
        private float sinceCheck;
        private float alpha = 1f;

        public NpcDefinition Npc => npc;
        public string Prompt => npc != null ? $"Поговорить: {npc.DisplayName}" : string.Empty;
        public Vector2 Position => transform.position;
        /// <summary>True while the NPC hides from an enemy.</summary>
        public bool Hidden => hiding.Hidden;

        private void Awake()
        {
            bodies = GetComponentsInChildren<SpriteRenderer>(true);
            colliders = GetComponentsInChildren<Collider2D>(true);
        }

        private void OnEnable()
        {
            if (!hiding.Hidden) Interactables.Register(this);
        }

        private void OnDisable() => Interactables.Unregister(this);

        public bool CanInteract(GameObject actor) => npc != null && !hiding.Hidden && !ModalState.IsOpen;

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
        /// Puts a person into the grid when there is room and the weight allows it, else into both hands (what they
        /// hold goes into the grid, else on the floor). Returns null when it is done, or what keeps the player from
        /// taking them.
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
            if (npc == null) return;
            Fade();

            sinceCheck += Time.deltaTime;
            if (sinceCheck < CheckEvery) return;
            bool changed = hiding.Step(EnemyNear(), sinceCheck, npc.HideFor);
            sinceCheck = 0f;
            if (!changed) return;

            // Hidden: nobody can talk to them or walk into them; back: both again.
            foreach (var collider in colliders) collider.enabled = !hiding.Hidden;
            if (hiding.Hidden) Interactables.Unregister(this);
            else Interactables.Register(this);
        }

        // The body fades away while hiding and back after.
        private void Fade()
        {
            float target = hiding.Hidden ? 0f : 1f;
            if (Mathf.Approximately(alpha, target)) return;
            alpha = Mathf.MoveTowards(alpha, target, Time.deltaTime / FadeTime);
            foreach (var body in bodies)
            {
                var color = body.color;
                color.a = alpha;
                body.color = color;
            }
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
                if (delta.sqrMagnitude < npc.HideDistance * npc.HideDistance) return true;
            }
            return false;
        }
    }
}
