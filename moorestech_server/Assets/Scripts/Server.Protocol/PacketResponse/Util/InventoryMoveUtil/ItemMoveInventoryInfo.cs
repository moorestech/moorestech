using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.InventoryMoveUtil
{
    public class ItemMoveInventoryInfo
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
        public ItemMoveInventoryInfo(ItemMoveInventoryType itemMoveInventoryType, Vector2Int blockPos = default)
        {
            ItemMoveInventoryType = itemMoveInventoryType;
            X = blockPos.x;
            Y = blockPos.y;
        }
    }
}