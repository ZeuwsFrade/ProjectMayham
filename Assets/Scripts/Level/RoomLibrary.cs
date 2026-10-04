using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>All room kinds with their frequency and loot, and every room prefab that can be put in a slot.</summary>
    [CreateAssetMenu(menuName = "Mayham/Room Library", fileName = "RoomLibrary")]
    public class RoomLibrary : ScriptableObject
    {
        [Serializable]
        public class KindInfo
        {
            public RoomKind kind;
            [Tooltip("Higher = the kind turns up more often in a raid.")]
            [Min(0f)] public float weight = 1f;
            [Tooltip("Slot sizes the kind may be put into.")]
            public RoomSize[] sizes = { RoomSize.Medium };
            public LootTable loot;
            [Tooltip("Chance that a loot spot of this kind of room gets an item.")]
            [Range(0f, 1f)] public float lootChance = 0.7f;
        }

        [SerializeField] private List<KindInfo> kinds = new List<KindInfo>();
        [SerializeField] private List<RoomTemplate> rooms = new List<RoomTemplate>();

        public IReadOnlyList<KindInfo> Kinds => kinds;
        public IReadOnlyList<RoomTemplate> Rooms => rooms;

        public KindInfo Info(RoomKind kind)
        {
            foreach (var info in kinds)
            {
                if (info.kind == kind) return info;
            }
            return null;
        }

        public void Set(List<KindInfo> newKinds, List<RoomTemplate> newRooms)
        {
            kinds = newKinds;
            rooms = newRooms;
        }

        /// <summary>Picks a kind for a slot of this size by weight among the kinds that have a room for it.</summary>
        public KindInfo PickKind(RoomSize size, System.Random rng)
        {
            float total = 0f;
            foreach (var info in kinds)
            {
                if (Allows(info, size)) total += info.weight;
            }
            if (total <= 0f) return null;

            double pick = rng.NextDouble() * total;
            KindInfo last = null;
            foreach (var info in kinds)
            {
                if (!Allows(info, size)) continue;
                last = info;
                pick -= info.weight;
                if (pick <= 0.0) return info;
            }
            return last;
        }

        /// <summary>A random room prefab of this kind and size, or null when there is none.</summary>
        public RoomTemplate PickRoom(RoomKind kind, RoomSize size, System.Random rng)
        {
            var matching = Matching(kind, size);
            return matching.Count == 0 ? null : matching[rng.Next(matching.Count)];
        }

        private bool Allows(KindInfo info, RoomSize size)
        {
            if (info.weight <= 0f || Array.IndexOf(info.sizes, size) < 0) return false;
            return Matching(info.kind, size).Count > 0;
        }

        private List<RoomTemplate> Matching(RoomKind kind, RoomSize size)
        {
            var result = new List<RoomTemplate>();
            foreach (var room in rooms)
            {
                if (room != null && room.Kind == kind && room.Size == size) result.Add(room);
            }
            return result;
        }
    }
}
