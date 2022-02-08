using System.Collections.Generic;
using Maingame.Types;

namespace MainGame.Network.Interface.Receive
{
    public interface IBlockInventoryUpdateEvent
    {
        public delegate void OnBlockInventorySlotUpdate(int x,int y,int slot,int id,int count);
        public delegate void OnBlockInventoryUpdate(int x,int y,List<ItemStack> items,string uiType);
        
        public void Subscribe(OnBlockInventorySlotUpdate onBlockInventorySlot,OnBlockInventoryUpdate onBlockUpdate);
    }
}