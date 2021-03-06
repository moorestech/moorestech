using System;
using System.Collections.Generic;
using Core.Item;
using Newtonsoft.Json;

namespace Game.PlayerInventory.Interface
{
    public class SaveInventoryData
    {
        [JsonProperty("MainItemId")] public List<int> MainItemId;
        [JsonProperty("MainItemCount")] public List<int> MainItemCount;
        [JsonProperty("CraftItemId")] public List<int> CraftItemId;
        [JsonProperty("CraftItemCount")] public List<int> CraftItemCount;
        [JsonProperty("GrabItemId")] public int GrabItemId;
        [JsonProperty("GrabItemCount")] public int GrabItemCount;
        [JsonProperty("PlayerId")] public int PlayerId;

        public (List<IItemStack> mainInventory, List<IItemStack> craftInventory, IItemStack grabItem) GetPlayerInventoryData(ItemStackFactory itemStackFactory)
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
            return (mainItemStack, craftItemStack, itemStackFactory.Create(GrabItemId, GrabItemCount));
        }

        public SaveInventoryData(){}
        public SaveInventoryData(int playerId, PlayerInventoryData playerInventoryData)
        {
            MainItemId = new ();
            MainItemCount = new ();
            for (int i = 0; i < playerInventoryData.MainOpenableInventory.GetSlotSize(); i++)
            {
                MainItemId.Add(playerInventoryData.MainOpenableInventory.GetItem(i).Id);
                MainItemCount.Add(playerInventoryData.MainOpenableInventory.GetItem(i).Count);
            }
            
            CraftItemId = new();
            CraftItemCount = new();
            for (int i = 0; i < playerInventoryData.CraftingOpenableInventory.GetSlotSize(); i++)
            {
                CraftItemId.Add(playerInventoryData.CraftingOpenableInventory.GetItem(i).Id);
                CraftItemCount.Add(playerInventoryData.CraftingOpenableInventory.GetItem(i).Count);
            }
            
            GrabItemId = playerInventoryData.GrabInventory.GetItem(0).Id;
            GrabItemCount = playerInventoryData.GrabInventory.GetItem(0).Count;
            
            
            PlayerId = playerId;
        }
    }
}