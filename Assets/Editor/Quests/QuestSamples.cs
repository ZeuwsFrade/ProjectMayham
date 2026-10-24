using System;
using System.IO;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Player;
using ProjectMayham.Quests;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// The first, placeholder quests of Tyler: one of every kind of objective, a chain of three and a side job,
    /// so the whole loop can be played. The real texts are the writer's job. Assets that exist are left alone.
    /// </summary>
    public static class QuestSamples
    {
        private const string Root = "Assets/Resources/Quests";

        [MenuItem("Mayham/Quests/Create Sample Quests")]
        public static void CreateMenu() => Debug.Log(Create());

        public static string Create()
        {
            Directory.CreateDirectory(Root + "/Givers");
            Directory.CreateDirectory(Root + "/Tyler");
            AssetDatabase.Refresh();

            int created = 0;
            var tyler = Ensure<QuestGiverDefinition>($"{Root}/Givers/Tyler.asset", ref created, null);
            var papers = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Papers.asset");
            var mug = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Mug.asset");

            var folder = QuestMenus.CreateQuestItem("RedFolder", "Красная папка", "Folders");
            Tint(folder, new Color(1f, 0.35f, 0.3f));

            var first = Ensure<QuestDefinition>($"{Root}/Tyler/Tyler_01_Papers.asset", ref created, so =>
            {
                Fill(so, tyler, 1, "Бумажная работа",
                    "Начнём с простого, [ИГРОК]. Мне нужны бумаги — обычные, офисные. В рейде их полно: лежат по кабинетам и у копиров.\n\n" +
                    "Принеси три пачки, и я заплачу. Да, за бумагу. Не спрашивай.",
                    "Бумаги, [ИГРОК]. Три пачки. Сами они не придут.",
                    "Вот это я понимаю. Держи, как договаривались.\n\nЗагляни ещё — есть дело посерьёзнее.",
                    30);
                AddObjective(so, QuestObjectiveKind.Deliver, papers, 3, RoomKind.Office);
            });

            var second = Ensure<QuestDefinition>($"{Root}/Tyler/Tyler_02_RedFolder.asset", ref created, so =>
            {
                Fill(so, tyler, 2, "Красная папка",
                    "В кабинете босса лежит красная папка. Что в ней — не твоё дело, да и не моё, если честно.\n\n" +
                    "Найди её и принеси. Кабинет узнаешь сразу: там пахнет властью и дорогим кофе.",
                    "Красная папка из кабинета босса. Она там одна такая.",
                    "Она. Даже не помята. С тобой приятно работать, [ИГРОК].",
                    60);
                Require(so, first);
                AddObjective(so, QuestObjectiveKind.FindSpecial, folder, 1, RoomKind.BossOffice);
                AddReward(so, mug, 1);
            });

            Ensure<QuestDefinition>($"{Root}/Tyler/Tyler_03_Parking.asset", ref created, so =>
            {
                Fill(so, tyler, 3, "Разведка на парковке",
                    "Говорят, на парковке кто-то оставил машину с включённым двигателем. Третий год как.\n\n" +
                    "Сходи, посмотри своими глазами и вернись. Живым, [ИГРОК]: мёртвые плохо рассказывают.",
                    "Парковка. Дойди, осмотрись и возвращайся.",
                    "Значит, всё-таки стоит. Ладно. Держи за беспокойство.",
                    50);
                Require(so, second);
                AddObjective(so, QuestObjectiveKind.VisitRoom, null, 1, RoomKind.Parking);
            });

            Ensure<QuestDefinition>($"{Root}/Tyler/Tyler_Side_Mugs.asset", ref created, so =>
            {
                Fill(so, tyler, 10, "Кружки",
                    "У меня бьётся посуда. Не спрашивай как. Принеси две кружки — любые, лишь бы целые.",
                    "Две кружки, [ИГРОК].",
                    "Целые. Отлично. Вот твои деньги.",
                    20);
                AddObjective(so, QuestObjectiveKind.Deliver, mug, 2, RoomKind.Office);
            });

            // A skill for a quest: Tyler shows how to shake a vending machine.
            var coin = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Coin.asset");
            Ensure<QuestDefinition>($"{Root}/Tyler/Tyler_Vending.asset", ref created, so =>
            {
                Fill(so, tyler, 12, "Автоматы-жулики",
                    "Автоматы в комнатах отдыха жрут монеты и половину не отдают. Но есть способ с ними договориться.\n\n" +
                    "Принеси мне пять монет — покажу.",
                    "Пять монет, [ИГРОК]. Тогда и поговорим.",
                    "Смотри: бьёшь сбоку, вот сюда, и трясёшь. Только недолго и не на весь этаж — сбегутся.",
                    0);
                Require(so, first);
                AddObjective(so, QuestObjectiveKind.Deliver, coin, 5, RoomKind.Office);
                so.FindProperty("rewardSkill").stringValue = PlayerSkills.VendingShake;
                so.FindProperty("rewardLoyalty").intValue = 3;
            });

            // Dailies: Tyler offers two of them a day, at random; each may be refused for the loyalty it gives.
            var books = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Books.asset");
            Ensure<QuestDefinition>($"{Root}/Tyler/Tyler_Daily_Papers.asset", ref created, so =>
            {
                Fill(so, tyler, 20, "Макулатура на сегодня",
                    "Бумаги опять кончились. Пять пачек, [ИГРОК], — и на сегодня мы в расчёте.",
                    "Пять пачек бумаги. Сегодня.",
                    "Вот и славно. До завтра хватит.",
                    15);
                AddObjective(so, QuestObjectiveKind.Deliver, papers, 5, RoomKind.Office);
                Daily(so, 2);
            });
            Ensure<QuestDefinition>($"{Root}/Tyler/Tyler_Daily_Books.asset", ref created, so =>
            {
                Fill(so, tyler, 21, "Книги для растопки",
                    "Холодает. Принеси две стопки книг — читать их всё равно никто не будет.",
                    "Две стопки книг, [ИГРОК].",
                    "Отлично горят. То есть — отличные книги.",
                    20);
                AddObjective(so, QuestObjectiveKind.Deliver, books, 2, RoomKind.Office);
                Daily(so, 2);
            });
            Ensure<QuestDefinition>($"{Root}/Tyler/Tyler_Daily_Toilet.asset", ref created, so =>
            {
                Fill(so, tyler, 22, "Обход туалета",
                    "Загляни сегодня в туалет в лабиринте. Просто проверь, что там всё как обычно. И вернись.",
                    "Туалет. Загляни и возвращайся.",
                    "Как обычно? Ну и хорошо. Держи.",
                    25);
                AddObjective(so, QuestObjectiveKind.VisitRoom, null, 1, RoomKind.Toilet);
                Daily(so, 2);
            });

            // A blueprint for a quest: Tyler draws how to put a paper press together.
            var press = ModuleSetup.EnsurePress();
            if (press != null)
            {
                Ensure<QuestDefinition>($"{Root}/Tyler/Tyler_Press.asset", ref created, so =>
                {
                    Fill(so, tyler, 13, "Пресс для макулатуры",
                        "Бумага нужна всем, а книги никто не читает. Видел я в одной типографии пресс: книги туда, бумага оттуда.\n\n" +
                        "Принеси три стопки книг — на пробу. А я нарисую, как такой собрать.",
                        "Три стопки книг, [ИГРОК]. Без них и рисовать нечего.",
                        "Держи чертёж. Соберёшь в убежище из папок, швабры и пары монет. Как — не спрашивай, просто собери.",
                        0);
                    Require(so, first);
                    AddObjective(so, QuestObjectiveKind.Deliver, books, 3, RoomKind.Office);
                    so.FindProperty("rewardBlueprint").objectReferenceValue = press;
                    so.FindProperty("rewardLoyalty").intValue = 2;
                });
            }

            AssetDatabase.SaveAssets();
            QuestCatalog.Reset();
            return $"{created} quest assets created; quest item: {folder.name}";
        }

        // Makes the asset, set up by <paramref name="fill"/>; an asset that is already there is returned as it is.
        private static T Ensure<T>(string path, ref int created, Action<SerializedObject> fill) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            // Filled in before it becomes an asset, so the file is right from its first write.
            asset = ScriptableObject.CreateInstance<T>();
            if (fill != null)
            {
                var so = new SerializedObject(asset);
                fill(so);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.CreateAsset(asset, path);
            created++;
            return asset;
        }

        private static void Fill(SerializedObject quest, QuestGiverDefinition giver, int order, string title,
            string offer, string reminder, string completed, int money)
        {
            quest.FindProperty("giver").objectReferenceValue = giver;
            quest.FindProperty("order").intValue = order;
            quest.FindProperty("title").stringValue = title;
            quest.FindProperty("offerText").stringValue = offer;
            quest.FindProperty("reminderText").stringValue = reminder;
            quest.FindProperty("completedText").stringValue = completed;
            quest.FindProperty("rewardMoney").intValue = money;
        }

        // A daily that may be refused; the refusal costs as much loyalty as the quest gives.
        private static void Daily(SerializedObject quest, int loyalty)
        {
            quest.FindProperty("repeatable").boolValue = true;
            quest.FindProperty("canAbandon").boolValue = true;
            quest.FindProperty("rewardLoyalty").intValue = loyalty;
        }

        private static void Require(SerializedObject quest, QuestDefinition required)
        {
            var list = quest.FindProperty("requires");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = required;
        }

        private static void AddObjective(SerializedObject quest, QuestObjectiveKind kind, ItemDefinition item, int count, RoomKind room)
        {
            var list = quest.FindProperty("objectives");
            list.arraySize++;
            var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("kind").intValue = (int)kind;
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("count").intValue = count;
            entry.FindPropertyRelative("room").intValue = (int)room;
            entry.FindPropertyRelative("text").stringValue = string.Empty;
        }

        private static void AddReward(SerializedObject quest, ItemDefinition item, int count)
        {
            var list = quest.FindProperty("rewardItems");
            list.arraySize++;
            var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("count").intValue = count;
        }

        // The pickup is a copy of the folders': a colour tells it apart until it gets a sprite of its own.
        private static void Tint(ItemDefinition item, Color color)
        {
            string path = AssetDatabase.GetAssetPath(item.WorldPrefab);
            var contents = PrefabUtility.LoadPrefabContents(path);
            var renderer = contents.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null && renderer.color != color)
            {
                renderer.color = color;
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
