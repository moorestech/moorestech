using System.Collections.Generic;
using MainGame.Constant;
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
            var playerId = properties.PlayerId;
            var slot = properties.SlotId;
            var item = properties.ItemStack;
            
            if (playerInventory.ContainsKey(playerId))
            {
                playerInventory[playerId][slot] = item;
            }
            else
            {
                var items = new List<ItemStack>();
                for (int i = 0; i < PlayerInventory.MainInventorySize; i++)
                {
                    items.Add(new ItemStack(
                        ItemConstant.NullItemId,
                        ItemConstant.NullItemCount));
                }

                items[slot] = item;
                playerInventory.Add(playerId,items);
            }
        }
        
    }
}