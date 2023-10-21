namespace Server.Protocol.PacketResponse.Util.InventoryMoveUitl
{
    public class FromItemMoveInventoryInfo
    {
        public readonly ItemMoveInventoryType ItemMoveInventoryType;
        public readonly int Slot;
        public readonly int X;
        public readonly int Y;


        ///     

        /// <param name="itemMoveInventoryType"></param>
        /// <param name="slot"> insert</param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public FromItemMoveInventoryInfo(ItemMoveInventoryType itemMoveInventoryType, int slot, int x = 0, int y = 0)
        {
            ItemMoveInventoryType = itemMoveInventoryType;
            Slot = slot;
            X = x;
            Y = y;
        }
    }

    public class ToItemMoveInventoryInfo
    {
        public readonly ItemMoveInventoryType ItemMoveInventoryType;
        public readonly int Slot;
        public readonly int X;
        public readonly int Y;


        ///     

        /// <param name="itemMoveInventoryType"></param>
        /// <param name="slot"> insert</param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public ToItemMoveInventoryInfo(ItemMoveInventoryType itemMoveInventoryType, int slot = -1, int x = 0, int y = 0)
        {
            ItemMoveInventoryType = itemMoveInventoryType;
            Slot = slot;
            X = x;
            Y = y;
        }
    }
}