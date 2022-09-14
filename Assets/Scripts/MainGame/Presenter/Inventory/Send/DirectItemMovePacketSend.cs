using MainGame.Basic;
using MainGame.Network.Send;
using MainGame.UnityView.UI.Inventory.Control;
using Server.Protocol.PacketResponse.Util.InventoryMoveUitl;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    /// <summary>
    /// シフト + クリック でメイン、サブインベントリ間のアイテムの直接移動のパケットを送信する
    /// </summary>
    public class DirectItemMovePacketSend : IInitializable
    {
        private readonly InventoryMoveItemProtocol _inventoryMoveItem;
        private readonly SubInventoryTypeProvider _subInventoryTypeProvider;
        private readonly PlayerInventoryViewModel _playerInventoryViewModel;

        public DirectItemMovePacketSend(InventoryMoveItemProtocol inventoryMoveItem,SubInventoryTypeProvider subInventoryTypeProvider,PlayerInventoryViewModel playerInventoryViewModel)
        {
            _inventoryMoveItem = inventoryMoveItem;
            _subInventoryTypeProvider = subInventoryTypeProvider;
            _playerInventoryViewModel = playerInventoryViewModel;
        }
        
        
        /// <summary>
        /// シフト + クリックでサブインベントリに直接アイテムを移動するやつ
        /// </summary>
        /// <param name="slot"></param>
        private void ItemDirectMove(int slot)
        {
            FromItemMoveInventoryInfo from;
            ToItemMoveInventoryInfo to;
            //スロット番号はメインインベントリから始まり、サブインベントリがメインインベントリの最後+1から始まるのでこのifが必要
            if (slot < PlayerInventoryConstant.MainInventorySize)
            {
                //メインインベントリに置く
                from = new FromItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory, slot);
                to = new ToItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory);
            }
            else
            {
                //サブインベントリに置く
                slot -= PlayerInventoryConstant.MainInventorySize;
                var pos = _subInventoryTypeProvider.BlockPos;
                from = new FromItemMoveInventoryInfo(_subInventoryTypeProvider.CurrentSubInventory, slot,pos.x,pos.y);
                to = new ToItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory);
            }

            var count = _playerInventoryViewModel[slot].Count;
            _inventoryMoveItem.Send(count,ItemMoveType.InsertSlot,from,to);
        }
        public void Initialize()
        {
            
        }
    }
}