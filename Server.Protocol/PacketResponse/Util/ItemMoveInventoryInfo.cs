namespace Server.Protocol.PacketResponse.Util
{
    public class FromItemMoveInventoryInfo
    {
        public readonly ItemMoveInventoryType ItemMoveInventoryType;
        public readonly int Slot;
        public readonly int X;
        public readonly int Y;

        /// <summary>
        /// アイテム移動用のパラメータクラスです
        /// </summary>
        /// <param name="itemMoveInventoryType">移動元のインベントリタイプ</param>
        /// <param name="slot">移動元のスロット insertモードの時は指定しない</param>
        /// <param name="x">ブロックインベントリの時は座標を指定する</param>
        /// <param name="y">ブロックインベントリの時は座標を指定する</param>
        public FromItemMoveInventoryInfo(ItemMoveInventoryType itemMoveInventoryType,int slot,int x = 0, int y = 0)
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

        /// <summary>
        /// アイテム移動用のパラメータクラスです
        /// </summary>
        /// <param name="itemMoveInventoryType">移動先のインベントリタイプ</param>
        /// <param name="slot">移動先のスロット insertモードの時は指定しない</param>
        /// <param name="x">ブロックインベントリの時は座標を指定する</param>
        /// <param name="y">ブロックインベントリの時は座標を指定する</param>
        public ToItemMoveInventoryInfo(ItemMoveInventoryType itemMoveInventoryType,int slot = -1,int x = 0, int y = 0)
        {
            ItemMoveInventoryType = itemMoveInventoryType;
            Slot = slot;
            X = x;
            Y = y;
        }
    }
    
}