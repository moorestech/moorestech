using MainGame.Basic;
using MainGame.Network.Send;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class PlayerInventoryMoveItemPacketSend : IInitializable
    {
        private readonly InventoryMoveItemProtocol _inventoryMoveItem;
        private readonly SubInventoryTypeProvider _subInventoryTypeProvider;


        public PlayerInventoryMoveItemPacketSend(InventoryMoveItemProtocol inventoryMoveItem, SubInventoryTypeProvider subInventoryTypeProvider)
        {
            _inventoryMoveItem = inventoryMoveItem;
            _subInventoryTypeProvider = subInventoryTypeProvider;
        }


        public void Initialize()
        {
        }


        /// <summary>
        ///     アイテムをクリックしてもつ時に発火する
        /// </summary>
        private void ItemSlotGrabbed(int slot, int count)
        {
            FromItemMoveInventoryInfo from;
            ToItemMoveInventoryInfo to;
            //スロット番号はメインインベントリから始まり、サブインベントリがメインインベントリの最後+1から始まるのでこのifが必要
            if (slot < PlayerInventoryConstant.MainInventorySize)
            {
                //メインインベントリに置く
                from = new FromItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory, slot);
                to = new ToItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory, 0);
            }
            else
            {
                //サブインベントリに置く
                slot -= PlayerInventoryConstant.MainInventorySize;
                var pos = _subInventoryTypeProvider.BlockPos;
                from = new FromItemMoveInventoryInfo(_subInventoryTypeProvider.CurrentSubInventory, slot, pos.x, pos.y);
                to = new ToItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory, 0);
            }

            _inventoryMoveItem.Send(count, ItemMoveType.SwapSlot, from, to);
        }

        /// <summary>
        ///     持っているスロットからインベントリにおいた時に発火する
        /// </summary>
        private void ItemSlotAdded(int slot, int addCount)
        {
            FromItemMoveInventoryInfo from;
            ToItemMoveInventoryInfo to;
            //スロット番号はメインインベントリから始まり、サブインベントリがメインインベントリの最後+1から始まるのでこのifが必要
            if (slot < PlayerInventoryConstant.MainInventorySize)
            {
                //メインインベントリに置く
                from = new FromItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory, 0);
                to = new ToItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory, slot);
            }
            else
            {
                //サブインベントリに置く
                slot -= PlayerInventoryConstant.MainInventorySize;
                from = new FromItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory, 0);
                var pos = _subInventoryTypeProvider.BlockPos;
                to = new ToItemMoveInventoryInfo(_subInventoryTypeProvider.CurrentSubInventory, slot, pos.x, pos.y);
            }

            _inventoryMoveItem.Send(addCount, ItemMoveType.SwapSlot, from, to);
        }
    }
}