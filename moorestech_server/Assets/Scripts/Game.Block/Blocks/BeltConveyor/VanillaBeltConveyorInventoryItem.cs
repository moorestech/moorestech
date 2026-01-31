using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Context;
using Mooresmaster.Model.BlockConnectInfoModule;
using Mooresmaster.Model.InventoryConnectsModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.BeltConveyor
{
    public interface IOnBeltConveyorItem
    {
        public uint RemainingTicks { get; }
        public uint TotalTicks { get; }
        public ItemId ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public BlockConnectInfoElement StartConnector { get; }
        public BlockConnectInfoElement GoalConnector { get; }
    }

    public class VanillaBeltConveyorInventoryItem : IOnBeltConveyorItem
    {
        public uint RemainingTicks { get; set; }
        public uint TotalTicks { get; private set; }
        public ItemId ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public BlockConnectInfoElement StartConnector { get; }
        public BlockConnectInfoElement GoalConnector { get; private set; }

        public VanillaBeltConveyorInventoryItem(ItemId itemId, ItemInstanceId itemInstanceId, BlockConnectInfoElement startConnector, BlockConnectInfoElement goalConnector, uint totalTicks)
        {
            ItemId = itemId;
            ItemInstanceId = itemInstanceId;
            StartConnector = startConnector;
            GoalConnector = goalConnector;
            TotalTicks = totalTicks;
            RemainingTicks = totalTicks;
        }

        /// <summary>
        /// GoalConnectorとGuidを更新
        /// Update GoalConnector and Guid
        /// </summary>
        public void SetGoalConnector(BlockConnectInfoElement goalConnector)
        {
            GoalConnector = goalConnector;
        }

        /// <summary>
        /// 停止状態から復帰した際にTotalTicksとRemainingTicksをリセットする
        /// Reset TotalTicks and RemainingTicks when recovering from stopped state
        /// </summary>
        public void ResetTicksOnSpeedRecovery(uint newTotalTicks)
        {
            // 現在の進捗率を維持しながらtickを更新
            // Update ticks while maintaining current progress ratio
            if (TotalTicks == 0 || TotalTicks == uint.MaxValue)
            {
                // 停止中に投入されたアイテムは進捗0からスタート
                // Items inserted while stopped start from 0 progress
                TotalTicks = newTotalTicks;
                RemainingTicks = newTotalTicks;
            }
        }

        public string GetSaveJsonString()
        {
            return JsonConvert.SerializeObject(new VanillaBeltConveyorInventoryItemJsonObject(this));
        }

        /// <summary>
        /// JSONからアイテムをロードする
        /// Load item from JSON string
        /// </summary>
        /// <param name="jsonString">JSON文字列</param>
        /// <param name="inventoryConnectors">コネクター情報</param>
        /// <param name="totalTicks">ベルトコンベアの総tick数</param>
        public static VanillaBeltConveyorInventoryItem LoadItem(string jsonString, InventoryConnects inventoryConnectors, uint totalTicks)
        {
            if (jsonString == null) return null;

            var jsonData = JsonConvert.DeserializeObject<VanillaBeltConveyorInventoryItemJsonObject>(jsonString);
            if (jsonData.ItemStack == null) return null;

            var itemId = MasterHolder.ItemMaster.GetItemId(jsonData.ItemStack.ItemGuid);
            var itemInstanceId = ItemInstanceId.Create();

            // 残り秒数からtickに変換
            // Convert remaining seconds to ticks
            var remainingTicks = GameUpdater.SecondsToTicks(jsonData.RemainingSeconds);

            var startConnector = FindBlockConnectInfoElementByGuid(jsonData.SourceConnectorGuid, inventoryConnectors.InputConnects.items);
            var goalConnector = FindBlockConnectInfoElementByGuid(jsonData.GoalConnectorGuid, inventoryConnectors.OutputConnects.items);

            var item = new VanillaBeltConveyorInventoryItem(itemId, itemInstanceId, startConnector, goalConnector, totalTicks)
            {
                RemainingTicks = remainingTicks
            };
            return item;

            #region Internal

            BlockConnectInfoElement FindBlockConnectInfoElementByGuid(Guid? guid, BlockConnectInfoElement[] connectInfos)
            {
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

        // 秒数として保存（tick数の変動に対応）
        // Save as seconds (to handle tick rate changes)
        [JsonProperty("remainingSeconds")] public double RemainingSeconds;

        [JsonProperty("sourceConnectorGuid")] public Guid? SourceConnectorGuid;

        [JsonProperty("goalConnectorGuid")] public Guid? GoalConnectorGuid;

        public VanillaBeltConveyorInventoryItemJsonObject(VanillaBeltConveyorInventoryItem vanillaBeltConveyorInventoryItem)
        {
            if (vanillaBeltConveyorInventoryItem == null)
            {
                ItemStack = null;
                RemainingSeconds = 0;
                return;
            }

            var item = ServerContext.ItemStackFactory.Create(vanillaBeltConveyorInventoryItem.ItemId, 1);
            ItemStack = new ItemStackSaveJsonObject(item);

            // tickを秒数に変換して保存
            // Convert ticks to seconds for storage
            RemainingSeconds = GameUpdater.TicksToSeconds(vanillaBeltConveyorInventoryItem.RemainingTicks);

            SourceConnectorGuid = vanillaBeltConveyorInventoryItem.StartConnector?.ConnectorGuid;
            GoalConnectorGuid = vanillaBeltConveyorInventoryItem.GoalConnector?.ConnectorGuid;
        }
    }
}
