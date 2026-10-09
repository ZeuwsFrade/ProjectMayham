using System;
using System.Collections.Generic;

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

    /// <summary>Saved contents of a grid inventory (the item in the hands is a plain id).</summary>
    [Serializable]
    public class InventoryData
    {
        public List<StackData> stacks = new List<StackData>();
        public string held;
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
        public const int CurrentVersion = 1;
        public const string DefaultPlayerName = "Тайлер";

        public int version = CurrentVersion;
        public string playerName = DefaultPlayerName;
        public int money = 25;
        /// <summary>Health the player returns with; negative means "full" (a new game, or after death).</summary>
        public float health = -1f;
        public InventoryData player = new InventoryData();
        public InventoryData stash = new InventoryData();
        /// <summary>Trader id to what is left of his stock. A trader that is not listed starts with the stock of his definition.</summary>
        public List<TraderStockData> traders = new List<TraderStockData>();
        /// <summary>Ids of the shelter modules that have been built.</summary>
        public List<string> builtModules = new List<string>();
    }

    [Serializable]
    public class TraderStockData
    {
        public string trader;
        public List<StockData> stock = new List<StockData>();
    }
}
