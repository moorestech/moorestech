using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Service;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Game.Block.Blocks.TrainRail;
using Game.Block.Factory.BlockTemplate.Utility;


namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaTrainCargoTemplate : IBlockTemplate
    {
        /// <summary>
        /// 新規にブロック（および対応するRailComponent等）を生成する
        /// </summary>
        public IBlock New(
            BlockMasterElement masterElement,
            BlockInstanceId instanceId,
            BlockPositionInfo positionInfo)
        {
            // 駅ブロックは常に2つのRailComponentを持つ
            var railComponents = RailComponentFactory.CreateRailComponents(2, positionInfo);// ①ここでは1つのstation内にある2つのRailComponentを直線で接続している
            var railSaverComponent = RailComponentFactory.CreateRailSaverComponent(railComponents);
            var station = StationComponentFactory.CreateAndConnectStationComponent<CargoplatformComponent>(
                masterElement, positionInfo, railComponents
            );//②stationをつなげて設置した場合に自動でrailComponentを接続するための処理もここでやってる

            var stationParam = masterElement.BlockParam as TrainCargoPlatformBlockParam;
            //var inventoryComponents = CreateInventoryComponents(null, instanceId, stationParam, positionInfo);

            // 生成したコンポーネントをブロックに登録する
            var blockComponents = new List<IBlockComponent>();
            blockComponents.Add(railSaverComponent);
            blockComponents.AddRange(railComponents);
            blockComponents.Add(station);
            //blockComponents.AddRange(inventoryComponents);
            return new BlockSystem(instanceId, masterElement.BlockGuid, blockComponents, positionInfo);
        }

        public IBlock Load(
            Dictionary<string, string> componentStates,
            BlockMasterElement masterElement,
            BlockInstanceId instanceId,
            BlockPositionInfo positionInfo)
        {
            // 保存されたRailComponent群を復元。railSaverComponentからセーブ情報の中にrailcomponent同士の接続情報が含まれているのでそれを復元(これで①1つのstation内にある2つのRailComponentを直線で接続と、②stationをつなげて設置した場合に自動でrailComponentを接続、の両方が満たされる)
            var railComponents = RailComponentUtility.RestoreRailComponents(componentStates, positionInfo);
            var railSaverComponent = new RailSaverComponent(railComponents);

            var stationParam = masterElement.BlockParam as TrainCargoPlatformBlockParam;
            var station = new CargoplatformComponent(stationParam.PlatformDistance, stationParam.InputSlotCount, stationParam.OutputSlotCount);

            // 復元したコンポーネントをブロックに登録する
            var blockComponents = new List<IBlockComponent>();
            blockComponents.Add(railSaverComponent);
            blockComponents.AddRange(railComponents);
            blockComponents.Add(station);
            return new BlockSystem(instanceId, masterElement.BlockGuid, blockComponents, positionInfo);
        }


        /*
        /// <summary>
        /// インベントリ関連のコンポーネントを作成する
        /// </summary>
        private List<IBlockComponent> CreateInventoryComponents(Dictionary<string, string> componentStates, BlockInstanceId instanceId, TrainCargoPlatformBlockParam param, BlockPositionInfo blockPositionInfo)
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
        */

    }



}