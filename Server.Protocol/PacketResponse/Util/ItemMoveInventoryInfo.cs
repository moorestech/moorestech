namespace Server.Protocol.PacketResponse.Util
{
    public class ItemMoveInventoryInfo
    {
        public readonly ItemMoveInventoryType ItemMoveInventoryType;
        public readonly ItemMoveType ItemMoveType;
        public readonly int Slot;
        public readonly int X;
        public readonly int Y;

        public ItemMoveInventoryInfo(ItemMoveInventoryType itemMoveInventoryType,ItemMoveType itemMoveType, int slot,  int x = 0, int y = 0)
        {
            ItemMoveInventoryType = itemMoveInventoryType;
            Slot = slot;
            ItemMoveType = itemMoveType;
            X = x;
            Y = y;
        }
    }
}