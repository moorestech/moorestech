using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Game.Block.Blocks.TrainRail;
using Game.Train.RailGraph;
using Game.Train.Utility;
using Newtonsoft.Json;
using Game.Context;
using Game.Block.Interface.Extension;
using UnityEngine;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaTrainCargoTemplate : IBlockTemplate
    {
        RailComponent[] railComponents;
        /// <summary>
        /// 新規にブロック（および対応するRailComponent等）を生成する
        /// </summary>
        public IBlock New(
            BlockMasterElement masterElement,
            BlockInstanceId instanceId,
            BlockPositionInfo positionInfo)
        {
            // 駅ブロックは常に2つのRailComponentを持つ
            railComponents = new RailComponent[2];
            var railSaverComponent = new RailSaverComponent(railComponents);
            var railComponentPositions = VanillaTrainStationTemplate.CalculateRailComponentPositions(positionInfo);

            // 各RailComponentを生成
            for (int i = 0; i < railComponents.Length; i++)
            {
                var componentId = new RailComponentID(positionInfo.OriginalPos, i);
                railComponents[i] = new RailComponent(railComponentPositions[i], positionInfo.BlockDirection, componentId);
            }
            railComponents[0].ConnectRailComponent(railComponents[1], true, true);

            var station = GetStation(masterElement, positionInfo);
            // 生成したコンポーネントをブロックに登録する
            var blockComponents = new List<IBlockComponent>();
            blockComponents.Add(railSaverComponent);
            blockComponents.AddRange(railComponents);
            blockComponents.Add(station);
            return new BlockSystem(instanceId, masterElement.BlockGuid, blockComponents, positionInfo);
        }

        public IBlock Load(
            Dictionary<string, string> componentStates,
            BlockMasterElement masterElement,
            BlockInstanceId instanceId,
            BlockPositionInfo positionInfo)
        {
            // 保存されたRailComponent群を復元
            railComponents = VanillaTrainStationTemplate.RestoreRailComponents(componentStates, positionInfo);
            // 復元したRailComponentを管理するRailSaverComponentを作成
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

        private CargoplatformComponent GetStation(BlockMasterElement masterElement, BlockPositionInfo positionInfo)
        {
            var stationParam = masterElement.BlockParam as TrainCargoPlatformBlockParam;
            var station = new CargoplatformComponent(stationParam.PlatformDistance, stationParam.InputSlotCount, stationParam.OutputSlotCount);
            //進行方向チェック
            var (v3, b) = StationConnectionChecker.IsStationConnectedToFront(positionInfo);
            if (b == true)
            {
                //自分の1 frontから相手の0 frontに接続する
                var railComponentId = new RailComponentID(v3, 0);
                var dst = new ConnectionDestination(railComponentId, true);
                VanillaTrainStationTemplate.EstablishConnection(railComponents[1], dst, true);
            }
            //逆方向チェック
            (v3, b) = StationConnectionChecker.IsStationConnectedToBack(positionInfo);
            if (b == true)
            {
                //自分の0 backから相手の1 backに接続する
                var railComponentId = new RailComponentID(v3, 1);
                var dst = new ConnectionDestination(railComponentId, false);
                VanillaTrainStationTemplate.EstablishConnection(railComponents[0], dst, false);
            }
            return station;
        }
    }



}