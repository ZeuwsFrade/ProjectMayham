using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectMayham.Core
{
    /// <summary>One stack of a saved grid inventory.</summary>
    [Serializable]
    public class StackData
    {
        public string item;
        public int count;
        public int x;
        public int y;
        public bool rotated;
    }

    /// <summary>Saved contents of a grid inventory (the items in the hands are plain ids).</summary>
    [Serializable]
    public class InventoryData
    {
        public List<StackData> stacks = new List<StackData>();
        // The right hand (a two-handed thing is kept here), the left hand, and which of them is used (0 right, 1 left).
        public string held;
        public string heldLeft;
        public int activeHand;
        // What is worn (item ids, empty when nothing). Only the player's inventory uses them.
        public string head;
        public string back;
        public string feet;
    }

    /// <summary>What a module on a slot of the shelter holds: things loaded on a day, things ready to take.</summary>
    [Serializable]
    public class ModuleStateData
    {
        public string slot;
        public int loaded;
        public int ready;
        /// <summary>The day (<see cref="SaveData.raids"/>) the loaded things were put in.</summary>
        public int day;
    }

    /// <summary>How many pieces of an item a trader has left.</summary>
    [Serializable]
    public class StockData
    {
        public string item;
        public int count;
    }

    /// <summary>Everything that is kept between raids and between runs of the game.</summary>
    [Serializable]
    public class SaveData
    {
        // 2: the player wears things, and the backpack sets the size of the inventory.
        // 3: Tyler sells the weapons of the fight.
        // 4: the NPCs brought home are in the shelter or away, and the save says which.
        // 5: hunger, thirst and the call of nature (an older save has none: the player is fed).
        public const int CurrentVersion = 5;
        /// <summary>The backpack of a new game.</summary>
        public const string StartBackpack = "Briefcase";
        /// <summary>The backpack as big as the inventory was before version 2 (8x5), given to older saves.</summary>
        public const string OldInventoryBackpack = "Backpack";
        /// <summary>The weapons Tyler sells since version 3, one of each.</summary>
        public static readonly string[] TylerWeapons = { "Pipe", "Sledgehammer" };
        /// <summary>The food, drink and medicine Tyler sells since version 5, with the stock of each.</summary>
        public static readonly (string item, int count)[] TylerConsumables =
        {
            ("Bar", 6), ("Noodles", 3), ("Sandwich", 4), ("Water", 8), ("CheeseFoam", 3), ("Coffee", 4), ("DripCoffee", 2), ("Bandage", 4),
        };
        public const string DefaultPlayerName = "Тайлер";

        public int version = CurrentVersion;
        public string playerName = DefaultPlayerName;
        public int money = 25;
        /// <summary>Health the player returns with; negative means "full" (a new game, or after death).</summary>
        public float health = -1f;
        /// <summary>Hunger, thirst and the call of nature the player returns with. See <see cref="Player.PlayerNeeds"/>.</summary>
        public NeedsData needs = new NeedsData();
        public InventoryData player = new InventoryData { back = StartBackpack };
        public InventoryData stash = new InventoryData();
        /// <summary>Trader id to what is left of his stock. A trader that is not listed starts with the stock of his definition.</summary>
        public List<TraderStockData> traders = new List<TraderStockData>();
        /// <summary>The shelter modules that have been built, as "slot=module". See <see cref="Shelter.ShelterModuleSlot"/>.</summary>
        public List<string> builtModules = new List<string>();
        /// <summary>Ids of the modules whose blueprints the player knows. See <see cref="Shelter.Blueprints"/>.</summary>
        public List<string> blueprints = new List<string>();
        /// <summary>What the built modules are busy with, slot by slot. See <see cref="Shelter.ModuleConverter"/>.</summary>
        public List<ModuleStateData> modules = new List<ModuleStateData>();
        /// <summary>Quests the player has taken or turned in, in the order they were taken. See <see cref="Quests.QuestLog"/>.</summary>
        public List<QuestProgressData> quests = new List<QuestProgressData>();
        /// <summary>Raids the player has set out on: the days of the dailies.</summary>
        public int raids;
        /// <summary>Skills the player has learned. See <see cref="Player.PlayerSkills"/>.</summary>
        public List<string> skills = new List<string>();
        /// <summary>The dailies each giver offers today. See <see cref="Quests.QuestLog"/>.</summary>
        public List<DailyOfferData> dailies = new List<DailyOfferData>();
        /// <summary>How worn the weapons are. See <see cref="Combat.WeaponWear"/>.</summary>
        public List<WeaponWearData> weaponWear = new List<WeaponWearData>();
        /// <summary>Loyalty points of the NPCs. See <see cref="Npcs.Loyalty"/>.</summary>
        public List<LoyaltyData> loyalty = new List<LoyaltyData>();
        /// <summary>Answers the story remembers. See <see cref="Npcs.PlayerChoices"/>.</summary>
        public List<string> choices = new List<string>();
        /// <summary>Ids of the lost NPCs the player has brought home. See <see cref="Npcs.NpcRescue"/>.</summary>
        public List<string> rescued = new List<string>();
        /// <summary>Whether each NPC brought home is in the shelter now. See <see cref="Npcs.NpcRescue.IsInShelter"/>.</summary>
        public List<ShelterNpcData> shelterNpcs = new List<ShelterNpcData>();

        /// <summary>
        /// Brings a save of an older version up to date. A trader whose stock is in the save keeps that stock and
        /// never sees what was added to his definition later, so what he sells now is added here.
        /// Before version 2 nobody wore anything and the inventory was 8x5: such a player gets the backpack of that
        /// size, so nothing they carry is lost, and Tyler gets the backpacks. Before version 3 Tyler had no weapons.
        /// </summary>
        public void Upgrade()
        {
            if (version >= CurrentVersion) return;
            var tyler = traders?.Find(t => t.trader == "Tyler");
            if (version < 2)
            {
                if (player == null) player = new InventoryData();
                player.back = OldInventoryBackpack;
                AddToStock(tyler, StartBackpack, 2);
                AddToStock(tyler, OldInventoryBackpack, 1);
            }
            if (version < 3)
            {
                foreach (string weapon in TylerWeapons) AddToStock(tyler, weapon, 1);
            }
            if (version < 4)
            {
                // Everyone brought home before this lived in the shelter.
                if (rescued == null) rescued = new List<string>();
                if (shelterNpcs == null) shelterNpcs = new List<ShelterNpcData>();
                foreach (string id in rescued)
                {
                    if (!shelterNpcs.Any(n => n.npc == id)) shelterNpcs.Add(new ShelterNpcData { npc = id, present = true });
                }
            }
            if (version < 5)
            {
                if (needs == null) needs = new NeedsData();
                foreach (var (item, count) in TylerConsumables) AddToStock(tyler, item, count);
            }
            version = CurrentVersion;
        }

        // Only what he does not have in any amount yet.
        private static void AddToStock(TraderStockData trader, string item, int count)
        {
            if (trader == null || trader.stock.Any(s => s.item == item)) return;
            trader.stock.Add(new StockData { item = item, count = count });
        }
    }

    /// <summary>The needs of the player, each 0..1 (0 = fine).</summary>
    [Serializable]
    public class NeedsData
    {
        public float hunger;
        public float thirst;
        public float nature;
    }

    /// <summary>Whether one NPC brought home is in the shelter (true) or away (false).</summary>
    [Serializable]
    public class ShelterNpcData
    {
        public string npc;
        public bool present;
    }

    /// <summary>Loyalty points of one NPC. An NPC that is not listed has none.</summary>
    [Serializable]
    public class LoyaltyData
    {
        public string npc;
        public int points;
    }

    /// <summary>Durability left in a weapon. A weapon that is not listed is as good as new.</summary>
    [Serializable]
    public class WeaponWearData
    {
        public string item;
        public float durability;
    }

    [Serializable]
    public class TraderStockData
    {
        public string trader;
        public List<StockData> stock = new List<StockData>();
    }

    /// <summary>How far one quest is. Items asked for are not stored: they are what lies in the backpack.</summary>
    [Serializable]
    public class QuestProgressData
    {
        /// <summary>Id of the quest: the name of its asset.</summary>
        public string quest;
        /// <summary>1 = taken, 2 = turned in, 3 = refused. Anything else means "no entry".</summary>
        public int state;
        /// <summary>Things done and brought home, for example "visit:Parking".</summary>
        public List<string> marks = new List<string>();
        /// <summary>The raid from which a daily or a refused quest is offered again.</summary>
        public int again;
        /// <summary>How many times the quest was turned in (a daily more than once).</summary>
        public int done;
    }

    /// <summary>The dailies a giver offers on one day (a day is a raid): picked once a day, so they do not change.</summary>
    [Serializable]
    public class DailyOfferData
    {
        public string giver;
        public int day;
        public List<string> quests = new List<string>();
    }
}
