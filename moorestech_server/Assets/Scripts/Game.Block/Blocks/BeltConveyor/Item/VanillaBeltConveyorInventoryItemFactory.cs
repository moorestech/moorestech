using Core.Item.Interface;
using Core.Master;
using Newtonsoft.Json;

namespace Game.Block.Blocks.BeltConveyor
{
    public class VanillaBeltConveyorInventoryItemFactory : IBeltConveyorItemFactory
    {
        public IBeltConveyorInventoryItem CreateItem(ItemId itemId, ItemInstanceId itemInstanceId)
        {
            return new VanillaBeltConveyorInventoryItem(itemId, itemInstanceId);
        }
        
        public IBeltConveyorInventoryItem LoadItem(string jsonString)
        {
            var jsonObject = JsonConvert.DeserializeObject<CommonBeltConveyorInventoryItemJsonObject>(jsonString);
            if (jsonObject.ItemStack == null)
                return null;
            
            var itemId = MasterHolder.ItemMaster.GetItemId(jsonObject.ItemStack.ItemGuid);
            var itemInstanceId = ItemInstanceId.Create();
            
            return new VanillaBeltConveyorInventoryItem(itemId, itemInstanceId)
            {
                RemainingPercent = jsonObject.RemainingPercent
            };
        }
    }
}