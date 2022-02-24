using MainGame.GameLogic.Inventory;
using MainGame.Network;
using MainGame.Network.Send;
using UnityEngine;

namespace MainGame.Control.UI.Inventory.ItemMove
{
    /// <summary>
    /// プレイヤーインベントリ、ブロックインベントリの間でアイテムを移動する際に使用するサービスクラス
    /// </summary>
    public class BlockInventoryMainInventoryItemMoveService
    {
        private readonly int _playerId;
        
        private readonly BlockInventoryDataCache _blockInventoryDataCache;
        private readonly MainInventoryDataCache _mainInventoryDataCache;
        
        private readonly SendBlockInventoryMoveItemProtocol _blockInventoryMove;
        private readonly SendBlockInventoryMainInventoryMoveItemProtocol _blockInventoryMainInventoryMove;
        private readonly SendMainInventoryMoveItemProtocol _mainInventoryMove;

        public BlockInventoryMainInventoryItemMoveService(
            PlayerConnectionSetting setting, 
            BlockInventoryDataCache blockInventoryDataCache, MainInventoryDataCache mainInventoryDataCache, 
            SendBlockInventoryMoveItemProtocol blockInventoryMove, 
            SendBlockInventoryMainInventoryMoveItemProtocol blockInventoryMainInventoryMove, 
            SendMainInventoryMoveItemProtocol mainInventoryMove)
        {
            _playerId = setting.PlayerId;
            _blockInventoryDataCache = blockInventoryDataCache;
            _mainInventoryDataCache = mainInventoryDataCache;
            _blockInventoryMove = blockInventoryMove;
            _blockInventoryMainInventoryMove = blockInventoryMainInventoryMove;
            _mainInventoryMove = mainInventoryMove;
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
            
            return _mainInventoryDataCache.GetItemStack(slot).Count;
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
                _mainInventoryMove.Send(_playerId,fromSlot,toSlot,itemCount);
                return;
            }
            
            //プレイヤーインベントリとブロックインベントリのアイテムの移動
            _blockInventoryMainInventoryMove.Send(_playerId,toIsBlock,_blockPosition,fromSlot,toSlot,itemCount);
        }
    }
}