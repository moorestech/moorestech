using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Block.Factory.BlockTemplate.Utility
{
    public static class RailComponentUtility
    {

        static public RailComponent[] RestoreRailComponents(Dictionary<string, string> componentStates, BlockPositionInfo positionInfo, Vector3 entryPosition, Vector3 exitPosition)
        {
            // JSON形式の保存データを取得・復元
            string railSaverJson = componentStates[typeof(RailSaverComponent).FullName];
            var saverData = JsonConvert.DeserializeObject<RailSaverData>(railSaverJson);

            int count = saverData.Values.Count;
            var railComponents = new RailComponent[count];
            var railComponentPositions = CalculateRailComponentPositions(positionInfo, entryPosition, exitPosition);

            // 各RailComponentを生成
            for (int i = 0; i < count; i++)
            {
                var componentInfo = saverData.Values[i];
                railComponents[i] = new RailComponent(railComponentPositions[i], componentInfo.RailDirection.Vector3, componentInfo.MyID);
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

            // 自分の駅内の接続情報も復元、距離は自動計算（もともとセーブに距離情報はない）
            if (count >= 2)
                railComponents[0].ConnectRailComponent(railComponents[1], true, true);
            return railComponents;
        }

        static public Vector3[] CalculateRailComponentPositions(BlockPositionInfo positionInfo, Vector3 entryPosition, Vector3 exitPosition)
        {
            Vector3 CoordinateConvert(BlockDirection blockDirection,Vector3 pos)
            {
                var rotation = blockDirection.GetRotation();
                var rotationMatrix = Matrix4x4.Rotate(rotation);
                // 行列は float4 × float4 の形なので pos を拡張して計算
                var transformed = rotationMatrix.MultiplyPoint3x4(pos);
                return transformed;
            }
            var blockDirection = positionInfo.BlockDirection;
            Vector3 baseOriginPosition = blockDirection.GetBlockBaseOriginPos(positionInfo);
            Vector3[] componentPositions = new Vector3[2];
            componentPositions[0] = CoordinateConvert(blockDirection, entryPosition) + baseOriginPosition;
            componentPositions[1] = CoordinateConvert(blockDirection, exitPosition) + baseOriginPosition;
            return componentPositions;
        }

    }
}