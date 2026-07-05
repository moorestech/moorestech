using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Component
{
    public static class BlockConnectorConnectPositionCalculator
    {
        /// <summary>
        /// key: コネクターの位置
        ///      Position of the connector
        ///
        /// value: 接続可能位置のリスト（IBlockConnector含む）
        ///        List of connectable positions (includes IBlockConnector)
        /// </summary>
        public static Dictionary<Vector3Int, List<(Vector3Int position, IBlockConnector connector)>> CalculateConnectorToConnectPosList(IReadOnlyList<IBlockConnector> inputConnectors, BlockPositionInfo blockPositionInfo)
        {
            var blockDirection = blockPositionInfo.BlockDirection;
            var blockBaseOriginPos = blockDirection.GetBlockBaseOriginPos(blockPositionInfo);
            var result = new Dictionary<Vector3Int, List<(Vector3Int position, IBlockConnector connector)>>();

            if (inputConnectors == null) return result;
            foreach (var inputConnectSetting in inputConnectors)
            {
                var blockPosConvertAction = blockDirection.GetCoordinateConvertAction();

                var inputConnectorPos = blockBaseOriginPos + blockPosConvertAction(inputConnectSetting.Offset);
                var directions = inputConnectSetting.Directions;
                if (directions == null)
                {
                    result.Add(inputConnectorPos, null);
                    continue;
                }

                var targetPositions = directions.Select(c => (inputConnectorPos + blockPosConvertAction(c), inputConnectSetting)).ToList();
                if (!result.TryAdd(inputConnectorPos, targetPositions)) result[inputConnectorPos] = result[inputConnectorPos].Concat(targetPositions).ToList();
            }

            return result;
        }

        /// <summary>
        /// key: コネクターと接続する位置
        ///     Position to connect to the connector
        ///
        /// value: 接続先コネクターの位置（IBlockConnector含む）
        ///        Position of the connecting connector (includes IBlockConnector)
        /// </summary>
        public static Dictionary<Vector3Int, (Vector3Int position, IBlockConnector connector)> CalculateConnectPosToConnector(IReadOnlyList<IBlockConnector> outputConnectors, BlockPositionInfo blockPositionInfo)
        {
            var result = new Dictionary<Vector3Int, (Vector3Int position, IBlockConnector connector)>();

            if (outputConnectors == null) return result;

            var connectorToConnectPosList = CalculateConnectorToConnectPosList(outputConnectors, blockPositionInfo);
            foreach (var (connectPos, targetConnectors) in connectorToConnectPosList)
            {
                if (targetConnectors == null) continue;

                foreach (var (targetPos, connector) in targetConnectors)
                {
                    // targetPosの重複は今のところ考慮しない
                    result[targetPos] = (connectPos, connector);
                }
            }

            return result;
        }
    }
}
