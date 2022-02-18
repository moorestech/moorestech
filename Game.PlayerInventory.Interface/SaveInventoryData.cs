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

        public (List<IItemStack>,List<IItemStack>) GetPlayerInventoryData(ItemStackFactory itemStackFactory)
        {
            var mainItemStack = new List<IItemStack>();
            for (var i = 0; i < MainItemId.Count; i++)
            {
                mainItemStack.Add(itemStackFactory.Create(MainItemId[i], MainItemCount[i]));
            }
            var craftItemStack = new List<IItemStack>();
            for (var i = 0; i < CraftItemId.Count; i++)
            {
                craftItemStack.Add(itemStackFactory.Create(CraftItemId[i], CraftItemCount[i]));
            }
            return (mainItemStack, craftItemStack);
        }

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
                CraftItemId.Add(playerInventoryData.CraftingInventory.GetItem(i).Id);
                CraftItemCount.Add(playerInventoryData.CraftingInventory.GetItem(i).Count);
            }
            
            
            
            PlayerId = playerId;
        }
    }
}