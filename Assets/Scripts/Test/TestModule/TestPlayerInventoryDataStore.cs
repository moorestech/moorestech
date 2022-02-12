using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;

namespace Test.TestModule
{
    public class TestPlayerInventoryDataStore
    {
        public Dictionary<int,List<ItemStack>> playerInventory = new Dictionary<int, List<ItemStack>>();

        public TestPlayerInventoryDataStore(PlayerInventoryUpdateEvent @event)
        {
            @event.Subscribe(OnPlayerInventoryUpdate,OnPlayerInventorySlotUpdate);
        }

        private void OnPlayerInventoryUpdate(OnPlayerInventoryUpdateProperties properties)
        {
            var playerId = properties.PlayerId;
            var items = properties.ItemStacks;
            
            if (playerInventory.ContainsKey(playerId))
            {
                playerInventory[playerId] = items;
            }
            else
            {
                playerInventory.Add(playerId,items);
            }
        }

        private void OnPlayerInventorySlotUpdate(OnPlayerInventorySlotUpdateProperties properties)
        {
            var slot = properties.SlotId;
            var item = properties.ItemStack;

            foreach (var player in playerInventory)
            {
                player.Value[slot] = item;
            }
        }
        
    }
}