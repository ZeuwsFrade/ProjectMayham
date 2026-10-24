using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.Quests;

namespace ProjectMayham.Npcs
{
    /// <summary>
    /// Which lost NPCs the player has brought home. An NPC carried out of a raid alive is in the player's inventory
    /// when the shelter loads; that is when they are taken out of it and stay for good. Kept in the save file.
    /// Their first quest is the rescue itself (<see cref="QuestObjectiveKind.Rescue"/>): taken when they go along,
    /// handed in when they arrive.
    /// </summary>
    public static class NpcRescue
    {
        public static bool IsRescued(string npcId) => !string.IsNullOrEmpty(npcId) && GameSession.Data.rescued.Contains(npcId);

        /// <summary>
        /// Whether the NPC is in the shelter now. Only a rescued NPC can be; one that was rescued but is not
        /// listed as away counts as present.
        /// </summary>
        public static bool IsInShelter(string npcId)
        {
            if (!IsRescued(npcId)) return false;
            var record = GameSession.Data.shelterNpcs.Find(n => n.npc == npcId);
            return record == null || record.present;
        }

        /// <summary>Writes down that a rescued NPC is in the shelter or has left it. Others are ignored.</summary>
        public static void SetInShelter(string npcId, bool present)
        {
            if (!IsRescued(npcId)) return;
            var list = GameSession.Data.shelterNpcs;
            var record = list.Find(n => n.npc == npcId);
            if (record == null) list.Add(new ShelterNpcData { npc = npcId, present = present });
            else record.present = present;
        }

        /// <summary>The NPC is home for good (rescued) and in the shelter; also brings back one who was away.</summary>
        public static void Rescue(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return;
            if (!IsRescued(npcId)) GameSession.Data.rescued.Add(npcId);
            SetInShelter(npcId, true);
        }

        /// <summary>The quests of this NPC that ask to bring them home.</summary>
        public static List<QuestDefinition> RescueQuests(string npcId) =>
            QuestCatalog.ByGiver(npcId).Where(q => q.Objectives.Any(o => o != null && o.Kind == QuestObjectiveKind.Rescue)).ToList();

        /// <summary>The NPC goes along: their rescue quest is taken, if it is on offer, and shows from then on.</summary>
        public static void StartRescue(string npcId)
        {
            foreach (var quest in RescueQuests(npcId)) QuestLog.Accept(quest);
        }

        /// <summary>
        /// The NPC is home: their rescue quests are handed in at once, also one that was never taken. Returns each
        /// with its result; one whose reward has no room in the backpack stays ready for a talk.
        /// </summary>
        public static List<(QuestDefinition quest, TurnInResult result)> FinishRescue(string npcId, GridInventory backpack)
        {
            var results = new List<(QuestDefinition, TurnInResult)>();
            foreach (var quest in RescueQuests(npcId))
            {
                QuestLog.Accept(quest);
                if (QuestLog.StatusOf(quest) != QuestStatus.Active) continue;
                results.Add((quest, QuestLog.TryTurnIn(quest, backpack)));
            }
            return results;
        }

        /// <summary>Takes every carried NPC out of the inventory (the grid and the hands) and returns who they were.</summary>
        public static List<NpcDefinition> TakeCarried(GridInventory inventory)
        {
            var carried = new List<NpcDefinition>();
            if (inventory == null) return carried;

            foreach (var stack in inventory.Stacks.Where(s => s.Item.IsPerson).ToList())
            {
                var npc = NpcCatalog.ByCarryItem(stack.Item);
                inventory.Remove(stack);
                if (npc != null) carried.Add(npc);
            }
            foreach (var hand in new[] { Hand.Right, Hand.Left })
            {
                var inHand = inventory.InHand(hand);
                if (inHand == null || !inHand.IsPerson || !inventory.TakeHeld(hand, out var held)) continue;
                var npc = NpcCatalog.ByCarryItem(held);
                if (npc != null) carried.Add(npc);
            }
            return carried;
        }
    }
}
