using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Service;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Game.Block.Blocks.TrainRail;
using Game.Train.RailGraph;
using Game.Train.Utility;
using Game.Block.Factory.BlockTemplate.Utility;


namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaTrainStationTemplate : IBlockTemplate
    {
        /// <summary>
        /// 新規にブロック（および対応するRailComponent等）を生成する
        /// </summary>
        public IBlock New(
            BlockMasterElement masterElement,
            BlockInstanceId instanceId,
            BlockPositionInfo positionInfo)
        {
            var stationParam = masterElement.BlockParam as TrainStationBlockParam;
            
            // 駅ブロックは常に2つのRailComponentを持つ
            var railComponents = new RailComponent[2];
            var railSaverComponent = new RailSaverComponent(railComponents);
            var railComponentPositions = RailComponentUtility.CalculateRailComponentPositions(positionInfo);

            // 各RailComponentを生成
            for (int i = 0; i < railComponents.Length; i++)
            {
                var componentId = new RailComponentID(positionInfo.OriginalPos, i);
                railComponents[i] = new RailComponent(railComponentPositions[i], positionInfo.BlockDirection, componentId);
            }
            railComponents[0].ConnectRailComponent(railComponents[1], true, true);

            var station = GetStation(masterElement, positionInfo, railComponents);
            var inventoryComponents = CreateInventoryComponents(null, instanceId, stationParam, positionInfo);
            
            // 生成したコンポーネントをブロックに登録する
            var blockComponents = new List<IBlockComponent>();
            blockComponents.Add(railSaverComponent);
            blockComponents.AddRange(railComponents);
            blockComponents.Add(station);
            blockComponents.AddRange(inventoryComponents);
            return new BlockSystem(instanceId, masterElement.BlockGuid, blockComponents, positionInfo);
        }

        public IBlock Load(
            Dictionary<string, string> componentStates,
            BlockMasterElement masterElement,
            BlockInstanceId instanceId,
            BlockPositionInfo positionInfo)
        {
            var stationParam = masterElement.BlockParam as TrainStationBlockParam;
            
            // 保存されたRailComponent群を復元
            var railComponents = RailComponentUtility.RestoreRailComponents(componentStates, positionInfo);
            // 復元したRailComponentを管理するRailSaverComponentを作成
            var railSaverComponent = new RailSaverComponent(railComponents);

            var station = new StationComponent(stationParam.StationDistance, "test", 1);
            
            var inventoryComponents = CreateInventoryComponents(componentStates, instanceId, stationParam, positionInfo);

            // 復元したコンポーネントをブロックに登録する
            var blockComponents = new List<IBlockComponent>();
            blockComponents.Add(railSaverComponent);
            blockComponents.AddRange(railComponents);
            blockComponents.Add(station);
            blockComponents.AddRange(inventoryComponents);
            return new BlockSystem(instanceId, masterElement.BlockGuid, blockComponents, positionInfo);
        }

        private StationComponent GetStation(BlockMasterElement masterElement, BlockPositionInfo positionInfo, RailComponent[] railComponents)
        {
            var stationParam = masterElement.BlockParam as TrainStationBlockParam;
            var station = new StationComponent(stationParam.StationDistance, "test", 1);
            //進行方向チェック
            var (v3, b) = StationConnectionChecker.IsStationConnectedToFront(positionInfo);
            if (b == true) 
            {
                //自分の1 frontから相手の0 frontに接続する
                var railComponentId = new RailComponentID(v3, 0);
                var dst = new ConnectionDestination(railComponentId, true);
                RailComponentUtility.EstablishConnection(railComponents[1], dst, true);
            }
            //逆方向チェック
            (v3, b) = StationConnectionChecker.IsStationConnectedToBack(positionInfo);
            if (b == true)
            {
                //自分の0 backから相手の1 backに接続する
                var railComponentId = new RailComponentID(v3, 1);
                var dst = new ConnectionDestination(railComponentId, false);
                RailComponentUtility.EstablishConnection(railComponents[0], dst, false);
            }
            return station;
        }
        
        /// <summary>
        /// インベントリ関連のコンポーネントを作成する
        /// </summary>
        /// <returns></returns>
        private List<IBlockComponent> CreateInventoryComponents(Dictionary<string, string> componentStates, BlockInstanceId instanceId, TrainStationBlockParam param, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
            var inserter = new ConnectingInventoryListPriorityInsertItemService(inputConnectorComponent);
            
            var chestComponent = componentStates == null ?
                new VanillaChestComponent(instanceId, param.ItemSlotCount, inserter) :
                new VanillaChestComponent(componentStates, instanceId, param.ItemSlotCount, inserter);
            
            return new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
            };
        }

    }
}
