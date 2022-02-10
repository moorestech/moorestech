using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;

namespace MainGame.GameLogic.Inventory
{
    public class BlockInventoryDataCache
    {
        private readonly BlockInventoryItemView _blockInventoryItemView;
        private List<ItemStack> _itemStackList;


        public BlockInventoryDataCache(BlockInventoryUpdateEvent blockInventory,BlockInventoryItemView blockInventoryItemView)
        {
            _blockInventoryItemView = blockInventoryItemView;
            blockInventory.Subscribe(OnBlockInventorySlotUpdate,OnSettingBlockInventory);
        }

        private void OnBlockInventorySlotUpdate(Vector2Int pos,int slot,int id,int count)
        {
            _blockInventoryItemView.BlockInventoryUpdate(slot,id,count);
            if (slot < _itemStackList.Count)
            {
                _itemStackList[slot] = new ItemStack(id,count);
            }
        }

        private void OnSettingBlockInventory(List<ItemStack> items,string uiType,params short[] uiParams)
        {
            _itemStackList = items;
            //UIを開く
            _blockInventoryItemView.OpenBlockInventory(uiType,uiParams);
            //UIを更新する
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                _blockInventoryItemView.BlockInventoryUpdate(i,item.ID,item.Count);
            }
        }
        
        public ItemStack GetItemStack(int slot)
        {
            if (slot < _itemStackList.Count)
            {
                return _itemStackList[slot];
            }
            return new ItemStack();
        }
        
    }
}