using Game.Block.Blocks;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Service;
using Game.Block.Blocks.TrainRail;
using Game.Block.Factory.BlockTemplate.Utility;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.RailGraph;
using Mooresmaster.Model.BlocksModule;
using System.Collections.Generic;


namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaTrainStationTemplate : IBlockTemplate
    {
        /// <summary>
        /// 新規にブロック（および対応するRailComponent等）を生成する
        /// </summary>
        public IBlock New(BlockMasterElement masterElement,
            BlockInstanceId instanceId,
            BlockPositionInfo positionInfo, BlockCreateParam[] createParams)
        {
            var stationParam = masterElement.BlockParam as TrainStationBlockParam;
            // 駅ブロックは常に2つのRailComponentを持つ
            //①ここでは1つのstation内にある2つのRailComponentを直線で接続している
            //②stationをつなげて設置した場合に自動でrailComponentを接続するための処理もここでやってる
            var railComponents = RailComponentFactory.Create2RailComponents(positionInfo, stationParam.EntryRailPosition, stationParam.ExitRailPosition);
            var railSaverComponent = RailComponentFactory.CreateRailSaverComponent(railComponents);
            var station = new StationComponent("test", stationParam.SlotCount);

            var inventoryComponents = CreateInventoryComponents(null, instanceId, stationParam, positionInfo);

            // 生成したコンポーネントをブロックに登録する
            var blockComponents = new List<IBlockComponent>();
            blockComponents.Add(railSaverComponent);
            blockComponents.AddRange(railComponents);
            blockComponents.Add(station);
            blockComponents.AddRange(inventoryComponents);

            // ここで各RailNodeにStationReferenceを設定  
            var createdBlock = new BlockSystem(instanceId, masterElement.BlockGuid, blockComponents, positionInfo);
            // 各RailComponentのNodeにStationReferenceを設定
            railComponents[0].FrontNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Entry, StationNodeSide.Front);
            railComponents[1].FrontNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Exit, StationNodeSide.Front);
            railComponents[1].BackNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Entry, StationNodeSide.Back);
            railComponents[0].BackNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Exit, StationNodeSide.Back);
            return createdBlock;
        }

        public IBlock Load(
            Dictionary<string, string> componentStates,
            BlockMasterElement masterElement,
            BlockInstanceId instanceId,
            BlockPositionInfo positionInfo)
        {
            // 保存されたRailComponent群を復元。railSaverComponentからセーブ情報の中にrailcomponent同士の接続情報が含まれているのでそれを復元(これで①1つのstation内にある2つのRailComponentを直線で接続と、②stationをつなげて設置した場合に自動でrailComponentを接続、の両方が満たされる)
            var stationParam = masterElement.BlockParam as TrainStationBlockParam;
            var railComponents = RailComponentUtility.RestoreRailComponents(componentStates, positionInfo, stationParam.EntryRailPosition, stationParam.ExitRailPosition);
            var railSaverComponent = new RailSaverComponent(railComponents);
            var station = new StationComponent("test", stationParam.SlotCount);
            
            var inventoryComponents = CreateInventoryComponents(componentStates, instanceId, stationParam, positionInfo);

            // 復元したコンポーネントをブロックに登録する
            var blockComponents = new List<IBlockComponent>();
            blockComponents.Add(railSaverComponent);
            blockComponents.AddRange(railComponents);
            blockComponents.Add(station);
            blockComponents.AddRange(inventoryComponents);

            // ここで各RailNodeにStationReferenceを設定  
            var createdBlock = new BlockSystem(instanceId, masterElement.BlockGuid, blockComponents, positionInfo);
            // 各RailComponentのNodeにStationReferenceを設定
            railComponents[0].FrontNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Entry, StationNodeSide.Front);
            railComponents[1].FrontNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Exit, StationNodeSide.Front);
            railComponents[1].BackNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Entry, StationNodeSide.Back);
            railComponents[0].BackNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Exit, StationNodeSide.Back);
            return createdBlock;
        }

        /// <summary>
        /// インベントリ関連のコンポーネントを作成する
        /// </summary>
        private List<IBlockComponent> CreateInventoryComponents(Dictionary<string, string> componentStates, BlockInstanceId instanceId, TrainStationBlockParam param, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
            var inserter = new ConnectingInventoryListPriorityInsertItemService(inputConnectorComponent);
            
            var chestComponent = componentStates == null ?
                new VanillaChestComponent(instanceId, param.SlotCount, inserter) :
                new VanillaChestComponent(componentStates, instanceId, param.SlotCount, inserter);
            
            return new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
            };
        }

    }
}
