using System.Collections.Generic;
using MainGame.GameLogic.Event;
using MainGame.Network.Interface.Receive;
using Maingame.Types;
using UnityEngine;
using IBlockInventoryUpdateEvent = MainGame.UnityView.Interface.IBlockInventoryUpdateEvent;

namespace MainGame.GameLogic.Inventory
{
    public class BlockInventoryDataCache
    {
        private readonly BlockInventoryUpdateEvent _blockInventoryView;
        
        private Vector2Int _openingPos;
        public BlockInventoryDataCache(IBlockInventoryUpdateEvent blockInventoryView,IReceiveBlockInventoryUpdateEvent blockInventory)
        {
            _blockInventoryView = blockInventoryView as BlockInventoryUpdateEvent;
            blockInventory.Subscribe(OnBlockInventorySlotUpdate,OnBlockInventoryUpdate);
        }

        private void OnBlockInventorySlotUpdate(Vector2Int pos,int slot,int id,int count)
        {
            if (_openingPos != pos) return;
            
            _blockInventoryView.OnInventoryUpdateInvoke(slot,id,count);
        }

        private void OnBlockInventoryUpdate(Vector2Int pos,List<ItemStack> items,string uiType,params short[] uiParams)
        {
            _openingPos = pos;
            //UIを開く
            _blockInventoryView.OnSettingInventoryInvoke(uiType,uiParams);
            //UIを更新する
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                _blockInventoryView.OnInventoryUpdateInvoke(i,item.ID,item.Count);
            }
        }
        
    }
}