using System.Collections.Generic;
using Maingame.Types;
using UnityEngine;

namespace MainGame.Network.Interface.Receive
{
    public interface IReceiveBlockInventoryUpdateEvent
    {
        public delegate void OnBlockInventorySlotUpdate(Vector2Int pos,int slot,int id,int count);
        public delegate void OnBlockInventoryUpdate(Vector2Int pos,List<ItemStack> items,string uiType,params short[] uiParams);
        
        public void Subscribe(OnBlockInventorySlotUpdate onBlockInventorySlot,OnBlockInventoryUpdate onBlockUpdate);
    }
}