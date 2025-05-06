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
using Newtonsoft.Json;
using Game.Context;
using Game.Block.Interface.Extension;
using UnityEngine;

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
            var railComponentPositions = CalculateRailComponentPositions(positionInfo);

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
            var railComponents = RestoreRailComponents(componentStates, positionInfo);
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
                EstablishConnection(railComponents[1], dst, true);
            }
            //逆方向チェック
            (v3, b) = StationConnectionChecker.IsStationConnectedToBack(positionInfo);
            if (b == true)
            {
                //自分の0 backから相手の1 backに接続する
                var railComponentId = new RailComponentID(v3, 1);
                var dst = new ConnectionDestination(railComponentId, false);
                EstablishConnection(railComponents[0], dst, false);
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

        //共通処理
        /// <summary>
        /// RailSaverComponentのJSONデータからRailComponent配列を復元する
        /// TODO: VanillaTrainRailTemplateと共通化検討
        /// </summary>
        static public RailComponent[] RestoreRailComponents(
            Dictionary<string, string> componentStates,
            BlockPositionInfo positionInfo)
        {
            // JSON形式の保存データを取得・復元
            string railSaverJson = componentStates[typeof(RailSaverComponent).FullName];
            var saverData = JsonConvert.DeserializeObject<RailSaverData>(railSaverJson);

            int count = saverData.Values.Count;
            var railComponents = new RailComponent[count];
            var railComponentPositions = CalculateRailComponentPositions(positionInfo);

            // 各RailComponentを生成
            for (int i = 0; i < count; i++)
            {
                var componentInfo = saverData.Values[i];
                railComponents[i] = new RailComponent(railComponentPositions[i], positionInfo.BlockDirection, componentInfo.MyID);
                // ベジェ曲線の強度を設定
                railComponents[i].UpdateControlPointStrength(componentInfo.BezierStrength);
            }

            // 接続情報の復元 (Front/Back)
            for (int i = 0; i < count; i++)
            {
                var componentInfo = saverData.Values[i];
                var currentComponent = railComponents[i];

                // FrontNodeへの接続情報を復元
                foreach (var destinationConnection in componentInfo.ConnectMyFrontTo)
                {
                    EstablishConnection(currentComponent, destinationConnection, isFrontSideOfComponent: true);
                }
                // BackNodeへの接続情報を復元
                foreach (var destinationConnection in componentInfo.ConnectMyBackTo)
                {
                    EstablishConnection(currentComponent, destinationConnection, isFrontSideOfComponent: false);
                }
            }
            return railComponents;
        }

        /// <summary>
        /// RailComponentの接続を実際に行うヘルパーメソッド
        /// TODO: VanillaTrainRailTemplateと共通化検討
        /// </summary>
        static public void EstablishConnection(RailComponent sourceComponent, ConnectionDestination destinationConnection, bool isFrontSideOfComponent)
        {
            var destinationComponentId = destinationConnection.DestinationID;
            var useFrontSideOfTarget = destinationConnection.IsFront;

            var destinationPosition = destinationComponentId.Position;
            var componentIndex = destinationComponentId.ID;

            // 対象ブロックをワールドから取得
            var targetBlock = ServerContext.WorldBlockDatastore.GetBlock(destinationPosition);
            if (targetBlock == null) return;

            // 対象ブロックがRailSaverComponentを持っているか確認
            if (!targetBlock.TryGetComponent<RailSaverComponent>(out var targetRailSaver))
                return;

            // RailComponents配列から対象のRailComponentを取得
            if (componentIndex < 0 || componentIndex >= targetRailSaver.RailComponents.Length)
                return;

            var targetComponent = targetRailSaver.RailComponents[componentIndex];

            // 接続を実施 (既に接続済みの場合、距離が上書きされるだけ)
            sourceComponent.ConnectRailComponent(targetComponent, isFrontSideOfComponent, useFrontSideOfTarget);
        }

        static public Vector3[] CalculateRailComponentPositions(BlockPositionInfo positionInfo)
        {
            var blockDirection = positionInfo.BlockDirection;
            Vector3 baseOriginPosition = blockDirection.GetBlockBaseOriginPos(positionInfo);
            var coordinateConverter = blockDirection.GetCoordinateConvertAction();
            Vector3Int blockSize = positionInfo.BlockSize;
            Vector3 corner0 = coordinateConverter(new Vector3Int(0, 0, 0));
            Vector3 corner1 = coordinateConverter(new Vector3Int(0, 0, blockSize.z - 1));
            Vector3 corner2 = coordinateConverter(new Vector3Int(-1, 0, 0));
            Vector3 corner3 = coordinateConverter(new Vector3Int(-1, 0, blockSize.z - 1));
            Vector3 corner4 = coordinateConverter(new Vector3Int(blockSize.x - 1, 0, 0));
            Vector3 corner5 = coordinateConverter(new Vector3Int(blockSize.x - 1, 0, blockSize.z - 1));
            Vector3 corner6 = coordinateConverter(new Vector3Int(blockSize.x, 0, 0));
            Vector3 corner7 = coordinateConverter(new Vector3Int(blockSize.x, 0, blockSize.z - 1));
            Vector3[] componentPositions = new Vector3[2];
            componentPositions[0] = (corner0 + corner1 + corner2 + corner3) * 0.25f + baseOriginPosition + new Vector3(0.5f, 0.5f, 0.5f);
            componentPositions[1] = (corner4 + corner5 + corner6 + corner7) * 0.25f + baseOriginPosition + new Vector3(0.5f, 0.5f, 0.5f);
            return componentPositions;
        }
    }
}
