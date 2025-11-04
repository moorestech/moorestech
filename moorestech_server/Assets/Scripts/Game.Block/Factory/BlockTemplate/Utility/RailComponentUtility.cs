using System.Collections.Generic;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Block.Factory.BlockTemplate.Utility
{
    public static class RailComponentUtility
    {
        public readonly struct RailComponentPlacement
        {
            public RailComponentPlacement(Vector3 position, float controlPointLength)
            {
                Position = position;
                ControlPointLength = controlPointLength;
            }

            public Vector3 Position { get; }
            public float ControlPointLength { get; }
        }

        static public RailComponent[] RestoreRailComponents(Dictionary<string, string> componentStates, BlockMasterElement masterElement, BlockPositionInfo positionInfo)
        {
            // JSON形式の保存データを取得・復元
            // Restore serialized component state from JSON
            string railSaverJson = componentStates[typeof(RailSaverComponent).FullName];
            var saverData = JsonConvert.DeserializeObject<RailSaverData>(railSaverJson);

            int count = saverData.Values.Count;
            var railComponents = new RailComponent[count];
            var placements = CalculateRailComponentPlacements(masterElement.BlockParam, positionInfo, count);

            // 各RailComponentを生成
            // Instantiate each rail component with saved metadata
            for (int i = 0; i < count; i++)
            {
                var componentInfo = saverData.Values[i];
                var placement = placements[Mathf.Min(i, placements.Length - 1)];
                railComponents[i] = new RailComponent(placement.Position, componentInfo.RailDirection.Vector3, componentInfo.MyID);
                // ベジェ曲線の強度を設定
                // Apply saved bezier strength
                railComponents[i].UpdateControlPointStrength(componentInfo.BezierStrength);
            }

            // 接続情報の復元 (Front/Back)
            for (int i = 0; i < count; i++)
            {
                var componentInfo = saverData.Values[i];
                var currentComponent = railComponents[i];

                // FrontNodeへの接続情報を復元
                // Restore front-side connection definitions
                foreach (var destinationConnection in componentInfo.ConnectMyFrontTo)
                {
                    EstablishConnection(currentComponent, destinationConnection, isFrontSideOfComponent: true);
                }
                // BackNodeへの接続情報を復元
                // Restore back-side connection definitions
                foreach (var destinationConnection in componentInfo.ConnectMyBackTo)
                {
                    EstablishConnection(currentComponent, destinationConnection, isFrontSideOfComponent: false);
                }
            }

            // 自分の駅内の接続情報も復元、距離は自動計算（もともとセーブに距離情報はない）
            // Reconnect intra-block components while distance stays auto-evaluated
            if (count >= 2)
                railComponents[0].ConnectRailComponent(railComponents[1], true, true);
            return railComponents;
        }

        static public RailComponentPlacement[] CalculateRailComponentPlacements(IBlockParam blockParam, BlockPositionInfo positionInfo, int expectedCount)
        {
            // 鉄道コンポーネントの基準位置を算出
            // Compute baseline positions for rail components
            var defaultPositions = CalculateDefaultRailComponentPositions(positionInfo, expectedCount);
            var controlPointLengths = BuildDefaultControlPointLengths(expectedCount);

            // マスターデータの指定を反映
            // Apply master-configured placement overrides
            ApplyPlacementOverrides(blockParam, positionInfo, expectedCount, defaultPositions, controlPointLengths);

            var placements = new RailComponentPlacement[expectedCount];
            for (int i = 0; i < expectedCount; i++)
            {
                placements[i] = new RailComponentPlacement(defaultPositions[i], controlPointLengths[i]);
            }

            return placements;
        }

        private static Vector3[] CalculateDefaultRailComponentPositions(BlockPositionInfo positionInfo, int expectedCount)
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
            Vector3[] componentPositions = new Vector3[Mathf.Max(expectedCount, 1)];
            var firstPosition = (corner0 + corner1 + corner2 + corner3) * 0.25f + baseOriginPosition + new Vector3(0.5f, 0.5f, 0.5f);
            var secondPosition = (corner4 + corner5 + corner6 + corner7) * 0.25f + baseOriginPosition + new Vector3(0.5f, 0.5f, 0.5f);

            for (int i = 0; i < componentPositions.Length; i++)
            {
                componentPositions[i] = i == 0 ? firstPosition : secondPosition;
            }

            return componentPositions;
        }

        private static float[] BuildDefaultControlPointLengths(int expectedCount)
        {
            var lengths = new float[Mathf.Max(expectedCount, 1)];
            for (int i = 0; i < lengths.Length; i++)
            {
                lengths[i] = 0.5f;
            }

            return lengths;
        }

        private static void ApplyPlacementOverrides(IBlockParam blockParam, BlockPositionInfo positionInfo, int expectedCount, Vector3[] defaultPositions, float[] controlPointLengths)
        {
            if (blockParam == null)
            {
                return;
            }

            // 北向き配置を基準とする補正量を算出
            // Calculate deltas against the north-facing baseline
            var northReferenceInfo = new BlockPositionInfo(positionInfo.OriginalPos, BlockDirection.North, positionInfo.BlockSize);
            var defaultNorthPositions = CalculateDefaultRailComponentPositions(northReferenceInfo, expectedCount);
            var rotation = positionInfo.BlockDirection.GetRotation();

            void ApplyOverride(int index, Vector3 customPositionNorth, float customControlLength)
            {
                if (index < 0 || index >= defaultPositions.Length || index >= defaultNorthPositions.Length)
                {
                    return;
                }

                var deltaNorth = customPositionNorth - defaultNorthPositions[index];
                var rotatedDelta = rotation * deltaNorth;
                defaultPositions[index] += rotatedDelta;
                controlPointLengths[index] = customControlLength;
            }

            switch (blockParam)
            {
                case TrainRailBlockParam railParam when expectedCount >= 1:
                    ApplyOverride(0, railParam.RailPosition, railParam.ControlPointLength);
                    break;
                case TrainStationBlockParam stationParam when expectedCount >= 2:
                    ApplyOverride(0, stationParam.FrontRailPosition, stationParam.FrontControlPointLength);
                    ApplyOverride(1, stationParam.BackRailPosition, stationParam.BackControlPointLength);
                    break;
                case TrainCargoPlatformBlockParam cargoParam when expectedCount >= 2:
                    ApplyOverride(0, cargoParam.FrontRailPosition, cargoParam.FrontControlPointLength);
                    ApplyOverride(1, cargoParam.BackRailPosition, cargoParam.BackControlPointLength);
                    break;
            }
        }

        // 自分の駅or貨物駅ブロック内のRailComponentから、別ブロックのRailComponentへの接続を確立する
        // Establish connections from the station/cargo block to external rail components
        // 自分から自分への接続はWorldBlockDatastore.GetBlockが失敗するため、ここでは扱わない
        // Self-loop connections are skipped because WorldBlockDatastore lookup fails in that case
        static public void EstablishConnection(RailComponent sourceComponent, ConnectionDestination destinationConnection, bool isFrontSideOfComponent)
        {
            var destinationComponentId = destinationConnection.DestinationID;
            var useFrontSideOfTarget = destinationConnection.IsFront;

            var destinationPosition = destinationComponentId.Position;
            var componentIndex = destinationComponentId.ID;

            // 対象ブロックをワールドから取得
            // Fetch the destination block from the world datastore
            var targetBlock = ServerContext.WorldBlockDatastore.GetBlock(destinationPosition);
            if (targetBlock == null) return;

            // 対象ブロックがRailSaverComponentを持っているか確認
            // Ensure the target block exposes a RailSaverComponent
            if (!targetBlock.TryGetComponent<RailSaverComponent>(out var targetRailSaver))
                return;

            // RailComponents配列から対象のRailComponentを取得
            // Resolve the destination rail component by index
            if (componentIndex < 0 || componentIndex >= targetRailSaver.RailComponents.Length)
                return;

            var targetComponent = targetRailSaver.RailComponents[componentIndex];

            // 接続を実施 (既に接続済みの場合、距離が上書きされるだけ)
            // Create the bidirectional link (distance is overwritten when already connected)
            sourceComponent.ConnectRailComponent(targetComponent, isFrontSideOfComponent, useFrontSideOfTarget);
        }
    }
}
