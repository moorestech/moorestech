using System;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.BlockConnectInfoModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.BeltConveyor
{
    public interface IOnBeltConveyorItem
    {
        public double RemainingPercent { get; }
        public ItemId ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public BlockConnectInfoElement StartConnector { get; }
        public BlockConnectInfoElement GoalConnector { get; }
    }

    public class VanillaBeltConveyorInventoryItem : IOnBeltConveyorItem
    {
        public double RemainingPercent { get; set; }
        public ItemId ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public BlockConnectInfoElement StartConnector { get; }
        public BlockConnectInfoElement GoalConnector { get; private set; }

        public VanillaBeltConveyorInventoryItem(ItemId itemId, ItemInstanceId itemInstanceId, BlockConnectInfoElement startConnector, BlockConnectInfoElement goalConnector)
        {
            ItemId = itemId;
            ItemInstanceId = itemInstanceId;
            StartConnector = startConnector;
            GoalConnector = goalConnector;
            RemainingPercent = 1;
        }

        /// <summary>
        /// GoalConnectorとGuidを更新
        /// Update GoalConnector and Guid
        /// </summary>
        public void SetGoalConnector(BlockConnectInfoElement goalConnector)
        {
            GoalConnector = goalConnector;
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
            
            var item = new VanillaBeltConveyorInventoryItem(itemId, itemInstanceId, null, null)
            {
                RemainingPercent = remainingPercent
            };
            return item;
        }
    }

    public class VanillaBeltConveyorInventoryItemJsonObject
    {
        [JsonProperty("itemStack")] public ItemStackSaveJsonObject ItemStack;

        [JsonProperty("remainingTime")] public double RemainingPercent;
        
        [JsonProperty("sourceConnectorGuid")] public Guid? SourceConnectorGuid;
        
        [JsonProperty("goalConnectorGuid")] public Guid? GoalConnectorGuid;

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
            SourceConnectorGuid = vanillaBeltConveyorInventoryItem.GetStartConnectorGuid();
            GoalConnectorGuid = vanillaBeltConveyorInventoryItem.GetGoalConnectorGuid();
        }
    }
}
