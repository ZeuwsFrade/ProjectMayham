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
        // The tracker hands over what was reached from a private handler of GameSession.SaveRequested, which is
        // only wired in Play mode: the checks reach it, and the fields it reads, by name.
        private static FieldInfo Hidden(object target, string field)
        {
            var info = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (info == null) throw new System.InvalidOperationException($"{target.GetType().Name} has no private field '{field}' any more.");
            return info;
        }

        static partial void TrackerTests(Checks t)
        {
            // "Come back alive": what was reached is handed to the quests on a save only while the player lives.
            using (var world = new World())
            {
                var quest = world.Quest("Scout");
                world.Objective(quest, QuestObjectiveKind.VisitRoom, null, 1, RoomKind.Parking);
                QuestLog.Accept(quest);

                var owner = new GameObject("self-test tracker") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    var tracker = owner.AddComponent<RaidQuestTracker>();
                    var stats = owner.AddComponent<PlayerStats>();
                    var commit = typeof(RaidQuestTracker).GetMethod("Commit", BindingFlags.NonPublic | BindingFlags.Instance);
                    ((HashSet<RoomKind>)Hidden(tracker, "visited").GetValue(tracker)).Add(RoomKind.Parking);

                    commit.Invoke(tracker, null);
                    t.Eq(world.Data.quests[0].marks.Count, 0, "no player found: nothing is brought home");

                    Hidden(tracker, "player").SetValue(tracker, stats);
                    Hidden(stats, "dead").SetValue(stats, true);
                    commit.Invoke(tracker, null);
                    t.Eq(world.Data.quests[0].marks.Count, 0, "a dead player brings nothing home");

                    Hidden(stats, "dead").SetValue(stats, false);
                    commit.Invoke(tracker, null);
                    t.Eq(string.Join(",", world.Data.quests[0].marks), "visit:Parking", "a living player brings the place home");
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
