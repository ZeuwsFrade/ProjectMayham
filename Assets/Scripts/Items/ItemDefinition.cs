using UnityEngine;

namespace ProjectMayham.Items
{
    /// <summary>Static data of one kind of item: name, icon, stack size and the prefab it has in the world.</summary>
    [CreateAssetMenu(menuName = "Mayham/Item", fileName = "NewItem")]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Item";
        [SerializeField] private Sprite icon;
        [Min(1)]
        [SerializeField] private int maxStack = 1;
        [Tooltip("Prefab with an ItemPickup that is spawned when the item is dropped.")]
        [SerializeField] private GameObject worldPrefab;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int MaxStack => Mathf.Max(1, maxStack);
        public GameObject WorldPrefab => worldPrefab;
    }
}
