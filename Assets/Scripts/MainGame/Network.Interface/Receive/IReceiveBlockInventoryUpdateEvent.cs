using System.Collections.Generic;
using MainGame.Basic;
using UnityEngine;

namespace MainGame.Network.Interface.Receive
{
    public interface IReceiveBlockInventoryUpdateEvent
    {
        public delegate void OnBlockInventorySlotUpdate(Vector2Int pos,int slot,int id,int count);
        public delegate void OnSettingBlockInventory(List<ItemStack> items,string uiType,params short[] uiParams);
        
        public void Subscribe(OnBlockInventorySlotUpdate onBlockInventorySlot,OnSettingBlockInventory onSettingBlock);
    }
}