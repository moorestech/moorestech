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
            var railComponentPositions = new Vector3[2];
            railComponentPositions[0] = CalculateRailComponentPosition(positionInfo, entryPosition);
            railComponentPositions[1] = CalculateRailComponentPosition(positionInfo, exitPosition);

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

        static public Vector3 CalculateRailComponentPosition(BlockPositionInfo positionInfo, Vector3 componentPosition)
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
            return CoordinateConvert(blockDirection, componentPosition) + baseOriginPosition;
        }

        // 自分の駅or貨物駅ブロック内のRailComponentから、別ブロックのRailComponentへの接続を確立する
        // 自分から自分への接続はWorldBlockDatastore.GetBlockが失敗するため、ここでは扱わない
        static public void EstablishConnection(RailComponent sourceComponent, ConnectionDestination destinationConnection, bool isFrontSideOfComponent)
        {
            var destinationComponentId = destinationConnection.railComponentID;
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