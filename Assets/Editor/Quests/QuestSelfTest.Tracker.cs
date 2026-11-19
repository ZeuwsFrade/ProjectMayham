using System.Collections.Generic;
using System.Reflection;
using ProjectMayham.Level;
using ProjectMayham.Player;
using ProjectMayham.Quests;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    public static partial class QuestSelfTest
    {
        private static FieldInfo Hidden(object target, string field)
        {
            var info = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (info == null) throw new System.InvalidOperationException($"{target.GetType().Name} has no private field '{field}' any more.");
            return info;
        }

        static partial void TrackerTests(Checks t)
        {
            using (var world = new World())
            {
                var quest = world.Quest("Scout");
                world.Objective(quest, QuestObjectiveKind.VisitRoom, null, 1, RoomKind.Parking);
                world.Objective(quest, QuestObjectiveKind.VisitZone, null, 1, zone: RaidZone.Deep);
                world.Objective(quest, QuestObjectiveKind.KillEnemies, null, 6);
                QuestLog.Accept(quest);
                QuestLog.CommitRaid(System.Array.Empty<RoomKind>(), new[] { RaidZone.Risk }, 1);

                var owner = new GameObject("self-test tracker") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    var tracker = owner.AddComponent<RaidQuestTracker>();
                    var stats = owner.AddComponent<PlayerStats>();
                    ((HashSet<RoomKind>)Hidden(tracker, "visited").GetValue(tracker)).Add(RoomKind.Parking);
                    ((HashSet<RaidZone>)Hidden(tracker, "zones").GetValue(tracker)).Add(RaidZone.Deep);
                    Hidden(tracker, "kills").SetValue(tracker, 2);

                    tracker.CommitExtraction();
                    t.Eq(world.Data.quests[0].marks.Count, 0, "no player found: nothing is brought home");
                    t.Eq(world.Data.quests[0].raidKills, 1, "staged kills do not replace previously banked kills");

                    Hidden(tracker, "player").SetValue(tracker, stats);
                    Hidden(stats, "dead").SetValue(stats, true);
                    tracker.CommitExtraction();
                    t.Eq(world.Data.quests[0].marks.Count, 0, "a dead player brings nothing home");
                    t.Eq(world.Data.quests[0].raidKills, 1, "death does not bank kills");

                    Hidden(stats, "dead").SetValue(stats, false);
                    tracker.CommitExtraction();
                    tracker.CommitExtraction();
                    t.Eq(string.Join(",", world.Data.quests[0].marks), "visit:Parking,zone:Deep", "extraction banks only matching room and zone visits");
                    t.Eq(QuestLog.Check(quest, null).Lines[2].Have, 3, "extraction adds kills to prior raid progress once");

                    var abandoned = owner.AddComponent<RaidQuestTracker>();
                    Hidden(abandoned, "player").SetValue(abandoned, stats);
                    Hidden(abandoned, "kills").SetValue(abandoned, 3);
                    Object.DestroyImmediate(abandoned);
                    t.Eq(world.Data.quests[0].raidKills, 3, "leaving without extraction does not bank this raid's kills");
                }
                finally
                {
                    Object.DestroyImmediate(owner);
                }
            }

            var go = new GameObject("self-test slot") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var slot = go.AddComponent<RoomSlot>();
                slot.Configure(RoomSize.Small, new Vector2Int(10, 20));   // 27x27 tiles of floor: x 10..37, y 20..47
                t.Ok(RaidQuestTracker.IsInside(slot, new Vector2(23.5f, 33.5f)), "the middle of the room is inside");
                t.Ok(RaidQuestTracker.IsInside(slot, new Vector2(11.5f, 21.5f)), "a tile away from the walls is inside");
                t.Ok(RaidQuestTracker.IsInside(slot, new Vector2(35.5f, 45.5f)), "the same in the opposite corner");

                // Review focus 5: the doorway and the strip along the walls do not count yet.
                t.Ok(!RaidQuestTracker.IsInside(slot, new Vector2(10.5f, 33.5f)), "the tile next to the wall is not inside yet");
                t.Ok(!RaidQuestTracker.IsInside(slot, new Vector2(9.5f, 33.5f)), "the doorway is not inside");
                t.Ok(!RaidQuestTracker.IsInside(slot, new Vector2(23.5f, 48.5f)), "the corridor behind the wall is outside");
                t.Ok(!RaidQuestTracker.IsInside(slot, new Vector2(60f, 33.5f)), "far away is outside");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
