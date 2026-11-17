namespace ProjectMayham.EditorTools
{
    /// <summary>Furniture proportions matched to the hand-tuned Shelter scene.</summary>
    public static class FurnitureScalePolicy
    {
        public static float Multiplier(string spriteName)
        {
            switch (spriteName)
            {
                case "Small-Table": return 2f;
                case "Chair": case "Chair-2": case "Boss-Chair": return 1.5f;
                case "Bookshelf": case "Tall-Bookshelf": case "Water-Dispenser":
                case "Coffee-Machine": return 1.4f;
                case "Big-Filing-Cabinet": case "Filing-Cabinet-Tall":
                case "Filing-Cabinet-Small": case "Filing-Cabinet-Open":
                case "Wide-Filing-Cabinet": case "Vending-Machine": case "Bin": return 1.3f;
                case "Desk": case "Desk-2": case "Boss-Desk": case "Big-Round-Table":
                case "Big-Sofa": case "Big-Sofa-2": case "Small-Sofa":
                case "Big-Office-Printer": case "Printer-Furniture": case "Printer":
                case "WC-Sink": case "Toilet-Closed": case "Toilet-Open": return 1.2f;
                default: return 1f;
            }
        }
    }
}
