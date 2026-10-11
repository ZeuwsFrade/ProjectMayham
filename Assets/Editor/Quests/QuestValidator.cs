using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Quests;
using ProjectMayham.Trade;
using ProjectMayham.World;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Checks the quest assets for the mistakes that are easy to make in the Inspector: Mayham > Quests >
    /// Validate Quests. The messages are for the writer and therefore in Russian. Errors break the game or let
    /// the player cheat; warnings only look wrong.
    /// </summary>
    public static class QuestValidator
    {
        public class Problem
        {
            public bool IsError;
            public UnityEngine.Object Asset;
            public string Message;
        }

        /// <summary>What the checks need to know about the rest of the project.</summary>
        public class Facts
        {
            public readonly HashSet<ItemDefinition> DatabaseItems = new HashSet<ItemDefinition>();
            public readonly HashSet<ItemDefinition> LootItems = new HashSet<ItemDefinition>();
            /// <summary>Items a trader sells, or takes or gives in a barter.</summary>
            public readonly HashSet<ItemDefinition> TraderItems = new HashSet<ItemDefinition>();
            /// <summary>Ids of the NPCs that can give quests: for now the traders.</summary>
            public readonly HashSet<string> NpcIds = new HashSet<string>();
            public RoomLibrary Library;
            public Func<UnityEngine.Object, string> PathOf = AssetDatabase.GetAssetPath;

            public static Facts FromProject()
            {
                var facts = new Facts();
                foreach (var database in LoadAll<ItemDatabase>())
                {
                    foreach (var item in database.Items)
                    {
                        if (item != null) facts.DatabaseItems.Add(item);
                    }
                }
                foreach (var table in LoadAll<LootTable>())
                {
                    foreach (var entry in table.Entries)
                    {
                        if (entry.item != null) facts.LootItems.Add(entry.item);
                    }
                }
                foreach (var trader in LoadAll<TraderDefinition>())
                {
                    facts.NpcIds.Add(trader.Id);
                    foreach (var entry in trader.Stock)
                    {
                        if (entry.item != null) facts.TraderItems.Add(entry.item);
                    }
                    foreach (var offer in trader.Barter)
                    {
                        foreach (var amount in offer.playerGives.Concat(offer.playerReceives))
                        {
                            if (amount != null && amount.item != null) facts.TraderItems.Add(amount.item);
                        }
                    }
                }
                facts.Library = AssetDatabase.LoadAssetAtPath<RoomLibrary>(RoomFactory.LibraryPath);
                return facts;
            }
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null).ToList();

        [MenuItem("Mayham/Quests/Validate Quests")]
        public static void ValidateMenu() => Debug.Log(Run());

        /// <summary>Checks every quest of the project, logs what is wrong (click a line to find the asset) and returns the totals.</summary>
        public static string Run()
        {
            var quests = LoadAll<QuestDefinition>();
            var problems = Check(quests, Facts.FromProject());
            foreach (var problem in problems)
            {
                if (problem.IsError) Debug.LogError(problem.Message, problem.Asset);
                else Debug.LogWarning(problem.Message, problem.Asset);
            }
            int errors = problems.Count(p => p.IsError);
            return $"Квесты: {quests.Count}, ошибок: {errors}, предупреждений: {problems.Count - errors}";
        }

        public static List<Problem> Check(IReadOnlyList<QuestDefinition> quests, Facts facts)
        {
            var problems = new List<Problem>();
            void Error(QuestDefinition quest, string message) =>
                problems.Add(new Problem { IsError = true, Asset = quest, Message = $"{quest.name}: {message}" });
            void Warning(QuestDefinition quest, string message) =>
                problems.Add(new Problem { IsError = false, Asset = quest, Message = $"{quest.name}: {message}" });

            foreach (var group in quests.GroupBy(q => q.Id).Where(g => g.Count() > 1))
            {
                foreach (var quest in group) Error(quest, "есть другой квест с таким же именем — в сейве они неразличимы");
            }

            foreach (var quest in quests)
            {
                string path = (facts.PathOf(quest) ?? string.Empty).Replace('\\', '/');
                if (!path.Contains("/Resources/Quests/")) Error(quest, "лежит вне Assets/Resources/Quests — игра его не найдёт");
                if (quest.Giver == null) Error(quest, "не указан выдающий (Giver)");

                CheckObjectives(quest, facts, Error, Warning);
                CheckReward(quest, facts, Error, Warning);
                CheckRequires(quest, Error);
                CheckTexts(quest, Warning);
            }

            foreach (var giver in quests.Select(q => q.Giver).Where(g => g != null).Distinct())
            {
                CheckGiver(giver, facts, message =>
                    problems.Add(new Problem { IsError = false, Asset = giver, Message = $"{giver.name}: {message}" }));
            }
            return problems;
        }

        // A giver is found by its name: the NPC asks for the quests of its own id. And what the giver says itself
        // has to fit the window like everything else; the two lines shown above answers cannot be paged at all.
        private static void CheckGiver(QuestGiverDefinition giver, Facts facts, Action<string> warning)
        {
            if (facts.NpcIds.Count > 0 && !facts.NpcIds.Contains(giver.Id))
                warning("такого NPC нет — имя ассета выдающего должно совпадать с именем ассета торговца, иначе его квесты никто не предложит");

            if (QuestText.Pages(giver.MenuText).Count > 1) warning("фраза над списком дел не помещается на одну страницу — окно её обрежет");
            if (QuestText.Pages(giver.NoWorkText).Count > 1) warning("фраза «работы нет» не помещается на одну страницу — окно её обрежет");

            var texts = new[]
            {
                ("договорились", giver.AcceptedText), ("напоминание", giver.ReminderText),
                ("всё готово", giver.ReadyText), ("нет места", giver.NoRoomText)
            };
            foreach (var (label, text) in texts)
            {
                foreach (string problem in PageProblems(text)) warning($"фраза «{label}»: {problem}");
            }
        }

        // What is wrong with the pages of a text as the writer marked them: too long, or too many lines.
        private static IEnumerable<string> PageProblems(string text)
        {
            var blocks = QuestText.Blocks(text);
            int tooLong = blocks.Count(page => page.Length > QuestText.PageLimit);
            if (tooLong > 0)
                yield return $"страниц длиннее {QuestText.PageLimit} символов — {tooLong}; игра разрежет их сама, лучше разделить пустой строкой";

            int tooManyLines = blocks.Count(page => QuestText.LineCount(page) > QuestText.PageLines);
            if (tooManyLines > 0)
                yield return $"страниц длиннее {QuestText.PageLines} строк — {tooManyLines}; игра разрежет их сама, лучше разделить пустой строкой";
        }

        private static void CheckObjectives(QuestDefinition quest, Facts facts,
            Action<QuestDefinition, string> error, Action<QuestDefinition, string> warning)
        {
            if (quest.Objectives.Count == 0) warning(quest, "нет целей");

            var seen = new HashSet<ItemDefinition>();
            foreach (var objective in quest.Objectives)
            {
                if (objective == null) continue;

                if (objective.NeedsItem)
                {
                    var item = objective.Item;
                    if (item == null)
                    {
                        error(quest, "у цели не указан предмет");
                        continue;
                    }
                    if (!seen.Add(item)) error(quest, $"предмет «{item.DisplayName}» указан в двух целях — объедините их в одну");
                    if (!facts.DatabaseItems.Contains(item))
                        error(quest, $"предмета «{item.DisplayName}» нет в базе предметов (Mayham > Refresh Item Database)");

                    if (objective.Kind == QuestObjectiveKind.FindSpecial) CheckSpecial(quest, item, facts, error);
                    else if (!facts.LootItems.Contains(item) && !facts.TraderItems.Contains(item))
                        warning(quest, $"предмет «{item.DisplayName}» не встречается ни в луте, ни у торговца — его негде взять");
                }

                if (objective.Kind != QuestObjectiveKind.Deliver) CheckRoom(quest, objective, facts, error);
            }
        }

        // A special item must exist only because of its quest: no price (or it can be sold and found again),
        // not in loot, not with a trader, and with a pickup the generator can put into a room.
        private static void CheckSpecial(QuestDefinition quest, ItemDefinition item, Facts facts, Action<QuestDefinition, string> error)
        {
            string name = item.DisplayName;
            if (item.Value > 0)
                error(quest, $"особый предмет «{name}» имеет цену {item.Value} — поставьте 0, иначе его можно продать и найти второй");
            if (facts.LootItems.Contains(item))
                error(quest, $"особый предмет «{name}» есть в таблице лута — он должен появляться только по квесту");
            if (facts.TraderItems.Contains(item))
                error(quest, $"особый предмет «{name}» есть у торговца (товары или бартер)");

            var prefab = item.WorldPrefab;
            if (prefab == null) error(quest, $"у особого предмета «{name}» нет префаба (World Prefab)");
            else if (prefab.GetComponent<ItemPickup>() == null || prefab.GetComponent<WrapEntity>() == null)
                error(quest, $"на префабе предмета «{name}» нет ItemPickup или WrapEntity");
        }

        private static void CheckRoom(QuestDefinition quest, QuestObjective objective, Facts facts, Action<QuestDefinition, string> error)
        {
            if (facts.Library == null) return;

            var kind = objective.Room;
            var sizes = (RoomSize[])Enum.GetValues(typeof(RoomSize));
            if (!sizes.Any(size => facts.Library.CanPlace(kind, size)))
            {
                error(quest, $"для комнаты «{RoomSizes.Title(kind)}» нет ни одного префаба подходящего размера");
                return;
            }
            if (objective.Kind != QuestObjectiveKind.FindSpecial) return;

            bool noSpots = facts.Library.Rooms.Any(room =>
                room != null && room.Kind == kind && (room.LootSpots == null || room.LootSpots.childCount == 0));
            if (noSpots) error(quest, $"у префаба комнаты «{RoomSizes.Title(kind)}» нет точек лута — особый предмет некуда положить");
        }

        private static void CheckReward(QuestDefinition quest, Facts facts,
            Action<QuestDefinition, string> error, Action<QuestDefinition, string> warning)
        {
            if (quest.RewardMoney == 0 && quest.RewardItems.Count == 0) warning(quest, "нет награды");
            foreach (var reward in quest.RewardItems)
            {
                if (reward == null || reward.item == null)
                {
                    error(quest, "в награде пустой предмет");
                    continue;
                }
                if (!facts.DatabaseItems.Contains(reward.item))
                    error(quest, $"предмета награды «{reward.item.DisplayName}» нет в базе предметов (Mayham > Refresh Item Database)");
            }
        }

        private static void CheckRequires(QuestDefinition quest, Action<QuestDefinition, string> error)
        {
            bool requiresItself = false;
            foreach (var required in quest.Requires)
            {
                if (required == null) error(quest, "условие ссылается на удалённый квест (пустая строка в Requires)");
                else if (required == quest) requiresItself = true;
            }
            if (requiresItself) error(quest, "квест требует сам себя");
            else if (InCycle(quest)) error(quest, "условия образуют цикл — квест никогда не откроется");
        }

        // True when following the requirements from this quest leads back to it.
        private static bool InCycle(QuestDefinition start)
        {
            var seen = new HashSet<QuestDefinition>();
            var stack = new Stack<QuestDefinition>(start.Requires.Where(q => q != null));
            while (stack.Count > 0)
            {
                var quest = stack.Pop();
                if (quest == start) return true;
                if (!seen.Add(quest)) continue;
                foreach (var required in quest.Requires)
                {
                    if (required != null) stack.Push(required);
                }
            }
            return false;
        }

        private static void CheckTexts(QuestDefinition quest, Action<QuestDefinition, string> warning)
        {
            if (quest.Title == quest.name) warning(quest, "пустое название — на кнопке будет имя ассета");
            if (string.IsNullOrWhiteSpace(quest.OfferText)) warning(quest, "пустой текст предложения");
            if (string.IsNullOrWhiteSpace(quest.CompletedText)) warning(quest, "пустой текст благодарности");

            var texts = new[] { ("предложение", quest.OfferText), ("напоминание", quest.ReminderText), ("благодарность", quest.CompletedText) };
            foreach (var (label, text) in texts)
            {
                foreach (string problem in PageProblems(text)) warning(quest, $"{label}: {problem}");
            }
        }
    }
}
