using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Items;

namespace ProjectMayham.Shelter
{
    /// <summary>
    /// The blueprints of the shelter's modules the player knows, kept in the save file: a module can be built only
    /// with its blueprint. Most are quest rewards (<see cref="Quests.QuestDefinition.RewardBlueprint"/>); some are
    /// items found in raids (<see cref="ShelterModuleDefinition.BlueprintItem"/>), learned when they are brought home
    /// and lost with the loot on death. A raid hides at most one of those, and only now and then.
    /// </summary>
    public static class Blueprints
    {
        public static bool IsKnown(ShelterModuleDefinition module) =>
            module != null && GameSession.Data.blueprints != null && GameSession.Data.blueprints.Contains(module.Id);

        public static void Learn(ShelterModuleDefinition module)
        {
            if (module == null || IsKnown(module)) return;
            var data = GameSession.Data;
            data.blueprints ??= new List<string>();
            data.blueprints.Add(module.Id);
        }

        /// <summary>
        /// Learns the blueprints the player has brought home: their items leave the inventory (grid and hands).
        /// Returns the modules learned now; a blueprint that was known already is taken away all the same.
        /// </summary>
        public static List<ShelterModuleDefinition> LearnCarried(GridInventory inventory)
        {
            var learned = new List<ShelterModuleDefinition>();
            if (inventory == null) return learned;

            foreach (var module in ModuleCatalog.All)
            {
                var item = module.BlueprintItem;
                if (item == null) continue;
                int carried = inventory.RemoveItems(item, int.MaxValue);
                while (inventory.TakeFromHands(item)) carried++;
                if (carried == 0) continue;
                if (!IsKnown(module)) learned.Add(module);
                Learn(module);
            }
            return learned;
        }

        /// <summary>
        /// The blueprint a raid hides, or null: with <paramref name="chance"/>, one picked at random among the modules
        /// that have a blueprint item, whose blueprint the player does not know and does not own as an item already.
        /// </summary>
        public static ShelterModuleDefinition PickForRaid(System.Random dice, float chance)
        {
            var candidates = ModuleCatalog.All
                .Where(m => m.BlueprintItem != null && !IsKnown(m) && Quests.QuestLog.Owned(GameSession.Data, m.BlueprintItem) == 0)
                .ToList();
            // Without a candidate no dice is thrown.
            if (candidates.Count == 0 || dice.NextDouble() >= chance) return null;
            return candidates[dice.Next(candidates.Count)];
        }
    }
}
