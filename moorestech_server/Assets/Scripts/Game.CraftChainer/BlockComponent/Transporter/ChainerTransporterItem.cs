using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Context;
using Game.CraftChainer.CraftNetwork;
using Newtonsoft.Json;

namespace Game.CraftChainer.BlockComponent
{
    public class ChainerTransporterItem : IBeltConveyorInventoryItem
    {
        public double RemainingPercent { get; set; }
        
        public CraftChainerNodeId TargetNodeId { get; }
        public ItemId ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        
        public string GetSaveJsonString()
        {
            return JsonConvert.SerializeObject(new ChainerTransporterItemJsonObject(this));
        }
        
        public ChainerTransporterItem(ItemInstanceId itemInstanceId, ItemId itemId, CraftChainerNodeId targetNodeId)
        {
            ItemInstanceId = itemInstanceId;
            ItemId = itemId;
            TargetNodeId = targetNodeId;
        }
    }
    
    public class ChainerTransporterItemJsonObject
    {
        [JsonProperty("itemStack")] public ItemStackSaveJsonObject ItemStack;
        
        [JsonProperty("remainingTime")] public double RemainingPercent;
        
        [JsonProperty("targetNodeId")] public int TargetNodeId;
        
        public ChainerTransporterItemJsonObject(ChainerTransporterItem transporterItem)
        {
            if (transporterItem == null)
            {
                ItemStack = null;
                RemainingPercent = 1;
                TargetNodeId = -1;
                return;
            }
            
            var item = ServerContext.ItemStackFactory.Create(transporterItem.ItemId, 1);
            ItemStack = new ItemStackSaveJsonObject(item);
            RemainingPercent = transporterItem.RemainingPercent;
            TargetNodeId = transporterItem.TargetNodeId.AsPrimitive();
        }
    }
}