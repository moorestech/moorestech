using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;

namespace MainGame.Model.DataStore.Inventory
{
    public class BlockInventoryDataCache
    {
        private List<ItemStack> _itemStackList;


        public BlockInventoryDataCache(IBlockInventoryUpdateEvent blockInventory)
        {
            blockInventory.Subscribe(OnBlockInventorySlotUpdate,OnSettingBlockInventory);
        }

        private void OnBlockInventorySlotUpdate(BlockInventorySlotUpdateProperties properties)
        {
            var slot = properties.Slot;
            var id = properties.Id;
            var count = properties.Count;
            
            
           
            //todo イベントにする_blockInventoryItemView.BlockInventoryUpdate(slot,id,count);
            
            if (slot < _itemStackList.Count)
            {
                _itemStackList[slot] = new ItemStack(id,count);
            }
        }

        private void OnSettingBlockInventory(SettingBlockInventoryProperties onSettingBlock)
        {
            var items = onSettingBlock.items;
            _itemStackList = items;
            
            //todo イベントにする _blockInventoryItemView.SettingBlockInventory(onSettingBlock.uiType,onSettingBlock.blockId,onSettingBlock.uiParams);
            //todo イベントにする_blockInventoryItemView.BlockInventoryUpdate(i,item.ID,item.Count);
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