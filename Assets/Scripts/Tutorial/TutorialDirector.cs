using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMayham.AI;
using ProjectMayham.Combat;
using ProjectMayham.Core;
using ProjectMayham.Interaction;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Player;
using ProjectMayham.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.Tutorial
{
    /// <summary>
    /// Runs the tutorial level: three stages, each with goals shown on screen (<see cref="TutorialHUD"/>).
    /// 1) Looking around: search the furniture for food and bandages, eat and dress the wounds; then the door to
    /// the next room opens. 2) Enemies: crouch past one Dokhodyaga, pick up a mug and throw it to lure the
    /// other away from the way out; reaching the far end is enough. 3) The world: drink at the cooler, use the
    /// toilet, crawl through a hole, climb a barricade, find the key and unlock the exit to the shelter.
    /// Each stage starts with a save, and dying loads the tutorial again at the start of the stage, with nothing lost.
    /// The needs stay where they are unless the player deals with them. Esc offers to skip the tutorial.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        // Zone ids (see TutorialZone).
        public const string StageTwoZone = "stage2";
        public const string PastGuardZone = "pastGuard";
        public const string GuardRoomZone = "guardRoom";
        public const string StageThreeZone = "stage3";
        public const string PastHoleZone = "pastHole";
        public const string PastBarricadeZone = "pastBarricade";
        public const string FinishZone = "finish";

        private class Goal
        {
            public string Text;
            public Func<bool> Check;
            public bool Done;
        }

        private class Stage
        {
            public string Title;
            public string Intro;
            public Goal[] Goals;
        }

        [Serializable]
        private struct Guard
        {
            public EnemyAI enemy;
            [Tooltip("Where it looks while standing at its post.")]
            public Vector2 facing;
        }

        [SerializeField] private PlayerStats player;
        [Tooltip("Esc closes this window first; it only offers to skip the tutorial when the window was not open.")]
        [SerializeField] private InventoryWindow inventoryWindow;
        [Tooltip("Where the player stands at the start of each of the three stages.")]
        [SerializeField] private Transform[] stageStarts = new Transform[3];
        [Tooltip("Between the first and the second room: opens when the goals of the first stage are done.")]
        [SerializeField] private Door firstGate;
        [Tooltip("The way out to the shelter, locked with the key.")]
        [SerializeField] private Door exitDoor;
        [SerializeField] private ItemDefinition bandage;
        [SerializeField] private ItemDefinition mug;
        [SerializeField] private ItemDefinition key;
        [Tooltip("The Dokhodyagi of the second stage: they stand at their posts looking one way.")]
        [SerializeField] private Guard[] guards = Array.Empty<Guard>();

        private static readonly Dictionary<string, string> ZoneHints = new Dictionary<string, string>
        {
            { PastGuardZone, "Получилось! На полу лежит кружка — подберите её (E). Брошенная вещь шумит, и враги идут на шум." },
            { GuardRoomZone, "Второй Доходяга стережёт выход. Не вставая, зажмите G и бросьте кружку в верхний угол рядом с ним.\nКогда он пойдёт на шум, крадитесь к выходу понизу." },
            { PastHoleZone, "Проход перегорожен баррикадой. Подойдите к ней и нажмите Пробел, чтобы перелезть (нужна выносливость)." },
            { PastBarricadeZone, "Дверь наружу заперта. Ключ где-то в ящиках — обыщите их." },
        };

        private Stage[] stages;
        private int stage;
        private readonly HashSet<string> reached = new HashSet<string>();
        private bool ate, bandaged, drank, relieved, thrown, exitOpened;
        private bool finished, dying, inventoryWasOpen;

        private TutorialHUD hud;
        private GridInventory inventory;
        private PlayerConsumer consumer;
        private PlayerDeath death;

        /// <summary>The tutorial running in this scene, or null.</summary>
        public static TutorialDirector Active { get; private set; }

        /// <summary>0, 1 or 2: the stage the player is in.</summary>
        public int StageIndex => stage;
        public TutorialHUD Hud => hud;

        private void Awake()
        {
            Active = this;
            stages = MakeStages();
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            WaterPoint.Used -= OnDrank;
            Toilet.Used -= OnRelieved;
            ThrownItem.Landed -= OnLanded;
            if (consumer != null) consumer.Used -= OnUsed;
            if (exitDoor != null) exitDoor.Opened -= OnExitOpened;
            if (death != null) death.OnDied -= OnDied;
            else if (player != null) player.Died -= OnDied;
        }

        private void Start()
        {
            inventory = player.GetComponent<GridInventory>();
            consumer = player.GetComponent<PlayerConsumer>();
            death = player.GetComponent<PlayerDeath>();

            // Nothing hurries the player here, and the shelter's slow recovery does not apply either.
            player.Needs.InShelter = false;
            player.Needs.Paused = true;

            WaterPoint.Used += OnDrank;
            Toilet.Used += OnRelieved;
            ThrownItem.Landed += OnLanded;
            if (consumer != null) consumer.Used += OnUsed;
            if (exitDoor != null) exitDoor.Opened += OnExitOpened;
            if (death != null) death.OnDied += OnDied;
            else player.Died += OnDied;

            foreach (var guard in guards)
                if (guard.enemy != null) guard.enemy.SetPost(guard.enemy.transform.position, guard.facing);

            hud = TutorialHUD.Create(transform);

            // A restart after a death begins where the stage began; the rooms behind are open.
            stage = Mathf.Clamp(TutorialRun.Checkpoint, 0, stages.Length - 1);
            if (stage >= 1 && firstGate != null) firstGate.Open();
            PlacePlayer(stage);
            EnterStage(stage, save: false);
        }

        private Stage[] MakeStages() => new[]
        {
            new Stage
            {
                Title = "Этап 1 из 3 · Осмотреться",
                Intro = "WASD — идти, Shift — бежать, мышь — смотреть.\nВы ранены и голодны: обыщите мебель (E) и найдите еду и бинты.",
                Goals = new[]
                {
                    new Goal { Text = "Обыщите шкафы и ящики (E): найдите еду и бинты", Check = () => (HasFood() || ate) && (Has(bandage) || bandaged) },
                    new Goal { Text = "Поешьте: I — инвентарь, наведите на еду и нажмите F", Check = () => ate },
                    new Goal { Text = "Перевяжите раны бинтами (так же, F)", Check = () => bandaged },
                },
            },
            new Stage
            {
                Title = "Этап 2 из 3 · Враги",
                Intro = "Впереди Доходяги. Зажмите Ctrl, чтобы присесть: шаги не слышны,\nа заметить вас можно только вблизи. Не заходите им в поле зрения.",
                Goals = new[]
                {
                    new Goal { Text = "Присядьте (Ctrl) и прокрадитесь за спиной Доходяги", Check = () => reached.Contains(PastGuardZone) },
                    new Goal { Text = "Подберите кружку с пола (E)", Check = () => Has(mug) || thrown },
                    new Goal { Text = "Отвлеките второго: зажмите G, прицельтесь и бросьте кружку", Check = () => thrown },
                    new Goal { Text = "Проберитесь к выходу", Check = () => reached.Contains(StageThreeZone) },
                },
            },
            new Stage
            {
                Title = "Этап 3 из 3 · Мир вокруг",
                Intro = "Здесь можно привести себя в порядок. Кулер утоляет жажду,\nтуалет снимает нужду — иначе вы замедлитесь, а враги вас учуют.",
                Goals = new[]
                {
                    new Goal { Text = "Попейте из кулера (удерживайте E)", Check = () => drank },
                    new Goal { Text = "Сходите в туалет (удерживайте E)", Check = () => relieved },
                    new Goal { Text = "Пролезьте через дыру (удерживайте E)", Check = () => reached.Contains(PastHoleZone) },
                    new Goal { Text = "Перелезьте через баррикаду (Пробел)", Check = () => reached.Contains(PastBarricadeZone) },
                    new Goal { Text = "Найдите ключ в ящиках", Check = () => Has(key) || exitOpened },
                    new Goal { Text = "Отоприте дверь и выйдите", Check = () => exitOpened },
                },
            },
        };

        private void Update()
        {
            if (finished) return;

            bool changed = false;
            foreach (var goal in stages[stage].Goals)
            {
                if (goal.Done || !goal.Check()) continue;
                goal.Done = true;
                changed = true;
            }
            if (changed) ShowGoals();

            if (stage == 0 && firstGate != null && !firstGate.IsOpen && stages[0].Goals.All(g => g.Done))
            {
                firstGate.Open();
                hud.ShowHint("Отлично! Дверь в следующую комнату открыта.");
            }

            UpdateEscape();
        }

        // ---- what the player does ----

        /// <summary>The player walked into a <see cref="TutorialZone"/>.</summary>
        public void Reached(string id)
        {
            if (finished || dying || !reached.Add(id)) return;

            switch (id)
            {
                case StageTwoZone when stage < 1: EnterStage(1, save: true); return;
                case StageThreeZone when stage < 2: EnterStage(2, save: true); return;
                case FinishZone: Complete(); return;
            }
            if (ZoneHints.TryGetValue(id, out var hint)) hud.ShowHint(hint);
        }

        private void OnUsed(ConsumableDefinition item)
        {
            if (item.Satiety > 0f) ate = true;
            if (item == bandage || item.Heal > 0f) bandaged = true;
        }

        private void OnDrank(GameObject actor)
        {
            if (actor == player.gameObject) drank = true;
        }

        private void OnRelieved(GameObject actor)
        {
            if (actor == player.gameObject) relieved = true;
        }

        private void OnLanded(ThrownItem item, Vector2 at) => thrown = true;

        private void OnExitOpened(Door door) => exitOpened = true;

        private bool HasFood()
        {
            if (inventory.HeldItem is ConsumableDefinition held && held.Satiety > 0f) return true;
            return inventory.Stacks.Any(s => !s.Sealed && s.Item is ConsumableDefinition food && food.Satiety > 0f);
        }

        private bool Has(ItemDefinition item) => item != null && (inventory.HeldItem == item || inventory.CountOf(item) > 0);

        // ---- stages ----

        private void EnterStage(int index, bool save)
        {
            stage = index;
            TutorialRun.Checkpoint = index;
            // The game is saved here, so that a death brings the player back to this point as they are now.
            if (save) GameSession.Save();
            ShowGoals();
            hud.ShowHint(stages[index].Intro, 12f);
        }

        private void ShowGoals()
        {
            var current = stages[stage];
            hud.ShowGoals(current.Title, current.Goals.Select(g => g.Text).ToList(), current.Goals.Select(g => g.Done).ToList());
        }

        private void PlacePlayer(int index)
        {
            if (stageStarts == null || index >= stageStarts.Length || stageStarts[index] == null) return;

            Vector2 at = stageStarts[index].position;
            if (player.TryGetComponent<Rigidbody2D>(out var body)) body.position = at;
            player.transform.position = at;
            var cam = Camera.main;
            if (cam != null) cam.transform.position = new Vector3(at.x, at.y, cam.transform.position.z);
        }

        private void Complete()
        {
            finished = true;
            ConfirmDialog.Notice(
                "Обучение пройдено",
                "Дальше — убежище: там можно отдохнуть, поторговать и собраться в рейд. Всё, что при вас, останется с вами.",
                "В убежище",
                TutorialRun.Finish);
        }

        private void OnDied()
        {
            dying = true;
            ConfirmDialog.Notice(
                "Вы погибли",
                "Ничего не потеряно: обучение продолжится с начала этапа.",
                "Ещё раз",
                TutorialRun.Restart);
        }

        // ---- skipping ----

        private void UpdateEscape()
        {
            bool inventoryOpen = inventoryWindow != null && inventoryWindow.IsOpen;
            var keyboard = Keyboard.current;

            // The inventory may have used this Esc press already, then it was open a frame ago (as in RaidController).
            bool free = !inventoryOpen && !inventoryWasOpen && !ModalState.IsOpen && !ModalWindow.EscapeConsumed
                        && !SceneLoader.IsLoading && !dying;
            if (free && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ConfirmDialog.Ask(
                    "Пропустить обучение?",
                    "Вы сразу попадёте в убежище. Всё, что при вас, останется с вами.",
                    TutorialRun.Finish,
                    yesText: "Пропустить", noText: "Продолжить");
            }
            inventoryWasOpen = inventoryOpen;
        }
    }
}
