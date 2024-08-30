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
        
        [JsonProperty("MainItemId")] public List<string> MainItemGuIds;
        [JsonProperty("MainItemCount")] public List<int> MainItemCounts;
        
        [JsonProperty("GrabItemId")] public string GrabItemGuid;
        [JsonProperty("GrabItemCount")] public int GrabItemCount;
        
        public PlayerInventorySaveJsonObject()
        {
        }
        
        public PlayerInventorySaveJsonObject(int playerId, PlayerInventoryData playerInventoryData)
        {
            MainItemGuIds = new List<string>();
            MainItemCounts = new List<int>();
            for (var i = 0; i < playerInventoryData.MainOpenableInventory.GetSlotSize(); i++)
            {
                var item = playerInventoryData.MainOpenableInventory.GetItem(i);
                var master = ItemMaster.GetItemMaster(item.Id);
                MainItemGuIds.Add(master.ItemGuid.ToString());
                MainItemCounts.Add(item.Count);
            }
            
            var grabItem = playerInventoryData.GrabInventory.GetItem(0);
            var grabItemMaster = ItemMaster.GetItemMaster(grabItem.Id);
            GrabItemGuid = grabItemMaster.ItemGuid.ToString();
            GrabItemCount = grabItem.Count;
            
            
            PlayerId = playerId;
        }
        
        public (List<IItemStack> mainInventory, IItemStack grabItem) GetPlayerInventoryData()
        {
            var mainItemStack = new List<IItemStack>();
            for (var i = 0; i < MainItemGuIds.Count; i++)
            {
                var item = ServerContext.ItemStackFactory.Create(new Guid(MainItemGuIds[i]), MainItemCounts[i]);
                mainItemStack.Add(item);
            }
            
            var grabItem = ServerContext.ItemStackFactory.Create(new Guid(GrabItemGuid), GrabItemCount);
            return (mainItemStack, grabItem);
        }
    }
}