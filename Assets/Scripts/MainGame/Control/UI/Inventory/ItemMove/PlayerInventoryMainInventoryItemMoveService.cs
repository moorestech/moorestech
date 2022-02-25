using MainGame.GameLogic.Inventory;
using MainGame.Network.Send;

namespace MainGame.Control.UI.Inventory.ItemMove
{
    /// <summary>
    /// メインインベントリ、クラフトインベントリの間でアイテムを移動する際に使用するサービスクラス
    /// </summary>
    public class PlayerInventoryMainInventoryItemMoveService
    {
        private readonly MainInventoryDataCache _mainInventoryDataCache;
        private readonly CraftingInventoryDataCache _craftingInventoryDataCache;

        private readonly SendMainInventoryMoveItemProtocol _mainInventoryMove;
        private readonly SendCraftingInventoryMoveItemProtocol _craftingInventoryMove;
        private readonly SendCraftingInventoryMainInventoryMoveItemProtocol _craftingInventoryMainInventoryMove;


        public PlayerInventoryMainInventoryItemMoveService(
            MainInventoryDataCache mainInventoryDataCache, CraftingInventoryDataCache craftingInventoryDataCache,
            SendMainInventoryMoveItemProtocol mainInventoryMove,SendCraftingInventoryMoveItemProtocol craftingInventoryMove,
            SendCraftingInventoryMainInventoryMoveItemProtocol craftingInventoryMainInventoryMove)
        {
            _mainInventoryDataCache = mainInventoryDataCache;
            _craftingInventoryDataCache = craftingInventoryDataCache;
            _mainInventoryMove = mainInventoryMove;
            _craftingInventoryMove = craftingInventoryMove;
            _craftingInventoryMainInventoryMove = craftingInventoryMainInventoryMove;
        }
        public void MoveAllItemStack(int fromSlot,bool fromIsCrafting, int toSlot, bool toIsCrafting)
        {
            var count = GetItemStackCount(fromSlot,fromIsCrafting);
            SendItemMove(fromSlot, fromIsCrafting, toSlot, toIsCrafting,
                count);
        }
        public void MoveHalfItemStack(int fromSlot,bool fromIsCrafting, int toSlot, bool toIsCrafting)
        {
            var count = GetItemStackCount(fromSlot,fromIsCrafting);
            SendItemMove(fromSlot, fromIsCrafting, toSlot, toIsCrafting,
                count/2);
        }
        public void MoveOneItemStack(int fromSlot,bool fromIsCrafting, int toSlot, bool toIsCrafting)
        {
            SendItemMove(fromSlot, fromIsCrafting, toSlot, toIsCrafting,
                1);
        }
        
        private int GetItemStackCount(int slot,bool isCrafting)
        {
            if (isCrafting)
            {
                return _craftingInventoryDataCache.GetItemStack(slot).Count;
            }
            return _mainInventoryDataCache.GetItemStack(slot).Count;
        }

        private void SendItemMove(int fromSlot, bool fromIsCrafting, int toSlot, bool toIsCrafting, int itemCount)
        {
            //クラフトインベントリ内のアイテムの移動
            if (fromIsCrafting && toIsCrafting)
            {
                _craftingInventoryMove.Send(fromSlot,toSlot,itemCount);
                return;
            }

            //プレイヤーインベントリ内のアイテムの移動
            if (!fromIsCrafting && !toIsCrafting)
            {
                _mainInventoryMove.Send(fromSlot,toSlot,itemCount);
                return;
            }
            
            //プレイヤーインベントリとブロックインベントリのアイテムの移動
            _craftingInventoryMainInventoryMove.Send(toIsCrafting,fromSlot,toSlot,itemCount);
        }
    }
}