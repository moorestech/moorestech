using MainGame.GameLogic.Inventory;
using MainGame.Network.Send;

namespace MainGame.Control.UI.Inventory.ItemMove
{
    /// <summary>
    /// メインインベントリ、クラフトインベントリの間でアイテムを移動する際に使用するサービスクラス
    /// </summary>
    public class PlayerInventoryMainInventoryItemMoveService
    {
        private readonly int _playerId;
        
        private readonly MainInventoryDataCache _mainInventoryDataCache;
        

        public PlayerInventoryMainInventoryItemMoveService(
            MainInventoryDataCache mainInventoryDataCache,  
            SendMainInventoryMoveItemProtocol mainInventoryMove)
        {
            _mainInventoryDataCache = mainInventoryDataCache;
        }
        public void MoveAllItemStack(int fromSlot,bool fromIsCrafting, int toSlot, bool toIsCrafting)
        {
            var count = GetItemStackCount(fromSlot,fromIsCrafting);
            SendItemMove(fromSlot, fromIsCrafting, toSlot, toIsCrafting,count);
        }
        public void MoveHalfItemStack(int fromSlot,bool fromIsCrafting, int toSlot, bool toIsCrafting)
        {
            var count = GetItemStackCount(fromSlot,fromIsCrafting);
            SendItemMove(fromSlot, fromIsCrafting, toSlot, toIsCrafting,count/2);
        }
        public void MoveOneItemStack(int fromSlot,bool fromIsCrafting, int toSlot, bool toIsCrafting)
        {
            SendItemMove(fromSlot, fromIsCrafting, toSlot, toIsCrafting,1);
        }
        
        private int GetItemStackCount(int slot,bool isCrafting)
        {
            //TODO
            return 0;
        }

        private void SendItemMove(int fromSlot, bool fromIsCrafting, int toSlot, bool toIsCrafting, int itemCount)
        {
            //TODO
        }
    }
}