using System;
using System.Collections.Generic;
using ProjectMayham.Items;
using UnityEngine;

namespace ProjectMayham.Trade
{
    /// <summary>An item and how many of it.</summary>
    [Serializable]
    public class ItemAmount
    {
        public ItemDefinition item;
        [Min(1)] public int count = 1;
    }

    /// <summary>One wares line of a trader: what he sells, how many he has, optionally with a fixed price.</summary>
    [Serializable]
    public class StockEntry
    {
        public ItemDefinition item;
        [Min(0)] public int count = 5;
        [Tooltip("Price in money when the trader sells it. 0 = derived from the item's value.")]
        [Min(0)] public int priceOverride;
    }

    /// <summary>A trade of things for things: the player gives some items and gets others, no money involved.</summary>
    [Serializable]
    public class BarterOffer
    {
        [Tooltip("Optional text shown above the offer.")]
        public string title;
        [Tooltip("What the trader wants.")]
        public List<ItemAmount> playerGives = new List<ItemAmount>();
        [Tooltip("What the trader hands over.")]
        public List<ItemAmount> playerReceives = new List<ItemAmount>();
    }

    /// <summary>Static data of a trader: wares, prices and barter offers. The changing stock is kept in the save file.</summary>
    [CreateAssetMenu(menuName = "Mayham/Trader", fileName = "NewTrader")]
    public class TraderDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Торговец";
        [SerializeField] private Sprite portrait;
        [TextArea]
        [SerializeField] private string greeting = "Привет, [ИГРОК]. Смотри, что есть.";

        [Header("Prices")]
        [Tooltip("The trader sells an item for its value times this.")]
        [SerializeField, Min(0.1f)] private float sellMarkup = 1.3f;
        [Tooltip("The trader pays this share of an item's value.")]
        [SerializeField, Range(0f, 1f)] private float buyRate = 0.5f;

        [Header("Trade")]
        [SerializeField] private List<StockEntry> stock = new List<StockEntry>();
        [Tooltip("Barter offers. Recipes are added here; the trade window lists them automatically.")]
        [SerializeField] private List<BarterOffer> barter = new List<BarterOffer>();

        public string Id => name;
        public string DisplayName => displayName;
        public Sprite Portrait => portrait;
        public string Greeting => greeting;
        public float SellMarkup => sellMarkup;
        public float BuyRate => buyRate;
        public IReadOnlyList<StockEntry> Stock => stock;
        public IReadOnlyList<BarterOffer> Barter => barter;
    }
}
