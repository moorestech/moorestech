using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.PlayerInventory.Interface
{
    public class SaveInventoryData
    {
        [JsonProperty("ItemId")] public List<int> ItemId { get; }
        [JsonProperty("ItemCount")] public List<int> ItemCount { get; }
        [JsonProperty("PlayerGUID")] public string PlayerGuid { get; }

        public SaveInventoryData(List<int> itemId, List<int> itemCount,Guid playerGuid)
        {
            ItemId = itemId;
            ItemCount = itemCount;
            PlayerGuid = playerGuid.ToString();
        }
    }
}