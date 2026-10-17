using ProjectMayham.Dialogue;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>A condition in the Inspector: the kind first, then only the fields that kind uses, with Russian labels.</summary>
    [CustomPropertyDrawer(typeof(DialogueCondition))]
    public class DialogueConditionDrawer : PropertyDrawer
    {
        // In the order of DialogueConditionKind.
        private static readonly string[] KindNames = { "Лояльность не ниже", "Квест", "Игрок выбрал", "Игрок не выбирал", "Есть предмет", "В рейде", "На базе" };
        // In the order of QuestStage.
        private static readonly string[] StageNames = { "не взят", "предлагается", "взят", "готов к сдаче", "сдан" };

        private static float Row => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        private static DialogueConditionKind KindOf(SerializedProperty property) =>
            (DialogueConditionKind)Mathf.Clamp(property.FindPropertyRelative("kind").intValue, 0, KindNames.Length - 1);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var kind = KindOf(property);
            if (kind == DialogueConditionKind.InRaid || kind == DialogueConditionKind.AtBase) return Row;
            return (kind == DialogueConditionKind.Quest || kind == DialogueConditionKind.HasItem ? 3 : 2) * Row;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            var kindProperty = property.FindPropertyRelative("kind");
            int picked = EditorGUI.Popup(row, "Условие", (int)KindOf(property), KindNames);
            if (picked != kindProperty.intValue) kindProperty.intValue = picked;
            row.y += Row;

            switch ((DialogueConditionKind)picked)
            {
                case DialogueConditionKind.MinLoyalty:
                    EditorGUI.PropertyField(row, property.FindPropertyRelative("level"), new GUIContent("Уровень"));
                    break;
                case DialogueConditionKind.Quest:
                    EditorGUI.PropertyField(row, property.FindPropertyRelative("quest"), new GUIContent("Квест"));
                    row.y += Row;
                    var stage = property.FindPropertyRelative("stage");
                    int chosen = EditorGUI.Popup(row, "В состоянии", Mathf.Clamp(stage.intValue, 0, StageNames.Length - 1), StageNames);
                    if (chosen != stage.intValue) stage.intValue = chosen;
                    break;
                case DialogueConditionKind.ChoiceMade:
                case DialogueConditionKind.ChoiceNotMade:
                    EditorGUI.PropertyField(row, property.FindPropertyRelative("choice"),
                        new GUIContent("Выбор", "Имя выбора, который запоминает ответ с действием «Запомнить выбор»."));
                    break;
                case DialogueConditionKind.HasItem:
                    EditorGUI.PropertyField(row, property.FindPropertyRelative("item"), new GUIContent("Предмет"));
                    row.y += Row;
                    EditorGUI.PropertyField(row, property.FindPropertyRelative("count"), new GUIContent("Сколько"));
                    break;
            }
            EditorGUI.EndProperty();
        }
    }

    /// <summary>An action in the Inspector: the kind first, then the field it uses, if any.</summary>
    [CustomPropertyDrawer(typeof(DialogueAction))]
    public class DialogueActionDrawer : PropertyDrawer
    {
        // In the order of DialogueActionKind.
        private static readonly string[] KindNames = { "Запомнить выбор", "Разговор о работе", "Торговля", "Лояльность", "Взять с собой" };

        private static float Row => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        private static DialogueActionKind KindOf(SerializedProperty property) =>
            (DialogueActionKind)Mathf.Clamp(property.FindPropertyRelative("kind").intValue, 0, KindNames.Length - 1);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var kind = KindOf(property);
            return (kind == DialogueActionKind.RememberChoice || kind == DialogueActionKind.AddLoyalty ? 2 : 1) * Row;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            var kindProperty = property.FindPropertyRelative("kind");
            int picked = EditorGUI.Popup(row, "Действие", (int)KindOf(property), KindNames);
            if (picked != kindProperty.intValue) kindProperty.intValue = picked;
            row.y += Row;

            if ((DialogueActionKind)picked == DialogueActionKind.RememberChoice)
                EditorGUI.PropertyField(row, property.FindPropertyRelative("choice"),
                    new GUIContent("Выбор", "Имя, под которым игра запомнит этот ответ: квесты и условия спрашивают его по имени."));
            else if ((DialogueActionKind)picked == DialogueActionKind.AddLoyalty)
                EditorGUI.PropertyField(row, property.FindPropertyRelative("amount"), new GUIContent("Очков", "Отрицательное число отнимает."));
            EditorGUI.EndProperty();
        }
    }
}
