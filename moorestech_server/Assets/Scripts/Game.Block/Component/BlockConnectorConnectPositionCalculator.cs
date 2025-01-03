using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Mooresmaster.Model.BlockConnectInfoModule;
using UnityEngine;

namespace Game.Block.Component
{
    public static class BlockConnectorConnectPositionCalculator
    {
        public static Dictionary<Vector3Int, List<(Vector3Int position, IConnectOption targetOption)>> CalculateInputConnectPoss(BlockConnectInfo inputConnectInfo, BlockPositionInfo blockPositionInfo)
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
        
        public static Dictionary<Vector3Int, (Vector3Int position, IConnectOption selfOption)> CalculateOutputConnectPoss(BlockConnectInfo outputConnectInfo, BlockPositionInfo blockPositionInfo)
        {
            var blockPos = blockPositionInfo.OriginalPos;
            var blockDirection = blockPositionInfo.BlockDirection;
            var blockModelOriginPos = blockDirection.GetBlockModelOriginPos(blockPositionInfo);
            var result = new Dictionary<Vector3Int, (Vector3Int position, IConnectOption selfOption)>();
            
            if (outputConnectInfo == null) return result;
            
            foreach (var connectSetting in outputConnectInfo.items)
            {
                var blockPosConvertAction = blockDirection.GetCoordinateConvertAction();
                
                var outputConnectorPos = blockPos + blockPosConvertAction(connectSetting.Offset);
                var directions = connectSetting.Directions;
                var targetPoss = directions.Select(c => blockPosConvertAction(c) + outputConnectorPos).ToList();
                
                foreach (var targetPos in targetPoss) result.Add(targetPos, (outputConnectorPos, connectSetting.ConnectOption));
            }
            
            return result;
        }
    }
}