using System.Collections.Generic;
using MainGame.Network.Interface;
using Maingame.Types;

namespace Test.TestModule
{
    public class TestPlayerInventoryDataStore
    {
        public Dictionary<int,List<ItemStack>> playerInventory = new Dictionary<int, List<ItemStack>>();

        public TestPlayerInventoryDataStore(IPlayerInventoryUpdateEvent @event)
        {
            @event.Subscribe(OnPlayerInventoryUpdate,OnPlayerInventorySlotUpdate);
        }

        private void OnPlayerInventoryUpdate(OnPlayerInventoryUpdateProperties properties)
        {
            
        }

        private void OnPlayerInventorySlotUpdate(OnPlayerInventorySlotUpdateProperties properties)
        {
            
        }
        
    }
}