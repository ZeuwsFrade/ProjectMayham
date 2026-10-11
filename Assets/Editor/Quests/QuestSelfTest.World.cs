using System;
using System.Collections.Generic;
using System.Reflection;
using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Quests;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    public static partial class QuestSelfTest
    {
        /// <summary>
        /// A throw-away game: its own save data put in place of the session's, quests and a giver that exist only in
        /// memory, temporary inventories. Dispose puts everything back, so the real save is never read or written.
        /// </summary>
        private sealed class World : IDisposable
        {
            private static readonly FieldInfo DataField =
                typeof(GameSession).GetField("data", BindingFlags.NonPublic | BindingFlags.Static);

            private readonly object sessionData;
            private readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();

            public readonly SaveData Data = new SaveData();
            public readonly QuestGiverDefinition Giver;
            public readonly List<QuestDefinition> Quests = new List<QuestDefinition>();

            public World(string giverId = "TestGiver")
            {
                if (DataField == null) throw new InvalidOperationException("GameSession has no private static field 'data' any more.");
                sessionData = DataField.GetValue(null);
                DataField.SetValue(null, Data);
                Giver = Make<QuestGiverDefinition>(giverId);
                Publish();
            }

            public T Make<T>(string name) where T : ScriptableObject
            {
                var asset = ScriptableObject.CreateInstance<T>();
                asset.name = name;
                asset.hideFlags = HideFlags.HideAndDontSave;
                temporary.Add(asset);
                return asset;
            }

            /// <summary>A quest of the test giver with texts made from its id; add objectives and a reward afterwards.</summary>
            public QuestDefinition Quest(string id, int order = 0, params QuestDefinition[] requires)
            {
                var quest = Make<QuestDefinition>(id);
                var so = new SerializedObject(quest);
                so.FindProperty("giver").objectReferenceValue = Giver;
                so.FindProperty("order").intValue = order;
                so.FindProperty("title").stringValue = id;
                so.FindProperty("offerText").stringValue = "Offer of " + id;
                so.FindProperty("completedText").stringValue = "Thanks for " + id;
                var list = so.FindProperty("requires");
                list.arraySize = requires.Length;
                for (int i = 0; i < requires.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = requires[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                Quests.Add(quest);
                Publish();
                return quest;
            }

            public void Objective(QuestDefinition quest, QuestObjectiveKind kind, ItemDefinition item, int count,
                RoomKind room = RoomKind.Office, string text = "", bool hidden = false)
            {
                var so = new SerializedObject(quest);
                var list = so.FindProperty("objectives");
                list.arraySize++;
                var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative("kind").intValue = (int)kind;
                entry.FindPropertyRelative("item").objectReferenceValue = item;
                entry.FindPropertyRelative("count").intValue = count;
                entry.FindPropertyRelative("room").intValue = (int)room;
                entry.FindPropertyRelative("text").stringValue = text;
                entry.FindPropertyRelative("hidden").boolValue = hidden;
                so.ApplyModifiedPropertiesWithoutUndo();
                Publish();
            }

            public void Reward(QuestDefinition quest, int money, ItemDefinition item = null, int count = 0)
            {
                var so = new SerializedObject(quest);
                so.FindProperty("rewardMoney").intValue = money;
                var list = so.FindProperty("rewardItems");
                list.arraySize = item != null ? 1 : 0;
                if (item != null)
                {
                    var entry = list.GetArrayElementAtIndex(0);
                    entry.FindPropertyRelative("item").objectReferenceValue = item;
                    entry.FindPropertyRelative("count").intValue = count;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            public void Set(UnityEngine.Object asset, string field, string value)
            {
                var so = new SerializedObject(asset);
                so.FindProperty(field).stringValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            /// <summary>An empty grid inventory with the default size (8x5) and weight limit (25 kg).</summary>
            public GridInventory Inventory()
            {
                var go = new GameObject("self-test inventory") { hideFlags = HideFlags.HideAndDontSave };
                temporary.Add(go);
                return go.AddComponent<GridInventory>();
            }

            // The catalog filters and sorts what it is given, as it does with the assets of the project.
            private void Publish() => QuestCatalog.Override(Quests, new[] { Giver });

            public void Dispose()
            {
                DataField.SetValue(null, sessionData);
                QuestCatalog.Reset();
                foreach (var item in temporary)
                {
                    if (item != null) UnityEngine.Object.DestroyImmediate(item);
                }
            }
        }

        /// <summary>An item asset of the project by name: "Papers", "Mug", "Folders", "Mop".</summary>
        private static ItemDefinition Item(string name) =>
            AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Data/Items/{name}.asset");
    }
}
