using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Dialogue;
using ProjectMayham.Npcs;
using ProjectMayham.Quests;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Checks the dialogues and the NPCs of the project for what would break a conversation or never show:
    /// Mayham > Dialogue > Validate Dialogues. Errors make a conversation go wrong; warnings point at what is
    /// probably a mistake (a line nobody leads to, a choice no answer remembers).
    /// </summary>
    public static class DialogueValidator
    {
        [MenuItem("Mayham/Dialogue/Validate Dialogues")]
        public static void ValidateMenu() => Debug.Log(Run());

        public static string Run()
        {
            var problems = Check(LoadAll<DialogueDefinition>(), LoadAll<NpcDefinition>(), LoadAll<QuestDefinition>());
            foreach (var problem in problems)
            {
                if (problem.IsError) Debug.LogError(problem.Message, problem.Asset);
                else Debug.LogWarning(problem.Message, problem.Asset);
            }
            int errors = problems.Count(p => p.IsError);
            return $"Диалоги: ошибок {errors}, предупреждений {problems.Count - errors}";
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null).ToList();

        public static List<QuestValidator.Problem> Check(IReadOnlyList<DialogueDefinition> dialogues, IReadOnlyList<NpcDefinition> npcs,
            IReadOnlyList<QuestDefinition> quests)
        {
            var problems = new List<QuestValidator.Problem>();
            void Report(UnityEngine.Object asset, bool error, string message) =>
                problems.Add(new QuestValidator.Problem { IsError = error, Asset = asset, Message = $"{asset.name}: {message}" });

            // The choices that some answer remembers, against those that quests and conditions ask for.
            var remembered = new HashSet<string>();
            var asked = new List<(UnityEngine.Object asset, string choice)>();

            foreach (var dialogue in dialogues)
            {
                var owners = npcs.Where(n => n.Dialogue == dialogue).ToList();
                CheckDialogue(dialogue, owners, Report, remembered, asked);
            }

            foreach (var npc in npcs)
            {
                var levels = npc.LoyaltyLevels;
                for (int i = 1; i < levels.Length; i++)
                {
                    if (levels[i] <= levels[i - 1]) Report(npc, true, "пороги лояльности должны расти: каждый уровень дороже предыдущего");
                }
                if (levels.Length != Loyalty.MaxLevel - 1) Report(npc, false, $"порогов лояльности {levels.Length}, а уровней выше первого {Loyalty.MaxLevel - 1}");
                if (npc.LostInMaze)
                {
                    if (npc.RaidPrefab == null || npc.RaidPrefab.GetComponent<LostNpc>() == null) Report(npc, true, "потерян в лабиринте, но нет префаба с LostNpc");
                    if (npc.CarryItem == null || !npc.CarryItem.IsPerson) Report(npc, true, "потерян в лабиринте, но нет вещи-человека, которой его несут");
                    else if (npc.CarryItem.WorldPrefab != npc.RaidPrefab) Report(npc, false, "вещь, которой его несут, на полу должна быть им самим (World Prefab = префаб в лабиринте)");
                    if (npc.ShelterPrefab == null || npc.ShelterPrefab.GetComponent<NpcResident>() == null) Report(npc, false, "нет префаба для убежища — спасённым его не будет видно");
                    if (!quests.Any(q => q.Giver != null && q.Giver.Id == npc.Id && IsRescue(q)))
                        Report(npc, false, "потерян в лабиринте, но нет его квеста «Довести до убежища» — спасение не попадёт в журнал");
                }
            }

            // A rescue quest is given by the one who is rescued: a lost NPC.
            var lost = new HashSet<string>(npcs.Where(n => n.LostInMaze).Select(n => n.Id));
            foreach (var quest in quests.Where(IsRescue))
            {
                if (quest.Giver == null || !lost.Contains(quest.Giver.Id))
                    Report(quest, true, "цель «Довести до убежища», но тот, кто даёт квест, не потерян в лабиринте");
                else if (quests.Count(q => q.Giver == quest.Giver && IsRescue(q)) > 1)
                    Report(quest, false, "у этого НПС несколько квестов «Довести до убежища» — сдадутся все сразу");
            }

            foreach (var quest in quests)
            {
                foreach (string choice in quest.RequiredChoices.Concat(quest.ExcludingChoices))
                {
                    if (string.IsNullOrWhiteSpace(choice)) Report(quest, true, "пустой выбор в условиях");
                    else asked.Add((quest, choice.Trim()));
                }
            }

            foreach (var (asset, choice) in asked)
            {
                if (!remembered.Contains(choice))
                    Report(asset, false, $"выбор «{choice}» не запоминает ни один ответ в диалогах — опечатка или ответ ещё не написан");
            }
            return problems;
        }

        private static bool IsRescue(QuestDefinition quest) => quest.Objectives.Any(o => o != null && o.Kind == QuestObjectiveKind.Rescue);

        private static void CheckDialogue(DialogueDefinition dialogue, List<NpcDefinition> owners,
            Action<UnityEngine.Object, bool, string> report, HashSet<string> remembered, List<(UnityEngine.Object, string)> asked)
        {
            var lines = dialogue.Lines.Where(l => l != null).ToList();
            if (lines.Count == 0)
            {
                report(dialogue, true, "в диалоге нет реплик");
                return;
            }
            if (!lines.Any(l => l.Start)) report(dialogue, false, "ни одна реплика не отмечена как начало — разговор начнётся с первой");

            var ids = new HashSet<string>();
            foreach (var line in lines)
            {
                if (line.Id.Length == 0) report(dialogue, true, "у реплики нет имени — к ней нельзя перейти");
                else if (!ids.Add(line.Id)) report(dialogue, true, $"имя реплики «{line.Id}» встречается дважды");
            }

            var reached = new HashSet<string>(lines.Where(l => l.Start).Select(l => l.Id));
            if (reached.Count == 0) reached.Add(lines[0].Id);
            bool trades = owners.Any(o => o.Trader != null);

            foreach (var line in lines)
            {
                string where = line.Id.Length > 0 ? $"реплика «{line.Id}»" : "реплика без имени";
                if (string.IsNullOrWhiteSpace(line.Text)) report(dialogue, false, $"{where}: текст пустой");
                foreach (string problem in QuestValidator.PageProblems(line.Text)) report(dialogue, false, $"{where}: {problem}");
                if (line.Answers.Count == 0) report(dialogue, false, $"{where}: нет ответов — останется только «Я пойду»");

                foreach (var condition in line.Conditions) CheckCondition(condition, $"{where}, условие начала", dialogue, report, asked);
                foreach (var answer in line.Answers)
                {
                    if (answer == null) continue;
                    string said = string.IsNullOrWhiteSpace(answer.Text) ? "ответ без текста" : $"ответ «{answer.Text}»";
                    if (string.IsNullOrWhiteSpace(answer.Text)) report(dialogue, true, $"{where}: у ответа нет текста");
                    if (answer.Next.Length > 0)
                    {
                        if (dialogue.Find(answer.Next) == null) report(dialogue, true, $"{where}, {said}: реплики «{answer.Next}» нет");
                        else reached.Add(answer.Next);
                    }
                    foreach (var condition in answer.Conditions) CheckCondition(condition, $"{where}, {said}", dialogue, report, asked);
                    foreach (var action in answer.Actions)
                    {
                        if (action == null) continue;
                        if (action.Kind == DialogueActionKind.RememberChoice)
                        {
                            if (action.Choice.Length == 0) report(dialogue, true, $"{where}, {said}: не указано, какой выбор запомнить");
                            else remembered.Add(action.Choice);
                        }
                        if (action.Kind == DialogueActionKind.OpenTrade && owners.Count > 0 && !trades)
                            report(dialogue, true, $"{where}, {said}: торговля, а у НПС нет торговца");
                        if (action.Kind == DialogueActionKind.TakeAlong && owners.Count > 0 && !owners.Any(o => o.LostInMaze && o.CarryItem != null))
                            report(dialogue, true, $"{where}, {said}: «взять с собой», а НПС не потерян в лабиринте или его не во что взять");
                    }
                }
            }

            foreach (var line in lines)
            {
                if (line.Id.Length > 0 && !reached.Contains(line.Id))
                    report(dialogue, false, $"реплика «{line.Id}»: к ней не ведёт ни один ответ и она не начало");
            }
        }

        private static void CheckCondition(DialogueCondition condition, string where, DialogueDefinition dialogue,
            Action<UnityEngine.Object, bool, string> report, List<(UnityEngine.Object, string)> asked)
        {
            if (condition == null) return;
            switch (condition.Kind)
            {
                case DialogueConditionKind.Quest:
                    if (condition.Quest == null) report(dialogue, true, $"{where}: в условии не указан квест");
                    break;
                case DialogueConditionKind.ChoiceMade:
                case DialogueConditionKind.ChoiceNotMade:
                    if (condition.Choice.Length == 0) report(dialogue, true, $"{where}: в условии не указан выбор");
                    else asked.Add((dialogue, condition.Choice));
                    break;
                case DialogueConditionKind.HasItem:
                    if (condition.Item == null) report(dialogue, true, $"{where}: в условии не указан предмет");
                    break;
            }
        }
    }
}
