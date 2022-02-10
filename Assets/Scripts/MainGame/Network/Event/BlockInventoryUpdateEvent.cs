using System.Collections.Generic;
using MainGame.Basic;
using UnityEngine;

namespace MainGame.Network.Event
{
    //TODO この辺は共通UI基盤に移行する
    public class BlockInventoryUpdateEvent 
    {
        public delegate void BlockInventorySlotUpdate(Vector2Int pos,int slot,int id,int count);
        public delegate void SettingBlockInventory(List<ItemStack> items,string uiType,params short[] uiParams);
        private event BlockInventorySlotUpdate OnBlockInventorySlotUpdate;
        private event SettingBlockInventory OnSettingBlock;
        public void Subscribe(BlockInventorySlotUpdate onBlockInventorySlot, SettingBlockInventory onSettingBlock)
        {
            OnBlockInventorySlotUpdate += onBlockInventorySlot;
            OnSettingBlock += onSettingBlock;
        }

        public void OnOnSettingBlock(List<ItemStack> items, string uitype, params short[] uiparams)
        {
            OnSettingBlock?.Invoke(items, uitype, uiparams);
        }

        public void OnOnBlockInventorySlotUpdate(Vector2Int pos, int slot, int id, int count)
        {
            OnBlockInventorySlotUpdate?.Invoke(pos, slot, id, count);
        }
    }
}