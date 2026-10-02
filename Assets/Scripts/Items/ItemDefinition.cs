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
        [Tooltip("Prefab with an ItemPickup that is spawned when the item is dropped.")]
        [SerializeField] private GameObject worldPrefab;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int MaxStack => Mathf.Max(1, maxStack);
        /// <summary>Footprint in grid cells, at least 1x1.</summary>
        public Vector2Int GridSize => new Vector2Int(Mathf.Max(1, gridSize.x), Mathf.Max(1, gridSize.y));
        public bool CanRotate => canRotate;
        public float Weight => Mathf.Max(0f, weight);
        public GameObject WorldPrefab => worldPrefab;
    }
}
