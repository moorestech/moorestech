using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using UnityEngine;

namespace Game.Block.Component
{
    public static class BlockConnectorConnectPositionCalculator
    {
        /// <summary>
        /// key: コネクターの位置
        ///      Position of the connector
        ///
        /// value: そのコネクターと接続できる位置のリスト（IBlockConnector含む）
        ///        List of positions that can be connected to the connector (includes IBlockConnector)
        /// </summary>
        public static Dictionary<Vector3Int, List<(Vector3Int position, IBlockConnector element)>> CalculateConnectorToConnectPosList(IBlockConnector[] inputConnects, BlockPositionInfo blockPositionInfo)
        {
            var blockDirection = blockPositionInfo.BlockDirection;
            var blockBaseOriginPos = blockDirection.GetBlockBaseOriginPos(blockPositionInfo);
            var result = new Dictionary<Vector3Int, List<(Vector3Int position, IBlockConnector element)>>();

            if (inputConnects == null) return result;
            foreach (var inputConnectSetting in inputConnects)
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
        /// value: その位置と接続するコネクターの位置（IBlockConnector含む）
        ///        Position of the connector to connect to that position (includes IBlockConnector)
        /// </summary>
        public static Dictionary<Vector3Int, (Vector3Int position, IBlockConnector element)> CalculateConnectPosToConnector(IBlockConnector[] outputConnects, BlockPositionInfo blockPositionInfo)
        {
            var result = new Dictionary<Vector3Int, (Vector3Int position, IBlockConnector element)>();

            if (outputConnects == null) return result;

            var connectorToConnectPosList = CalculateConnectorToConnectPosList(outputConnects, blockPositionInfo);
            foreach (var (connectPos, targetElements) in connectorToConnectPosList)
            {
                if (targetElements == null) continue;

                foreach (var (targetPos, element) in targetElements)
                {
                    // targetPosの重複は今のところ考慮しない
                    result[targetPos] = (connectPos, element);
                }
            }

            return result;
        }
    }
}
