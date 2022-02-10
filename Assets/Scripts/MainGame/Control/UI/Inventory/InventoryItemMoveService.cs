using MainGame.GameLogic;
using MainGame.GameLogic.Inventory;
using MainGame.Network.Send;
using UnityEngine;

namespace MainGame.Control.UI.Inventory
{
    public class InventoryItemMoveService
    {
        private readonly int _playerId;
        
        private readonly BlockInventoryDataCache _blockInventoryDataCache;
        private readonly InventoryDataCache _inventoryDataCache;
        
        private readonly SendBlockInventoryMoveItemProtocol _blockInventoryMove;
        private readonly SendBlockInventoryPlayerInventoryMoveItemProtocol _blockInventoryPlayerInventoryMove;
        private readonly SendPlayerInventoryMoveItemProtocol _playerInventoryMove;

        public InventoryItemMoveService(
            PlayerConnectionSetting setting, 
            BlockInventoryDataCache blockInventoryDataCache, InventoryDataCache inventoryDataCache, 
            SendBlockInventoryMoveItemProtocol blockInventoryMove, 
            SendBlockInventoryPlayerInventoryMoveItemProtocol blockInventoryPlayerInventoryMove, 
            SendPlayerInventoryMoveItemProtocol playerInventoryMove)
        {
            _playerId = setting.PlayerId;
            _blockInventoryDataCache = blockInventoryDataCache;
            _inventoryDataCache = inventoryDataCache;
            _blockInventoryMove = blockInventoryMove;
            _blockInventoryPlayerInventoryMove = blockInventoryPlayerInventoryMove;
            _playerInventoryMove = playerInventoryMove;
        }

        private Vector2Int _blockPosition;
        public void SetBlockPosition(int x,int y) { _blockPosition = new Vector2Int(x,y);}
        public void MoveAllItemStack(int fromSlot,bool fromIsBlock, int toSlot, bool toIsBlock)
        {
            var count = GetItemStackCount(fromSlot,fromIsBlock);
            SendItemMove(fromSlot, fromIsBlock, toSlot, toIsBlock,count);
        }
        public void MoveHalfItemStack(int fromSlot,bool fromIsBlock, int toSlot, bool toIsBlock)
        {
            var count = GetItemStackCount(fromSlot,fromIsBlock);
            SendItemMove(fromSlot, fromIsBlock, toSlot, toIsBlock,count/2);
        }
        public void MoveOneItemStack(int fromSlot,bool fromIsBlock, int toSlot, bool toIsBlock)
        {
            SendItemMove(fromSlot, fromIsBlock, toSlot, toIsBlock,1);
        }
        
        private int GetItemStackCount(int slot,bool isBlock)
        {
            if (isBlock) return _blockInventoryDataCache.GetItemStack(slot).Count;
            
            return _inventoryDataCache.GetItemStack(slot).Count;
        }

        private void SendItemMove(int fromSlot, bool fromIsBlock, int toSlot, bool toIsBlock, int itemCount)
        {
            //ブロック内のアイテムの移動
            if (fromIsBlock && toIsBlock)
            {
                _blockInventoryMove.Send(_blockPosition,fromSlot,toSlot,itemCount);
                return;
            }

            //プレイヤーインベントリ内のアイテムの移動
            if (!fromIsBlock && !toIsBlock)
            {
                _playerInventoryMove.Send(_playerId,fromSlot,toSlot,itemCount);
                return;
            }
            
            //プレイヤーインベントリとブロックインベントリのアイテムの移動
            _blockInventoryPlayerInventoryMove.Send(_playerId,toIsBlock,_blockPosition,fromSlot,toSlot,itemCount);
        }
    }
}