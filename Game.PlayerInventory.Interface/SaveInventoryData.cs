using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.PlayerInventory.Interface
{
    public class SaveInventoryData
    {
        [JsonProperty("ItemId")] public List<int> ItemId { get; }
        [JsonProperty("ItemCount")] public List<int> ItemCount { get; }
        [JsonProperty("PlayerId")] public int PlayerId { get; }

        public SaveInventoryData(int playerId, List<int> itemId, List<int> itemCount)
        {
            ItemId = itemId;
            ItemCount = itemCount;
            PlayerId = playerId;
        }
    }
}