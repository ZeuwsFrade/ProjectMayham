namespace ProjectMayham.Items
{
    /// <summary>Where a thing is worn. The numbers are stored in the item assets: never renumber, only append.</summary>
    public enum EquipSlot
    {
        /// <summary>Not worn at all.</summary>
        None = 0,
        Head = 1,
        /// <summary>The backpack: it sets the size of the inventory grid.</summary>
        Back = 2,
        Feet = 3,
    }
}
