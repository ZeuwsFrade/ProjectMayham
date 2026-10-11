using System.Collections.Generic;
using ProjectMayham.Items;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// What a raid has to contain for reasons outside the level itself (quests): rooms of some kinds, items lying
    /// in them, places the player is asked to reach. Filled by whoever starts the raid and read by
    /// <see cref="RaidGenerator"/>; the level code knows nothing about where the demands come from.
    /// </summary>
    public class RaidDemands
    {
        /// <summary>An item that has to lie in a room of a kind.</summary>
        public class ItemDemand
        {
            public ItemDefinition Item;
            public int Count;
            public RoomKind Room;
        }

        private readonly List<ItemDemand> items = new List<ItemDemand>();
        private readonly List<RoomKind> visits = new List<RoomKind>();
        private readonly List<RoomKind> rooms = new List<RoomKind>();

        public IReadOnlyList<ItemDemand> Items => items;
        /// <summary>Kinds of rooms the player is asked to reach.</summary>
        public IReadOnlyList<RoomKind> Visits => visits;
        public bool IsEmpty => rooms.Count == 0;

        /// <summary>Every kind of room that has to exist, each once, the one asked for first coming first.</summary>
        public List<RoomKind> Rooms() => new List<RoomKind>(rooms);

        /// <summary>Asks for an item in a room of a kind. An item that is already asked for keeps its room and gets the larger amount.</summary>
        public void AddItem(ItemDefinition item, int count, RoomKind room)
        {
            if (item == null || count <= 0) return;

            var existing = items.Find(i => i.Item == item);
            if (existing != null)
            {
                existing.Count = Mathf.Max(existing.Count, count);
                return;
            }
            items.Add(new ItemDemand { Item = item, Count = count, Room = room });
            Require(room);
        }

        public void AddVisit(RoomKind room)
        {
            if (!visits.Contains(room)) visits.Add(room);
            Require(room);
        }

        public bool WantsVisit(RoomKind room) => visits.Contains(room);

        private void Require(RoomKind room)
        {
            if (!rooms.Contains(room)) rooms.Add(room);
        }
    }
}
