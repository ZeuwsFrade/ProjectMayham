using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Dialogue;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Npcs;
using ProjectMayham.Quests;
using ProjectMayham.Trade;
using ProjectMayham.World;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Makes the lost intern (Mayham > Dialogue > Create Intern): the NPC, the person item he is carried as (6x2,
    /// 15 kg), his prefabs for the maze and the shelter, his dialogue with a fork, his quests, his giver and his trade.
    /// Every text, number and picture is a placeholder for the writer. What exists already is left as it is.
    /// </summary>
    public static class InternSamples
    {
        public const string Id = "Intern";
        private const string SpritesFolder = "Assets/Sprites/Npcs";
        private const string PrefabsFolder = "Assets/Prefabs/Npcs";
        private const string ItemsFolder = "Assets/Data/Items/Npcs";
        private const string QuestsFolder = "Assets/Resources/Quests/Intern";
        private const string GiversFolder = "Assets/Resources/Quests/Givers";
        private const string PickupTemplate = "Assets/Prefabs/Interactable/Mug.prefab";

        public const string ChoiceMet = "intern_met";
        // He told why he hides; then one of the two answers below, which picks his quest at the base.
        public const string ChoiceHeard = "intern_heard";
        public const string ChoiceFled = "intern_fled";
        public const string ChoiceSaw = "intern_saw";

        [MenuItem("Mayham/Dialogue/Create Intern")]
        public static void CreateMenu() => Debug.Log(Create());

        public static string Create()
        {
            var made = new List<string>();
            foreach (string folder in new[] { SpritesFolder, PrefabsFolder, ItemsFolder, QuestsFolder, NpcSamples.DialoguesFolder, NpcSamples.NpcsFolder }) Folder(folder);

            var sitting = Sprite($"{SpritesFolder}/Intern.png", 32);
            var lying = Sprite($"{SpritesFolder}/Intern-Carried.png", 16);
            if (sitting == null || lying == null) return "no sprites in Assets/Sprites/Npcs (Intern.png, Intern-Carried.png)";

            var raid = Prefab($"{PrefabsFolder}/Intern_Raid.prefab", sitting, typeof(LostNpc), true, made);
            var shelter = Prefab($"{PrefabsFolder}/Intern_Shelter.prefab", sitting, typeof(NpcResident), false, made);

            var item = Ensure<ItemDefinition>($"{ItemsFolder}/Intern.asset", made, so =>
            {
                so.FindProperty("displayName").stringValue = "Стажёр";
                so.FindProperty("icon").objectReferenceValue = lying;
                so.FindProperty("maxStack").intValue = 1;
                so.FindProperty("gridSize").vector2IntValue = new Vector2Int(6, 2);
                so.FindProperty("canRotate").boolValue = true;
                so.FindProperty("weight").floatValue = 15f;
                so.FindProperty("value").intValue = 0;
                so.FindProperty("holdable").boolValue = true;
                so.FindProperty("heldSprite").objectReferenceValue = lying;
                so.FindProperty("heldMatchesWorld").boolValue = false;
                so.FindProperty("heldScale").floatValue = 0.3f;
                so.FindProperty("isPerson").boolValue = true;
                so.FindProperty("worldPrefab").objectReferenceValue = raid;
            });

            var trader = Ensure<TraderDefinition>("Assets/Data/Traders/Intern.asset", made, so =>
            {
                so.FindProperty("displayName").stringValue = "Стажёр";
                so.FindProperty("portrait").objectReferenceValue = sitting;
                so.FindProperty("greeting").stringValue = "Канцелярия! Почти не краденая.";
                so.FindProperty("sellMarkup").floatValue = 1.2f;
                so.FindProperty("buyRate").floatValue = 0.5f;
                so.FindProperty("discountPerLevel").floatValue = 0.05f;
                var stock = so.FindProperty("stock");
                var wares = new[] { ("Notes", 10, 1), ("Folders", 6, 1), ("Papers", 20, 1), ("Mop", 1, 3) };
                stock.arraySize = wares.Length;
                for (int i = 0; i < wares.Length; i++)
                {
                    var entry = stock.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("item").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Data/Items/{wares[i].Item1}.asset");
                    entry.FindPropertyRelative("count").intValue = wares[i].Item2;
                    entry.FindPropertyRelative("priceOverride").intValue = 0;
                    entry.FindPropertyRelative("minLoyalty").intValue = wares[i].Item3;
                }
            });

            var giver = Ensure<QuestGiverDefinition>($"{GiversFolder}/Intern.asset", made, so =>
            {
                so.FindProperty("askChoice").stringValue = "Чем тебе помочь?";
                so.FindProperty("menuText").stringValue = "Если не трудно…";
                so.FindProperty("noWorkText").stringValue = "Пока ничего не нужно. Спасибо, что спросил, [ИГРОК].";
                so.FindProperty("acceptedText").stringValue = "Правда? Спасибо!";
                so.FindProperty("reminderText").stringValue = "Я подожду. Я тут уже привык ждать.";
                so.FindProperty("readyText").stringValue = "Принёс? Ты лучший.";
                so.FindProperty("noRoomText").stringValue = "Тебе некуда положить… Освободи место, я не убегу.";
            });

            var mug = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Mug.asset");
            var notes = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Notes.asset");
            var folders = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Folders.asset");
            var briefcase = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Equipment/Briefcase.asset");
            // The rescue is taken when he goes along and handed in when he arrives; the others are given at the base.
            var rescue = Quest("Intern_00_Rescue", giver, 0, made, "Выведи меня",
                "Выведи меня отсюда, а? Сам я не дойду — ноги не держат.",
                "Стены… Настоящие стены. Спасибо, [ИГРОК].\n\nВозьми мой портфель. Мне он теперь ни к чему — отчёт всё равно пропал.",
                0, 10, null, new[] { (QuestObjectiveKind.Rescue, item, 1, RoomKind.Office) }, rewards: new[] { (briefcase, 1) });
            var coffee = Quest("Intern_01_Mug", giver, 1, made, "Кружка",
                "Кружка. Мне нужна кружка. Без кофе я тут сойду с ума — а кофе тут нет, но хоть подержу.",
                "Спасибо… Даже без кофе уже легче.", 10, 5, rescue, new[] { (QuestObjectiveKind.Deliver, mug, 1, RoomKind.Office) });
            Quest("Intern_02_Notes", giver, 2, made, "Записки",
                "Я бежал так, что растерял все записи. Принеси записки — по ним я восстановлю отчёт.",
                "Отлично. Теперь я почти полезный сотрудник.", 15, 6, coffee, new[] { (QuestObjectiveKind.Deliver, notes, 3, RoomKind.Office) },
                required: ChoiceFled);
            Quest("Intern_02_Parking", giver, 2, made, "Что у парковки",
                "Ты спрашивал, что я видел у парковки. Сходи туда — только посмотри и сразу назад. Мне надо знать, что мне не показалось.",
                "Значит, оно там… Мне не показалось. Спасибо, что сходил.", 15, 6, coffee, new[] { (QuestObjectiveKind.VisitRoom, (ItemDefinition)null, 1, RoomKind.Parking) },
                required: ChoiceSaw);
            Quest("Intern_03_Folders", giver, 3, made, "Папки для учёта",
                "Хочу навести порядок в нашем убежище. Принеси две пачки папок.",
                "Вот теперь здесь как в нормальном офисе.", 20, 5, rescue, new[] { (QuestObjectiveKind.Deliver, folders, 2, RoomKind.Office) },
                minLoyalty: 2);

            var dialogue = AssetDatabase.LoadAssetAtPath<DialogueDefinition>($"{NpcSamples.DialoguesFolder}/Dialogue_Intern.asset");
            if (dialogue == null)
            {
                dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
                NpcSamples.Fill(dialogue, Lines());
                AssetDatabase.CreateAsset(dialogue, $"{NpcSamples.DialoguesFolder}/Dialogue_Intern.asset");
                made.Add("Dialogue_Intern");
            }

            var npc = Ensure<NpcDefinition>($"{NpcSamples.NpcsFolder}/{Id}.asset", made, so =>
            {
                so.FindProperty("displayName").stringValue = "Стажёр Витя";
                so.FindProperty("portrait").objectReferenceValue = sitting;
                so.FindProperty("dialogue").objectReferenceValue = dialogue;
                so.FindProperty("trader").objectReferenceValue = trader;
                so.FindProperty("lostInMaze").boolValue = true;
                so.FindProperty("raidPrefab").objectReferenceValue = raid;
                so.FindProperty("carryItem").objectReferenceValue = item;
                so.FindProperty("takeAlongLevel").intValue = 1;
                so.FindProperty("fleeDistance").floatValue = 6f;
                so.FindProperty("shelterPrefab").objectReferenceValue = shelter;
            });

            // The prefabs point at the NPC once it exists.
            SetNpc($"{PrefabsFolder}/Intern_Raid.prefab", npc);
            SetNpc($"{PrefabsFolder}/Intern_Shelter.prefab", npc);
            ShelterSetup.RefreshItemDatabase();
            AssetDatabase.SaveAssets();
            return $"Intern: made {made.Count} ({string.Join(", ", made)})";
        }

        public static List<NpcSamples.Line> Lines()
        {
            NpcSamples.Answer Leave(string text) => new NpcSamples.Answer { Text = text };
            NpcSamples.Answer Along() => new NpcSamples.Answer
            {
                Text = "Пойдём со мной", Actions = { (DialogueActionKind.TakeAlong, "", 0) },
                Conditions = { (DialogueConditionKind.ChoiceMade, 1, ChoiceHeard) },
            };
            NpcSamples.Answer Why() => new NpcSamples.Answer
            {
                Text = "Почему ты шепчешь?", Next = "hiding",
                Conditions = { (DialogueConditionKind.ChoiceNotMade, 1, ChoiceHeard) },
            };

            return new List<NpcSamples.Line>
            {
                // In the maze: who he is, why he hides (the fork), and going along. No work here: his first quest is
                // the rescue itself, the others he gives at the base.
                new NpcSamples.Line
                {
                    Id = "raid_first", Start = true,
                    Conditions = { (DialogueConditionKind.InRaid, 1, ""), (DialogueConditionKind.ChoiceNotMade, 1, ChoiceMet) },
                    Text = "Тс-с! Не шуми… Ты живой? Настоящий?\n\nЯ Витя, стажёр из бухгалтерии. Пошёл сдавать отчёт — и заблудился. Давно.",
                    Answers =
                    {
                        new NpcSamples.Answer { Text = "Почему ты шепчешь?", Next = "hiding", Actions = { (DialogueActionKind.RememberChoice, ChoiceMet, 0) } },
                        new NpcSamples.Answer { Text = "Мне пора", Actions = { (DialogueActionKind.RememberChoice, ChoiceMet, 0) } },
                    },
                },
                new NpcSamples.Line
                {
                    Id = "hiding",
                    Text = "Тут кто-то ходит. У парковки. Я видел — это не человек.\n\nЯ бежал так, что растерял все записи. Теперь сижу и не дышу.",
                    Answers =
                    {
                        new NpcSamples.Answer
                        {
                            Text = "Забудь про записи. Уходим", Next = "go",
                            Actions = { (DialogueActionKind.RememberChoice, ChoiceHeard, 0), (DialogueActionKind.RememberChoice, ChoiceFled, 0) },
                        },
                        new NpcSamples.Answer
                        {
                            Text = "Что ты видел у парковки?", Next = "saw",
                            Actions = { (DialogueActionKind.RememberChoice, ChoiceHeard, 0), (DialogueActionKind.RememberChoice, ChoiceSaw, 0) },
                        },
                    },
                },
                new NpcSamples.Line
                {
                    Id = "go",
                    Text = "Уходим?.. Да. Да! Выведи меня, пожалуйста. Сам я не дойду — ноги не держат.",
                    Answers = { Along(), Leave("Подожди здесь") },
                },
                new NpcSamples.Line
                {
                    Id = "saw",
                    Text = "Не знаю. Высокое. Стоит и не двигается, пока на него смотришь.\n\nВыведи меня — в убежище расскажу всё, что помню.",
                    Answers = { Along(), Leave("Подожди здесь") },
                },
                new NpcSamples.Line
                {
                    Id = "raid", Start = true, Conditions = { (DialogueConditionKind.InRaid, 1, "") },
                    Text = "Ты вернулся! Я уж думал, тебя кто-нибудь съел.",
                    Answers = { Along(), Why(), Leave("Держись тут") },
                },

                // At the base.
                new NpcSamples.Line
                {
                    Id = "base", Start = true, Conditions = { (DialogueConditionKind.AtBase, 1, "") },
                    Text = "Тут хотя бы есть стулья. И никто не ходит за стеной.\n\nЕсли что-то нужно — у меня завалялось кое-что из канцелярии.",
                    Answers =
                    {
                        new NpcSamples.Answer { Text = "Давай поторгуем", Next = "base", Actions = { (DialogueActionKind.OpenTrade, "", 0) } },
                        new NpcSamples.Answer { Text = "Есть работа?", Actions = { (DialogueActionKind.OpenWork, "", 0) } },
                        new NpcSamples.Answer { Text = "Как тебе тут?", Next = "base_talk" },
                        Leave("Я пойду"),
                    },
                },
                new NpcSamples.Line
                {
                    Id = "base_talk",
                    Text = "Спокойно. Иногда мне снится лабиринт.\n\nГоворят, когда построишь мастерскую, я смогу кое-что показать. Я ведь не только отчёты умею.",
                    Answers = { new NpcSamples.Answer { Text = "Ясно", Next = "base" } },
                },
            };
        }

        private static QuestDefinition Quest(string name, QuestGiverDefinition giver, int order, List<string> made, string title, string offer,
            string thanks, int money, int loyalty, QuestDefinition requires,
            (QuestObjectiveKind kind, ItemDefinition item, int count, RoomKind room)[] objectives,
            string required = null, int minLoyalty = 1, (ItemDefinition item, int count)[] rewards = null)
        {
            return Ensure<QuestDefinition>($"{QuestsFolder}/{name}.asset", made, so =>
            {
                so.FindProperty("giver").objectReferenceValue = giver;
                so.FindProperty("order").intValue = order;
                so.FindProperty("title").stringValue = title;
                so.FindProperty("offerText").stringValue = offer;
                so.FindProperty("completedText").stringValue = thanks;
                so.FindProperty("rewardMoney").intValue = money;
                so.FindProperty("rewardLoyalty").intValue = loyalty;
                var items = so.FindProperty("rewardItems");
                items.arraySize = rewards != null ? rewards.Length : 0;
                for (int i = 0; i < items.arraySize; i++)
                {
                    items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = rewards[i].item;
                    items.GetArrayElementAtIndex(i).FindPropertyRelative("count").intValue = rewards[i].count;
                }
                so.FindProperty("minLoyalty").intValue = minLoyalty;
                var list = so.FindProperty("requires");
                list.arraySize = requires != null ? 1 : 0;
                if (requires != null) list.GetArrayElementAtIndex(0).objectReferenceValue = requires;
                var choices = so.FindProperty("requiredChoices");
                choices.arraySize = required != null ? 1 : 0;
                if (required != null) choices.GetArrayElementAtIndex(0).stringValue = required;
                var goals = so.FindProperty("objectives");
                goals.arraySize = objectives.Length;
                for (int i = 0; i < objectives.Length; i++)
                {
                    var goal = goals.GetArrayElementAtIndex(i);
                    goal.FindPropertyRelative("kind").intValue = (int)objectives[i].kind;
                    goal.FindPropertyRelative("item").objectReferenceValue = objectives[i].item;
                    goal.FindPropertyRelative("count").intValue = objectives[i].count;
                    goal.FindPropertyRelative("room").intValue = (int)objectives[i].room;
                }
            });
        }

        // Filled in before it becomes an asset, so the file is right from its first write.
        private static T Ensure<T>(string path, List<string> made, System.Action<SerializedObject> fill) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            var so = new SerializedObject(asset);
            fill(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
            made.Add(System.IO.Path.GetFileNameWithoutExtension(path));
            return asset;
        }

        // The mug's pickup made into a person: the sprite, a collider on the furniture layer, the NPC component.
        private static GameObject Prefab(string path, Sprite sprite, System.Type component, bool inMaze, List<string> made)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            if (!AssetDatabase.CopyAsset(PickupTemplate, path)) return null;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = System.IO.Path.GetFileNameWithoutExtension(path);
                root.transform.localScale = Vector3.one;
                root.layer = LayerMask.NameToLayer("Furniture");
                foreach (var pickup in root.GetComponentsInChildren<ItemPickup>(true)) Object.DestroyImmediate(pickup);
                if (!inMaze)
                {
                    foreach (var wrap in root.GetComponentsInChildren<WrapEntity>(true)) Object.DestroyImmediate(wrap);
                }
                var renderer = root.GetComponentInChildren<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.sprite = sprite;
                    renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 3);
                }
                // A missing component is not null for ??, so ask with TryGetComponent.
                if (!root.TryGetComponent<BoxCollider2D>(out var box)) box = root.AddComponent<BoxCollider2D>();
                box.size = new Vector2(0.6f, 0.6f);
                box.isTrigger = false;
                root.AddComponent(component);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            made.Add(System.IO.Path.GetFileNameWithoutExtension(path));
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void SetNpc(string path, NpcDefinition npc)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                MonoBehaviour target = null;
                if (root.TryGetComponent<LostNpc>(out var lost)) target = lost;
                else if (root.TryGetComponent<NpcResident>(out var resident)) target = resident;
                if (target == null) return;
                var so = new SerializedObject(target);
                if (so.FindProperty("npc").objectReferenceValue == npc) return;
                so.FindProperty("npc").objectReferenceValue = npc;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Sprite Sprite(string path, int pixelsPerUnit)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit)
                || importer.filterMode != FilterMode.Point || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            Folder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
