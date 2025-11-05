using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.InventoryMoveUtil
{
    public class ItemMoveInventoryInfo
    {
        public readonly ItemMoveInventoryType ItemMoveInventoryType;

        /// <summary>
        /// ブロックまたは列車インベントリの識別子
        /// Identifier for block or train inventory
        /// </summary>
        public readonly InventoryIdentifierMessagePack InventoryIdentifier;

        /// <summary>
        ///     アイテム移動用のパラメータクラスです（プレイヤーインベントリ用）
        ///     Parameter class for item movement (for player inventory)
        /// </summary>
        /// <param name="itemMoveInventoryType">移動元のインベントリタイプ</param>
        public ItemMoveInventoryInfo(ItemMoveInventoryType itemMoveInventoryType)
        {
            ItemMoveInventoryType = itemMoveInventoryType;
            InventoryIdentifier = null;
        }

        /// <summary>
        ///     アイテム移動用のパラメータクラスです（ブロック/列車インベントリ用）
        ///     Parameter class for item movement (for block/train inventory)
        /// </summary>
        /// <param name="itemMoveInventoryType">移動元のインベントリタイプ</param>
        /// <param name="inventoryIdentifier">ブロックまたは列車インベントリの識別子</param>
        public ItemMoveInventoryInfo(ItemMoveInventoryType itemMoveInventoryType, InventoryIdentifierMessagePack inventoryIdentifier)
        {
            ItemMoveInventoryType = itemMoveInventoryType;
            InventoryIdentifier = inventoryIdentifier;
        }
    }
}