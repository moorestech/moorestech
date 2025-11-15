using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.InventoryMoveUtil
{
    public class ItemMoveInventoryInfo
    {
        public readonly ItemMoveInventoryType ItemMoveInventoryType;

        /// <summary>
        /// サブインベントリの識別子
        /// Sub-inventory identifier
        /// </summary>
        public readonly InventoryIdentifierMessagePack SubInventoryIdentifier;
        
        public static ItemMoveInventoryInfo CreateMain()
        {
            return new ItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory, null);
        }

        public static ItemMoveInventoryInfo CreateGrab()
        {
            return new ItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory, null);
        }

        public static ItemMoveInventoryInfo CreateSubInventory(InventoryIdentifierMessagePack subInventoryIdentifier)
        {
            return new ItemMoveInventoryInfo(ItemMoveInventoryType.SubInventory, subInventoryIdentifier);
        }

        private ItemMoveInventoryInfo(ItemMoveInventoryType itemMoveInventoryType, InventoryIdentifierMessagePack subInventoryIdentifier)
        {
            ItemMoveInventoryType = itemMoveInventoryType;
            SubInventoryIdentifier = subInventoryIdentifier;
        }
    }
}