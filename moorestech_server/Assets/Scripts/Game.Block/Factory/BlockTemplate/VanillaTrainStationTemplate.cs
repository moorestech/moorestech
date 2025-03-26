
using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Game.Block.Blocks.TrainRail;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaTrainStationTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var transformer = new RailComponent(blockPositionInfo);
            var components = new List<IBlockComponent>
            {
                transformer,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var transformer = new RailComponent(blockPositionInfo);
            var components = new List<IBlockComponent>
            {
                transformer,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}


/*
using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Game.Block.Blocks.TrainRail;
using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaTrainStationTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _openableInventoryUpdateEvent;

        public VanillaTrainStationTemplate(BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent)
        {
            _openableInventoryUpdateEvent = openableInventoryUpdateEvent;
        }

        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            // 駅ブロックが持つインベントリスロット数などを Param から取得
            var param = blockMasterElement.BlockParam as TrainStationBlockParam;
            var stationLength = param.StationLength;
            var inventorySlotCount = param.ItemSlotCount;

            // インベントリ接続用コンポーネントを作成
            var connector = new BlockConnectorComponent<IBlockInventory>(
                param.InventoryConnectors.InputConnects,
                param.InventoryConnectors.OutputConnects,
                blockPositionInfo
            );

            // 駅コンポーネントを作成
            var stationComponent = new StationComponent(
                blockInstanceId,
                stationLength,
                "MyTrainStation",
                inventorySlotCount,
                _openableInventoryUpdateEvent
            );

            // コンポーネントをまとめて返す
            var components = new List<IBlockComponent>
            {
                stationComponent,
                connector // ベルトコンベア等と繋がる
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = blockMasterElement.BlockParam as TrainStationBlockParam;
            var stationLength = param.StationLength;
            var inventorySlotCount = param.ItemSlotCount;

            // インベントリ接続用コンポーネント
            var connector = new BlockConnectorComponent<IBlockInventory>(
                param.InventoryConnectors.InputConnects,
                param.InventoryConnectors.OutputConnects,
                blockPositionInfo
            );

            // 駅コンポーネントを作成
            var stationComponent = new StationComponent(
                blockInstanceId,
                stationLength,
                "MyTrainStation",
                inventorySlotCount,
                _openableInventoryUpdateEvent
            );

            // セーブ文字列を読み込む (あれば)
            if (componentStates.TryGetValue(stationComponent.SaveKey, out var savedJson))
            {
                stationComponent.LoadFromJsonString(savedJson);
            }

            var components = new List<IBlockComponent>
            {
                stationComponent,
                connector
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}

*/