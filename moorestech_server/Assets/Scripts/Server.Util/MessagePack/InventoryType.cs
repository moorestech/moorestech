namespace Server.Util.MessagePack
{
    public enum InventoryType
    {
        Main,
        Grab,
        Block,
        Train,
    }

    public static class InventoryTypeExtension
    {
        public static bool IsSubInventory(this InventoryType inventoryType)
        {
            return inventoryType is InventoryType.Block or InventoryType.Train;
        }
    }
}
