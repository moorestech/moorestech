using System;
using Core.Master;
using Newtonsoft.Json;

namespace Game.CraftChainer.CraftChain
{
    public class CraftingSolverItem
    {
        public readonly ItemId ItemId;
        public readonly int Quantity;
        
        public CraftingSolverItem(ItemId itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }
    }
    
    public class CraftingSolverItemJsonObject
    {
        [JsonProperty("itemGuid")] public string ItemGuid;
        [JsonProperty("quantity")] public int Quantity;
        
        public CraftingSolverItemJsonObject(CraftingSolverItem craftingSolverItem)
        {
            ItemGuid = MasterHolder.ItemMaster.GetItemMaster(craftingSolverItem.ItemId).ItemGuid.ToString();
            Quantity = craftingSolverItem.Quantity;
        }
        
        public CraftingSolverItem ToCraftingSolverItem()
        {
            var guid = new Guid(ItemGuid);
            var itemId = MasterHolder.ItemMaster.GetItemId(guid);
            return new CraftingSolverItem(itemId, Quantity);
        }
    }
}