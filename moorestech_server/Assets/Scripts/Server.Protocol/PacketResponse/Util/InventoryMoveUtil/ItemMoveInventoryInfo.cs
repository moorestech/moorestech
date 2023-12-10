namespace Server.Protocol.PacketResponse.Util.InventoryMoveUtil
{
    public class FromItemMoveInventoryInfo
    {
        public readonly ItemMoveInventoryType ItemMoveInventoryType;
        public readonly int X;
        public readonly int Y;

        /// <summary>
        ///     アイテム移動用のパラメータクラスです
        /// </summary>
        /// <param name="itemMoveInventoryType">移動元のインベントリタイプ</param>
        /// <param name="x">ブロックインベントリの時は座標を指定する</param>
        /// <param name="y">ブロックインベントリの時は座標を指定する</param>
        public FromItemMoveInventoryInfo(ItemMoveInventoryType itemMoveInventoryType, int x = 0, int y = 0)
        {
            ItemMoveInventoryType = itemMoveInventoryType;
            X = x;
            Y = y;
        }
    }

    public class ToItemMoveInventoryInfo
    {
        public readonly ItemMoveInventoryType ItemMoveInventoryType;
        public readonly int X;
        public readonly int Y;

        /// <summary>
        ///     アイテム移動用のパラメータクラスです
        /// </summary>
        /// <param name="itemMoveInventoryType">移動先のインベントリタイプ</param>
        /// <param name="x">ブロックインベントリの時は座標を指定する</param>
        /// <param name="y">ブロックインベントリの時は座標を指定する</param>
        public ToItemMoveInventoryInfo(ItemMoveInventoryType itemMoveInventoryType, int x = 0, int y = 0)
        {
            ItemMoveInventoryType = itemMoveInventoryType;
            X = x;
            Y = y;
        }
    }
}