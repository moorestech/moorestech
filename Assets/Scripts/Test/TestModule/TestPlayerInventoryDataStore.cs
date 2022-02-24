using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;

namespace Test.TestModule
{
    public class TestPlayerInventoryDataStore
    {
        public Dictionary<int,List<ItemStack>> playerInventory = new Dictionary<int, List<ItemStack>>();

        public TestPlayerInventoryDataStore(IMainInventoryUpdateEvent mainInventory)
        {
            mainInventory.Subscribe(OnPlayerInventoryUpdate,OnPlayerInventorySlotUpdate);
        }

        private void OnPlayerInventoryUpdate(MainInventoryUpdateProperties properties)
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

        private void OnPlayerInventorySlotUpdate(MainInventorySlotUpdateProperties properties)
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