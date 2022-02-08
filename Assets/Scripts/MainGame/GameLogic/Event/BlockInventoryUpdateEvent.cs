using MainGame.UnityView.Interface;
using static MainGame.UnityView.Interface.IBlockInventoryUpdateEvent;

namespace MainGame.GameLogic.Event
{
    public class BlockInventoryUpdateEvent : IBlockInventoryUpdateEvent
    {
        
        public void Subscribe(InventoryUpdate inventoryUpdate, OpenInventory openInventory)
        {
            
        }
    }
}