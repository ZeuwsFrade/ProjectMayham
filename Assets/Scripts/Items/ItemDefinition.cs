using UnityEngine;

namespace ProjectMayham.Items
{
    /// <summary>Static data of one kind of item: name, icon, stack size, grid footprint, weight and the prefab it has in the world.</summary>
    [CreateAssetMenu(menuName = "Mayham/Item", fileName = "NewItem")]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Item";
        [SerializeField] private Sprite icon;
        [Min(1)]
        [SerializeField] private int maxStack = 1;
        [Tooltip("Cells the item takes in a grid inventory (width, height). A whole stack shares this footprint.")]
        [SerializeField] private Vector2Int gridSize = Vector2Int.one;
        [Tooltip("Whether the item can be turned by 90 degrees in a grid inventory.")]
        [SerializeField] private bool canRotate = true;
        [Tooltip("Weight of one item, in kg.")]
        [Min(0f)]
        [SerializeField] private float weight = 1f;
        [Header("Hands")]
        [Tooltip("Whether the player can take the item in their hands (it is then shown next to the player).")]
        [SerializeField] private bool holdable;
        [Tooltip("Sprite shown in the hands. The inventory icon is used when empty.")]
        [SerializeField] private Sprite heldSprite;
        [Tooltip("Where the item is held, relative to the player facing forward (+Y).")]
        [SerializeField] private Vector2 heldOffset = new Vector2(0.4f, 0.45f);
        [Tooltip("Rotation of the sprite in the hands, in degrees.")]
        [SerializeField] private float heldAngle;
        [Tooltip("Hold the item at the same size as its world prefab (so resizing the prefab resizes it in the hands too).")]
        [SerializeField] private bool heldMatchesWorld = true;
        [Tooltip("Size in the hands when it does not follow the world prefab (1 = the sprite's natural size).")]
        [SerializeField, Min(0.01f)] private float heldScale = 1f;

        [Header("World")]
        [Tooltip("Prefab with an ItemPickup that is spawned when the item is dropped.")]
        [SerializeField] private GameObject worldPrefab;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int MaxStack => Mathf.Max(1, maxStack);
        /// <summary>Footprint in grid cells, at least 1x1.</summary>
        public Vector2Int GridSize => new Vector2Int(Mathf.Max(1, gridSize.x), Mathf.Max(1, gridSize.y));
        public bool CanRotate => canRotate;
        public float Weight => Mathf.Max(0f, weight);
        public bool Holdable => holdable;
        public Sprite HeldSprite => heldSprite != null ? heldSprite : icon;
        public Vector2 HeldOffset => heldOffset;
        public float HeldAngle => heldAngle;
        /// <summary>World size multiplier of the sprite in the hands: that of the world prefab, or the set value.</summary>
        public float HeldScale => heldMatchesWorld && worldPrefab != null
            ? Mathf.Max(0.01f, worldPrefab.transform.lossyScale.x)
            : Mathf.Max(0.01f, heldScale);
        public GameObject WorldPrefab => worldPrefab;
    }
}
