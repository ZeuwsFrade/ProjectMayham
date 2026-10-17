using System.Collections.Generic;
using ProjectMayham.Dialogue;
using ProjectMayham.Items;
using ProjectMayham.Npcs;
using ProjectMayham.Quests;
using ProjectMayham.Trade;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Makes Tyler an NPC with a dialogue asset (Mayham > Dialogue > Create Tyler Dialogue): what he said before, as
    /// lines and answers, plus a friendlier greeting from loyalty 2 with a rumour about the lost intern. Gives his
    /// quests loyalty points and puts the sledgehammer behind loyalty 2. The texts are placeholders for the writer.
    /// What exists already is left as it is.
    /// </summary>
    public static class NpcSamples
    {
        public const string DialoguesFolder = "Assets/Data/Dialogues";
        public const string NpcsFolder = "Assets/Resources/Npcs";
        private const string TylerTrader = "Assets/Data/Traders/Tyler.asset";

        public class Answer
        {
            public string Text, Next;
            public List<(DialogueActionKind kind, string choice, int amount)> Actions = new List<(DialogueActionKind, string, int)>();
            public List<(DialogueConditionKind kind, int level, string choice)> Conditions = new List<(DialogueConditionKind, int, string)>();
        }

        public class Line
        {
            public string Id, Text;
            public bool Start;
            public int MinLoyalty = 1;
            public List<(DialogueConditionKind kind, int level, string choice)> Conditions = new List<(DialogueConditionKind, int, string)>();
            public List<Answer> Answers = new List<Answer>();
        }

        [MenuItem("Mayham/Dialogue/Create Tyler Dialogue")]
        public static void CreateMenu() => Debug.Log(Create());

        public static string Create()
        {
            var made = new List<string>();
            var trader = AssetDatabase.LoadAssetAtPath<TraderDefinition>(TylerTrader);
            if (trader == null) return $"no trader at {TylerTrader}";
            Folder(DialoguesFolder);
            Folder(NpcsFolder);

            string dialoguePath = $"{DialoguesFolder}/Dialogue_Tyler.asset";
            var dialogue = AssetDatabase.LoadAssetAtPath<DialogueDefinition>(dialoguePath);
            if (dialogue == null)
            {
                dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
                Fill(dialogue, TylerLines(trader.Greeting));
                AssetDatabase.CreateAsset(dialogue, dialoguePath);
                made.Add("Dialogue_Tyler");
            }

            string npcPath = $"{NpcsFolder}/Tyler.asset";
            if (AssetDatabase.LoadAssetAtPath<NpcDefinition>(npcPath) == null)
            {
                var npc = ScriptableObject.CreateInstance<NpcDefinition>();
                var so = new SerializedObject(npc);
                so.FindProperty("displayName").stringValue = trader.DisplayName;
                so.FindProperty("portrait").objectReferenceValue = trader.Portrait;
                so.FindProperty("dialogue").objectReferenceValue = dialogue;
                so.FindProperty("trader").objectReferenceValue = trader;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(npc, npcPath);
                made.Add("Npcs/Tyler");
            }

            int rewarded = 0;
            foreach (var (quest, points) in new[] { ("Tyler/Tyler_01_Papers", 5), ("Tyler/Tyler_02_RedFolder", 5), ("Tyler/Tyler_03_Parking", 5), ("Tyler/Tyler_Side_Mugs", 3) })
            {
                var asset = AssetDatabase.LoadAssetAtPath<QuestDefinition>($"Assets/Resources/Quests/{quest}.asset");
                if (asset == null || asset.RewardLoyalty > 0) continue;
                var so = new SerializedObject(asset);
                so.FindProperty("rewardLoyalty").intValue = points;
                so.ApplyModifiedPropertiesWithoutUndo();
                rewarded++;
            }

            int locked = 0;
            var tso = new SerializedObject(trader);
            var stock = tso.FindProperty("stock");
            for (int i = 0; i < stock.arraySize; i++)
            {
                var entry = stock.GetArrayElementAtIndex(i);
                var item = entry.FindPropertyRelative("item").objectReferenceValue as ItemDefinition;
                if (item == null || item.name != "Sledgehammer" || entry.FindPropertyRelative("minLoyalty").intValue > 1) continue;
                entry.FindPropertyRelative("minLoyalty").intValue = 2;
                locked++;
            }
            tso.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return $"Tyler: made {made.Count} ({string.Join(", ", made)}), loyalty given to {rewarded} quests, {locked} wares behind loyalty";
        }

        private static List<Line> TylerLines(string greeting)
        {
            Answer Trade(string back) => new Answer { Text = "Давай посмотрим товары", Next = back, Actions = { (DialogueActionKind.OpenTrade, "", 0) } };
            Answer Work() => new Answer { Text = "Есть для меня работа?", Actions = { (DialogueActionKind.OpenWork, "", 0) } };
            Answer Leave() => new Answer { Text = "Я пойду" };

            return new List<Line>
            {
                new Line
                {
                    Id = "start_friend", Start = true, MinLoyalty = 2,
                    Text = "Здорово, [ИГРОК]! Своим у меня всегда найдётся что-нибудь получше.",
                    Answers = { Trade("start_friend"), Work(), new Answer { Text = "Что нового?", Next = "news" }, Leave() },
                },
                new Line
                {
                    Id = "start", Start = true,
                    Text = string.IsNullOrWhiteSpace(greeting) ? "Привет, [ИГРОК]. Смотри, что есть." : greeting,
                    Answers = { Trade("start"), Work(), Leave() },
                },
                new Line
                {
                    Id = "news",
                    Text = "Говорят, в лабиринте кто-то сидит. Живой. Стажёр из бухгалтерии, если верить слухам.\n\n"
                        + "Не знаю, кто его туда затащил, но сам он оттуда вряд ли выберется.",
                    Answers = { new Answer { Text = "Ясно", Next = "start_friend" } },
                },
            };
        }

        /// <summary>Writes lines into a dialogue that is not an asset yet (so the file is right from its first write).</summary>
        public static void Fill(DialogueDefinition dialogue, List<Line> lines)
        {
            var so = new SerializedObject(dialogue);
            var list = so.FindProperty("lines");
            list.arraySize = lines.Count;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = line.Id;
                element.FindPropertyRelative("text").stringValue = line.Text;
                element.FindPropertyRelative("start").boolValue = line.Start;
                var lineConditions = new List<(DialogueConditionKind kind, int level, string choice)>(line.Conditions);
                if (line.MinLoyalty > 1) lineConditions.Insert(0, (DialogueConditionKind.MinLoyalty, line.MinLoyalty, ""));
                var conditions = element.FindPropertyRelative("conditions");
                conditions.arraySize = lineConditions.Count;
                for (int k = 0; k < lineConditions.Count; k++)
                {
                    var condition = conditions.GetArrayElementAtIndex(k);
                    condition.FindPropertyRelative("kind").enumValueIndex = (int)lineConditions[k].kind;
                    condition.FindPropertyRelative("level").intValue = Mathf.Max(1, lineConditions[k].level);
                    condition.FindPropertyRelative("choice").stringValue = lineConditions[k].choice ?? string.Empty;
                }

                var answers = element.FindPropertyRelative("answers");
                answers.arraySize = line.Answers.Count;
                for (int j = 0; j < line.Answers.Count; j++)
                {
                    var answer = line.Answers[j];
                    var a = answers.GetArrayElementAtIndex(j);
                    a.FindPropertyRelative("text").stringValue = answer.Text;
                    a.FindPropertyRelative("next").stringValue = answer.Next ?? string.Empty;
                    var actions = a.FindPropertyRelative("actions");
                    actions.arraySize = answer.Actions.Count;
                    for (int k = 0; k < answer.Actions.Count; k++)
                    {
                        var action = actions.GetArrayElementAtIndex(k);
                        action.FindPropertyRelative("kind").enumValueIndex = (int)answer.Actions[k].kind;
                        action.FindPropertyRelative("choice").stringValue = answer.Actions[k].choice ?? string.Empty;
                        action.FindPropertyRelative("amount").intValue = answer.Actions[k].amount;
                    }
                    var answerConditions = a.FindPropertyRelative("conditions");
                    answerConditions.arraySize = answer.Conditions.Count;
                    for (int k = 0; k < answer.Conditions.Count; k++)
                    {
                        var condition = answerConditions.GetArrayElementAtIndex(k);
                        condition.FindPropertyRelative("kind").enumValueIndex = (int)answer.Conditions[k].kind;
                        condition.FindPropertyRelative("level").intValue = Mathf.Max(1, answer.Conditions[k].level);
                        condition.FindPropertyRelative("choice").stringValue = answer.Conditions[k].choice ?? string.Empty;
                    }
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
