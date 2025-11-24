using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Newtonsoft.Json;

namespace Game.PlayerInventory.Interface
{
    public class PlayerInventorySaveJsonObject
    {
        [JsonProperty("PlayerId")] public int PlayerId;
        
        [JsonProperty("MainInventoryItems")] public List<ItemStackSaveJsonObject> MainInventoryItems;
        
        [JsonProperty("GrabInventoryItems")] public ItemStackSaveJsonObject GrabInventoryItem;
        
        public PlayerInventorySaveJsonObject()
        {
        }
        
        public PlayerInventorySaveJsonObject(int playerId, PlayerInventoryData playerInventoryData)
        {
            MainInventoryItems = new List<ItemStackSaveJsonObject>();
            for (var i = 0; i < playerInventoryData.MainOpenableInventory.GetSlotSize(); i++)
            {
                var item = playerInventoryData.MainOpenableInventory.GetItem(i);
                MainInventoryItems.Add(new ItemStackSaveJsonObject(item));
            }
            
            var grabItemStack = playerInventoryData.GrabInventory.GetItem(0);
            GrabInventoryItem = new ItemStackSaveJsonObject(grabItemStack);
            
            PlayerId = playerId;
        }
        
        public (List<IItemStack> mainInventory, IItemStack grabItem) GetPlayerInventoryData()
        {
            var mainItemStack = new List<IItemStack>();
            foreach (var items in MainInventoryItems)
            {
                mainItemStack.Add(items.ToItemStack());
            }
            var grabItem = GrabInventoryItem.ToItemStack();
            
            return (mainItemStack, grabItem);
        }
    }
}