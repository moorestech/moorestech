using System.Collections.Generic;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Block.Factory.BlockTemplate.Utility
{
    public static class RailComponentUtility
    {
        
        static public RailComponent[] RestoreRailComponents(Dictionary<string, string> componentStates,BlockPositionInfo positionInfo)
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
    }
}