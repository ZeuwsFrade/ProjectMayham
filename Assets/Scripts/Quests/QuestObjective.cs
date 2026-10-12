using System;
using ProjectMayham.Items;
using ProjectMayham.Level;
using UnityEngine;

namespace ProjectMayham.Quests
{
    /// <summary>What an objective asks for. The numbers are stored in the quest assets: never renumber, only append.</summary>
    public enum QuestObjectiveKind
    {
        /// <summary>Bring ordinary items to the quest giver.</summary>
        Deliver = 0,
        /// <summary>Bring an item that lies in the raid only while its quest is active.</summary>
        FindSpecial = 1,
        /// <summary>Enter a room of a kind in a raid and come back alive.</summary>
        VisitRoom = 2,
        // 3 is kept for "kill enemies".
    }

    /// <summary>One thing a quest asks the player to do.</summary>
    [Serializable]
    public class QuestObjective
    {
        [SerializeField] private QuestObjectiveKind kind;
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(1)] private int count = 1;
        [SerializeField] private RoomKind room;
        [Tooltip("For a special item: it is hidden in a container of the room instead of lying on the floor.")]
        [SerializeField] private bool hidden;
        [Tooltip("Own wording of the objective. Empty: it is put together from the item and the room.")]
        [SerializeField] private string text;

        public QuestObjectiveKind Kind => kind;
        public ItemDefinition Item => item;
        public int Count => Mathf.Max(1, count);
        /// <summary>Where the special item lies, or the place to visit.</summary>
        public RoomKind Room => room;
        /// <summary>True for a special item that has to be searched for in the containers of its room.</summary>
        public bool Hidden => kind == QuestObjectiveKind.FindSpecial && hidden;

        public bool NeedsItem => kind == QuestObjectiveKind.Deliver || kind == QuestObjectiveKind.FindSpecial;
        /// <summary>False when the objective cannot be checked: it needs an item and has none.</summary>
        public bool IsUsable => !NeedsItem || item != null;

        /// <summary>The mark in the save file that says the place was reached and brought home.</summary>
        public string VisitMark => VisitMarkOf(room);
        public static string VisitMarkOf(RoomKind kind) => "visit:" + kind;

        private bool HasOwnText => !string.IsNullOrWhiteSpace(text);
        private string ItemName => item != null ? item.DisplayName : "?";

        /// <summary>What the objective is about, without the amount: "Папки", "Красная папка (Офис босса)", "Побывать: Парковка".</summary>
        public string Subject
        {
            get
            {
                if (HasOwnText) return text.Trim();
                switch (kind)
                {
                    case QuestObjectiveKind.Deliver: return ItemName;
                    case QuestObjectiveKind.FindSpecial: return $"{ItemName} ({RoomSizes.Title(room)})";
                    default: return $"Побывать: {RoomSizes.Title(room)}";
                }
            }
        }

        /// <summary>The objective as it is offered, with the amount: "Папки ×3".</summary>
        public string Describe()
        {
            if (HasOwnText || !NeedsItem || Count <= 1) return Subject;
            return kind == QuestObjectiveKind.Deliver
                ? $"{ItemName} ×{Count}"
                : $"{ItemName} ×{Count} ({RoomSizes.Title(room)})";
        }
    }
}
