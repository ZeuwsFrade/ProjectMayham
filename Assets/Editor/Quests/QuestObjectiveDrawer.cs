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
        // In the order of QuestObjectiveKind: its values are 0, 1, 2.
        private static readonly string[] KindNames = { "Принести предметы", "Найти особый предмет", "Побывать в комнате" };

        private static readonly RoomKind[] RoomKinds = (RoomKind[])Enum.GetValues(typeof(RoomKind));
        private static readonly string[] RoomNames = RoomKinds.Select(RoomSizes.Title).ToArray();

        private static float Row => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        private static QuestObjectiveKind KindOf(SerializedProperty property) =>
            (QuestObjectiveKind)Mathf.Clamp(property.FindPropertyRelative("kind").intValue, 0, KindNames.Length - 1);

        private static int Rows(QuestObjectiveKind kind)
        {
            switch (kind)
            {
                case QuestObjectiveKind.Deliver: return 4;       // kind, item, count, own wording
                case QuestObjectiveKind.FindSpecial: return 5;   // kind, item, count, room, own wording
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
            int picked = EditorGUI.Popup(row, "Цель", (int)kind, KindNames);
            if (picked != kindProperty.intValue) kindProperty.intValue = picked;
            kind = (QuestObjectiveKind)picked;
            row.y += Row;

            if (kind != QuestObjectiveKind.VisitRoom)
            {
                EditorGUI.PropertyField(row, property.FindPropertyRelative("item"), new GUIContent("Предмет"));
                row.y += Row;
                EditorGUI.PropertyField(row, property.FindPropertyRelative("count"), new GUIContent("Сколько"));
                row.y += Row;
            }

            if (kind != QuestObjectiveKind.Deliver)
            {
                var roomProperty = property.FindPropertyRelative("room");
                int index = Mathf.Max(0, Array.IndexOf(RoomKinds, (RoomKind)roomProperty.intValue));
                int chosen = EditorGUI.Popup(row, kind == QuestObjectiveKind.VisitRoom ? "Куда прийти" : "Где лежит", index, RoomNames);
                if (chosen != index || (int)RoomKinds[chosen] != roomProperty.intValue) roomProperty.intValue = (int)RoomKinds[chosen];
                row.y += Row;
            }

            EditorGUI.PropertyField(row, property.FindPropertyRelative("text"),
                new GUIContent("Своя формулировка", "Пусто — цель называется сама: по предмету и комнате."));
            EditorGUI.EndProperty();
        }
    }
}
