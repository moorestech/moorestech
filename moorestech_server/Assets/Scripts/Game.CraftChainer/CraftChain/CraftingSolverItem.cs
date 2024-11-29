using System;
using Core.Master;
using MessagePack;
using Newtonsoft.Json;

namespace Game.CraftChainer.CraftChain
{
    public class CraftingSolverItem
    {
        public readonly ItemId ItemId;
        public readonly int Count;
        
        public CraftingSolverItem(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
    
    [JsonObject, MessagePackObject]
    public class CraftingSolverItemJsonObjectMessagePack
    {
        [JsonProperty("itemGuid"), Key(0)] public string ItemGuid;
        [JsonProperty("count"), Key(1)] public int Count;
        
        public CraftingSolverItemJsonObjectMessagePack() { }
        public CraftingSolverItemJsonObjectMessagePack(CraftingSolverItem craftingSolverItem)
        {
            ItemGuid = MasterHolder.ItemMaster.GetItemMaster(craftingSolverItem.ItemId).ItemGuid.ToString();
            Count = craftingSolverItem.Count;
        }
        
        public CraftingSolverItem ToCraftingSolverItem()
        {
            var guid = new Guid(ItemGuid);
            var itemId = MasterHolder.ItemMaster.GetItemId(guid);
            return new CraftingSolverItem(itemId, Count);
        }
    }
}