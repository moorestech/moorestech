using System;
using System.Collections.Generic;
using Core.Item;
using Newtonsoft.Json;

namespace Game.PlayerInventory.Interface
{
    public class SaveInventoryData
    {
        [JsonProperty("MainItemId")] public List<int> MainItemId { get; }
        [JsonProperty("MainItemCount")] public List<int> MainItemCount { get; }
        [JsonProperty("CraftItemId")] public List<int> CraftItemId { get; }
        [JsonProperty("CraftItemCount")] public List<int> CraftItemCount { get; }
        [JsonProperty("PlayerId")] public int PlayerId { get; }

        public SaveInventoryData(int playerId, PlayerInventoryData playerInventoryData)
        {
            MainItemId = new ();
            MainItemCount = new ();
            for (int i = 0; i < playerInventoryData.MainInventory.GetSlotSize(); i++)
            {
                MainItemId.Add(playerInventoryData.MainInventory.GetItem(i).Id);
                MainItemCount.Add(playerInventoryData.MainInventory.GetItem(i).Count);
            }
            
            CraftItemId = new();
            CraftItemCount = new();
            for (int i = 0; i < playerInventoryData.CraftingInventory.GetSlotSize(); i++)
            {
                MainItemId.Add(playerInventoryData.CraftingInventory.GetItem(i).Id);
                MainItemCount.Add(playerInventoryData.CraftingInventory.GetItem(i).Count);
            }
            
            
            
            PlayerId = playerId;
        }
    }
}