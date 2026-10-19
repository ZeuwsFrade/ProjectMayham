using ProjectMayham.Dialogue;
using ProjectMayham.Items;
using ProjectMayham.Trade;
using UnityEngine;

namespace ProjectMayham.Npcs
{
    /// <summary>
    /// An NPC as a whole: the name, the portrait, the conversation, the trade and how loyal they can get. The name of
    /// the asset is the id of the NPC: the same as the id of their quest giver and their trader (Tyler.asset
    /// everywhere). Kept in Resources/Npcs, so the game finds an NPC by the id.
    /// </summary>
    [CreateAssetMenu(menuName = "Mayham/NPC", fileName = "NewNpc")]
    public class NpcDefinition : ScriptableObject
    {
        /// <summary>Loyalty points for levels 2, 3 and 4 when an NPC does not set its own.</summary>
        public static readonly int[] DefaultLevels = { 10, 25, 50 };

        [SerializeField] private string displayName = "НПС";
        [SerializeField] private Sprite portrait;
        [Tooltip("What the NPC says. Empty: a greeting with trade and work, as before dialogues were assets.")]
        [SerializeField] private DialogueDefinition dialogue;
        [Tooltip("What the NPC sells. Empty: the NPC does not trade.")]
        [SerializeField] private TraderDefinition trader;
        [Tooltip("Loyalty points it takes to reach levels 2, 3 and 4. Quests give the points.")]
        [SerializeField] private int[] loyaltyLevels = { 10, 25, 50 };

        [Header("Lost in the maze")]
        [Tooltip("The NPC sits somewhere in the maze until the player brings them home. One lost NPC a raid: when several are still lost, the raid picks one at random.")]
        [SerializeField] private bool lostInMaze;
        [Tooltip("What sits in the maze: a prefab with LostNpc.")]
        [SerializeField] private GameObject raidPrefab;
        [Tooltip("What the player carries: a heavy item marked as a person, whose world prefab is the raid prefab.")]
        [SerializeField] private ItemDefinition carryItem;
        [Tooltip("The loyalty level from which the NPC goes with the player. 1: at the first meeting (their rescue is their first quest).")]
        [SerializeField, Range(1, 4)] private int takeAlongLevel = 1;
        [Tooltip("An enemy closer than this (world units, a tile is one) makes the NPC hide where they sit.")]
        [UnityEngine.Serialization.FormerlySerializedAs("fleeDistance")]
        [SerializeField, Min(1f)] private float hideDistance = 6f;
        [Tooltip("Seconds without an enemy near before the hidden NPC comes back.")]
        [SerializeField, Min(0.5f)] private float hideFor = 8f;

        [Header("At the base")]
        [Tooltip("What stands in the shelter once the NPC is brought home: a prefab with NpcResident.")]
        [SerializeField] private GameObject shelterPrefab;

        public string Id => name;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Portrait => portrait;
        public DialogueDefinition Dialogue => dialogue;
        public TraderDefinition Trader => trader;
        public int[] LoyaltyLevels => loyaltyLevels != null && loyaltyLevels.Length > 0 ? loyaltyLevels : DefaultLevels;
        public bool LostInMaze => lostInMaze;
        public GameObject RaidPrefab => raidPrefab;
        public ItemDefinition CarryItem => carryItem;
        public int TakeAlongLevel => Mathf.Clamp(takeAlongLevel, 1, 4);
        public float HideDistance => Mathf.Max(1f, hideDistance);
        public float HideFor => Mathf.Max(0.5f, hideFor);
        public GameObject ShelterPrefab => shelterPrefab;
    }
}
