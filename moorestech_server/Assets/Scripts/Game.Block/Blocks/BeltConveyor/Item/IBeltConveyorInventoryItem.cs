using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Newtonsoft.Json;

namespace Game.Block.Blocks.BeltConveyor
{
    public interface IOnBeltConveyorItem
    {
        public double RemainingPercent { get; }
        public ItemId ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
    }
    
    public class VanillaBeltConveyorInventoryItem : IOnBeltConveyorItem
    {
        public double RemainingPercent { get; set; }
        public ItemId ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        
        public VanillaBeltConveyorInventoryItem(ItemId itemId, ItemInstanceId itemInstanceId)
        {
            ItemId = itemId;
            ItemInstanceId = itemInstanceId;
            RemainingPercent = 1;
        }
        
        public string GetSaveJsonString()
        {
            return JsonConvert.SerializeObject(new VanillaBeltConveyorInventoryItemJsonObject(this));
        }
        
        public static VanillaBeltConveyorInventoryItem LoadItem(string jsonString)
        {
            if (jsonString == null) return null;
            
            var jsonData = JsonConvert.DeserializeObject<VanillaBeltConveyorInventoryItemJsonObject>(jsonString);
            if (jsonData.ItemStack == null) return null;
            
            var itemId = MasterHolder.ItemMaster.GetItemId(jsonData.ItemStack.ItemGuid);
            var remainingPercent = jsonData.RemainingPercent;
            var itemInstanceId = ItemInstanceId.Create();
            
            return new VanillaBeltConveyorInventoryItem(itemId, itemInstanceId)
            {
                RemainingPercent = remainingPercent
            };
        }
    }
    
    public class VanillaBeltConveyorInventoryItemJsonObject
    {
        [JsonProperty("itemStack")] public ItemStackSaveJsonObject ItemStack;
        
        [JsonProperty("remainingTime")] public double RemainingPercent;
        
        public VanillaBeltConveyorInventoryItemJsonObject(VanillaBeltConveyorInventoryItem vanillaBeltConveyorInventoryItem)
        {
            if (vanillaBeltConveyorInventoryItem == null)
            {
                ItemStack = null;
                RemainingPercent = 1;
                return;
            }
            
            var item = ServerContext.ItemStackFactory.Create(vanillaBeltConveyorInventoryItem.ItemId, 1);
            ItemStack = new ItemStackSaveJsonObject(item);
            RemainingPercent = vanillaBeltConveyorInventoryItem.RemainingPercent;
        }
    }
}