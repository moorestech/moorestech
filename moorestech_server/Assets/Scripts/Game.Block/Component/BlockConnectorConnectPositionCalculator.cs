using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Mooresmaster.Model.BlockConnectInfoModule;
using UnityEngine;

namespace Game.Block.Component
{
    public static class BlockConnectorConnectPositionCalculator
    {
        /// <summary>
        /// key: コネクターの位置
        ///      Position of the connector
        ///
        /// value: そのコネクターと接続できる位置のリスト
        ///        List of positions that can be connected to the connector
        /// </summary>
        public static Dictionary<Vector3Int, List<(Vector3Int position, IConnectOption targetOption)>> CalculateConnectorToConnectPosList(BlockConnectInfo inputConnectInfo, BlockPositionInfo blockPositionInfo)
        {
            var blockPos = blockPositionInfo.OriginalPos;
            var blockDirection = blockPositionInfo.BlockDirection;
            var result = new Dictionary<Vector3Int, List<(Vector3Int position, IConnectOption targetOption)>>();
            
            if (inputConnectInfo == null) return result;
            foreach (var inputConnectSetting in inputConnectInfo.items)
            {
                var blockPosConvertAction = blockDirection.GetCoordinateConvertAction();
                
                var inputConnectorPos = blockPos + blockPosConvertAction(inputConnectSetting.Offset);
                var directions = inputConnectSetting.Directions;
                if (directions == null)
                {
                    result.Add(inputConnectorPos, null);
                    continue;
                }
                
                var targetPositions = directions.Select(c => (blockPosConvertAction(c) + inputConnectorPos, inputConnectSetting.ConnectOption)).ToList();
                if (!result.TryAdd(inputConnectorPos, targetPositions)) result[inputConnectorPos] = result[inputConnectorPos].Concat(targetPositions).ToList();
            }
            
            return result;
        }
        
        /// <summary>
        /// key: コネクターと接続する位置
        ///     Position to connect to the connector
        ///
        /// value: その位置と接続するコネクターの位置
        ///        Position of the connector to connect to that position
        /// </summary>
        public static Dictionary<Vector3Int, (Vector3Int position, IConnectOption selfOption)> CalculateConnectPosToConnector(BlockConnectInfo outputConnectInfo, BlockPositionInfo blockPositionInfo)
        {
            var result = new Dictionary<Vector3Int, (Vector3Int position, IConnectOption selfOption)>();
            
            if (outputConnectInfo == null) return result;
            
            var connectorToConnectPosList = CalculateConnectorToConnectPosList(outputConnectInfo, blockPositionInfo);
            foreach (var (connectPos, targetOptions) in connectorToConnectPosList)
            {
                if (targetOptions == null) continue;
                
                foreach (var (targetPos, targetOption) in targetOptions)
                {
                    // targetPosの重複は今のところ考慮しない
                    result[targetPos] = (connectPos, targetOption);
                }
            }
            
            return result;
        }
    }
}