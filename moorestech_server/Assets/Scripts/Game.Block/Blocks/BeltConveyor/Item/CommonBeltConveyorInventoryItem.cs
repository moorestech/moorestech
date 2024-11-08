using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Newtonsoft.Json;

namespace Game.Block.Blocks.BeltConveyor
{
    public class CommonBeltConveyorInventoryItem : IBeltConveyorInventoryItem
    {
        public double RemainingPercent { get; set; }
        public ItemId ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        
        public CommonBeltConveyorInventoryItem(ItemId itemId, ItemInstanceId itemInstanceId)
        {
            ItemId = itemId;
            ItemInstanceId = itemInstanceId;
            RemainingPercent = 1;
        }
        
        public string GetSaveJsonString()
        {
            return JsonConvert.SerializeObject(new CommonBeltConveyorInventoryItemJsonObject(this));
        }
    }
    
    public class CommonBeltConveyorInventoryItemJsonObject
    {
        [JsonProperty("itemStack")] public ItemStackSaveJsonObject ItemStack;
        
        [JsonProperty("remainingTime")] public double RemainingPercent;
        
        public CommonBeltConveyorInventoryItemJsonObject(CommonBeltConveyorInventoryItem commonBeltConveyorInventoryItem)
        {
            if (commonBeltConveyorInventoryItem == null)
            {
                ItemStack = null;
                RemainingPercent = 1;
                return;
            }
            
            var item = ServerContext.ItemStackFactory.Create(commonBeltConveyorInventoryItem.ItemId, 1);
            ItemStack = new ItemStackSaveJsonObject(item);
            RemainingPercent = commonBeltConveyorInventoryItem.RemainingPercent;
        }
    }
}