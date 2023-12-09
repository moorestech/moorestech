using MainGame.Basic;
using MainGame.Network.Send;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    /// <summary>
    ///     シフト + クリック でメイン、サブインベントリ間のアイテムの直接移動のパケットを送信する
    /// </summary>
    public class DirectItemMovePacketSend : IInitializable
    {
        private readonly InventoryMoveItemProtocol _inventoryMoveItem;
        private readonly SubInventoryTypeProvider _subInventoryTypeProvider;

        public DirectItemMovePacketSend(InventoryMoveItemProtocol inventoryMoveItem, SubInventoryTypeProvider subInventoryTypeProvider)
        {
            _inventoryMoveItem = inventoryMoveItem;
            _subInventoryTypeProvider = subInventoryTypeProvider;
        }

        public void Initialize()
        {
        }


        /// <summary>
        ///     シフト + クリックでサブインベントリに直接アイテムを移動するやつ
        /// </summary>
        /// <param name="slot"></param>
        private void ItemDirectMove(int slot)
        {
            FromItemMoveInventoryInfo from;
            ToItemMoveInventoryInfo to;
            var pos = _subInventoryTypeProvider.BlockPos;
            //スロット番号はメインインベントリから始まり、サブインベントリがメインインベントリの最後+1から始まるのでこのifが必要
            if (slot < PlayerInventoryConstant.MainInventorySize)
            {
                //メインインベントリに置く
                from = new FromItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory, slot);
                to = new ToItemMoveInventoryInfo(_subInventoryTypeProvider.CurrentSubInventory, x: pos.x, y: pos.y);
            }
            else
            {
                //サブインベントリに置く
                var subSlot = slot - PlayerInventoryConstant.MainInventorySize;
                from = new FromItemMoveInventoryInfo(_subInventoryTypeProvider.CurrentSubInventory, subSlot, pos.x, pos.y);
                to = new ToItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory);
            }

        }
    }
}