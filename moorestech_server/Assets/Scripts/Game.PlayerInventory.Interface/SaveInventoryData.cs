using System.Collections.Generic;
using Core.Item.Interface;
using Game.Context;
using Newtonsoft.Json;

namespace Game.PlayerInventory.Interface
{
    public class SaveInventoryData
    {
        [JsonProperty("GrabItemCount")] public int GrabItemCount;
        [JsonProperty("GrabItemId")] public int GrabItemId;
        [JsonProperty("MainItemCount")] public List<int> MainItemCount;
        [JsonProperty("MainItemId")] public List<int> MainItemId;
        [JsonProperty("PlayerId")] public int PlayerId;

        public SaveInventoryData()
        {
        }

        public SaveInventoryData(int playerId, PlayerInventoryData playerInventoryData)
        {
            MainItemId = new List<int>();
            MainItemCount = new List<int>();
            for (var i = 0; i < playerInventoryData.MainOpenableInventory.GetSlotSize(); i++)
            {
                MainItemId.Add(playerInventoryData.MainOpenableInventory.GetItem(i).Id);
                MainItemCount.Add(playerInventoryData.MainOpenableInventory.GetItem(i).Count);
            }

            GrabItemId = playerInventoryData.GrabInventory.GetItem(0).Id;
            GrabItemCount = playerInventoryData.GrabInventory.GetItem(0).Count;


            PlayerId = playerId;
        }

        public (List<IItemStack> mainInventory, IItemStack grabItem) GetPlayerInventoryData()
        {
            var mainItemStack = new List<IItemStack>();
            for (var i = 0; i < MainItemId.Count; i++)
            {
                var item = ServerContext.ItemStackFactory.Create(MainItemId[i], MainItemCount[i]);
                mainItemStack.Add(item);
            }
            var grabItem = ServerContext.ItemStackFactory.Create(GrabItemId, GrabItemCount);
            return (mainItemStack, grabItem);
        }
    }
}