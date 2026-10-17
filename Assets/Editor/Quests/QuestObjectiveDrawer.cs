using System;
using System.Linq;
using ProjectMayham.Level;
using ProjectMayham.Quests;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// An objective in the Inspector: the kind first, then only the fields that kind uses, with Russian labels.
    /// </summary>
    [CustomPropertyDrawer(typeof(QuestObjective))]
    public class QuestObjectiveDrawer : PropertyDrawer
    {
        // The kinds in the list and their names (the values have a gap: 3 is kept for "kill enemies").
        private static readonly QuestObjectiveKind[] Kinds =
            { QuestObjectiveKind.Deliver, QuestObjectiveKind.FindSpecial, QuestObjectiveKind.VisitRoom, QuestObjectiveKind.Rescue };
        private static readonly string[] KindNames = { "Принести предметы", "Найти особый предмет", "Побывать в комнате", "Довести до убежища" };

        private static readonly RoomKind[] RoomKinds = (RoomKind[])Enum.GetValues(typeof(RoomKind));
        private static readonly string[] RoomNames = RoomKinds.Select(RoomSizes.Title).ToArray();

        private static float Row => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        private static QuestObjectiveKind KindOf(SerializedProperty property)
        {
            int index = Array.IndexOf(Kinds, (QuestObjectiveKind)property.FindPropertyRelative("kind").intValue);
            return index >= 0 ? Kinds[index] : QuestObjectiveKind.Deliver;
        }

        private static int Rows(QuestObjectiveKind kind)
        {
            switch (kind)
            {
                case QuestObjectiveKind.Deliver: return 4;       // kind, item, count, own wording
                case QuestObjectiveKind.FindSpecial: return 6;   // kind, item, count, room, hidden, own wording
                case QuestObjectiveKind.Rescue: return 3;        // kind, the person item, own wording
                default: return 3;                               // kind, room, own wording
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => Rows(KindOf(property)) * Row;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            var kindProperty = property.FindPropertyRelative("kind");
            var kind = KindOf(property);
            int picked = EditorGUI.Popup(row, "Цель", Array.IndexOf(Kinds, kind), KindNames);
            kind = Kinds[Mathf.Clamp(picked, 0, Kinds.Length - 1)];
            if ((int)kind != kindProperty.intValue) kindProperty.intValue = (int)kind;
            row.y += Row;

            if (kind == QuestObjectiveKind.Rescue)
            {
                EditorGUI.PropertyField(row, property.FindPropertyRelative("item"), new GUIContent("Кого ведём",
                    "Вещь-человек, которой его несут: по ней цель называется. Довести нужно того, кто даёт квест; квест берётся, когда он идёт с игроком, и сдаётся сам в убежище."));
                row.y += Row;
            }
            else if (kind != QuestObjectiveKind.VisitRoom)
            {
                EditorGUI.PropertyField(row, property.FindPropertyRelative("item"), new GUIContent("Предмет"));
                row.y += Row;
                EditorGUI.PropertyField(row, property.FindPropertyRelative("count"), new GUIContent("Сколько"));
                row.y += Row;
            }

            if (kind == QuestObjectiveKind.FindSpecial || kind == QuestObjectiveKind.VisitRoom)
            {
                var roomProperty = property.FindPropertyRelative("room");
                int index = Mathf.Max(0, Array.IndexOf(RoomKinds, (RoomKind)roomProperty.intValue));
                int chosen = EditorGUI.Popup(row, kind == QuestObjectiveKind.VisitRoom ? "Куда прийти" : "Где лежит", index, RoomNames);
                if (chosen != index || (int)RoomKinds[chosen] != roomProperty.intValue) roomProperty.intValue = (int)RoomKinds[chosen];
                row.y += Row;
            }

            if (kind == QuestObjectiveKind.FindSpecial)
            {
                EditorGUI.PropertyField(row, property.FindPropertyRelative("hidden"),
                    new GUIContent("Спрятан в контейнере", "Выключено — предмет лежит на полу комнаты. Включено — в одном из её контейнеров, его надо найти обыском."));
                row.y += Row;
            }

            EditorGUI.PropertyField(row, property.FindPropertyRelative("text"),
                new GUIContent("Своя формулировка", "Пусто — цель называется сама: по предмету и комнате."));
            EditorGUI.EndProperty();
        }
    }
}
