using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.BeltConveyor
{
    public interface IOnBeltConveyorItem
    {
        public double RemainingPercent { get; }
        public ItemId ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public BlockConnectorInfo StartConnector { get; }
        public BlockConnectorInfo GoalConnector { get; }
    }

    public class VanillaBeltConveyorInventoryItem : IOnBeltConveyorItem
    {
        public double RemainingPercent { get; set; }
        public ItemId ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public BlockConnectorInfo StartConnector { get; }
        public BlockConnectorInfo GoalConnector { get; private set; }

        public VanillaBeltConveyorInventoryItem(ItemId itemId, ItemInstanceId itemInstanceId, BlockConnectorInfo startConnector, BlockConnectorInfo goalConnector)
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
        public void SetGoalConnector(BlockConnectorInfo goalConnector)
        {
            GoalConnector = goalConnector;
        }

        public string GetSaveJsonString()
        {
            return JsonConvert.SerializeObject(new VanillaBeltConveyorInventoryItemJsonObject(this));
        }
        
        public static VanillaBeltConveyorInventoryItem LoadItem(string jsonString, InventoryConnects inventoryConnectors)
        {
            if (jsonString == null) return null;

            var jsonData = JsonConvert.DeserializeObject<VanillaBeltConveyorInventoryItemJsonObject>(jsonString);
            if (jsonData.ItemStack == null) return null;

            var itemId = MasterHolder.ItemMaster.GetItemId(jsonData.ItemStack.ItemGuid);
            var remainingPercent = jsonData.RemainingPercent;
            var itemInstanceId = ItemInstanceId.Create();
            
            var inputConnectors = BlockConnectorInfoFactory.FromConnectors(inventoryConnectors.InputConnects);
            var outputConnectors = BlockConnectorInfoFactory.FromConnectors(inventoryConnectors.OutputConnects);
            var startConnector = FindBlockConnectInfoElementByGuid(jsonData.SourceConnectorGuid, inputConnectors);
            var goalConnector = FindBlockConnectInfoElementByGuid(jsonData.GoalConnectorGuid, outputConnectors);
            
            var item = new VanillaBeltConveyorInventoryItem(itemId, itemInstanceId, startConnector, goalConnector)
            {
                RemainingPercent = remainingPercent
            };
            return item;
            
            #region Internal
            
            BlockConnectorInfo FindBlockConnectInfoElementByGuid(Guid? guid, IEnumerable<BlockConnectorInfo> connectInfos)
            {
                if (connectInfos == null) return null;
                foreach (var connectInfo in connectInfos)
                {
                    if (connectInfo.ConnectorGuid == guid)
                    {
                        return connectInfo;
                    }
                }

                return null;
            }
            
            #endregion
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
            SourceConnectorGuid = vanillaBeltConveyorInventoryItem.StartConnector?.ConnectorGuid;
            GoalConnectorGuid = vanillaBeltConveyorInventoryItem.GoalConnector?.ConnectorGuid;
        }
    }
}
