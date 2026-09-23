using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component.ConnectJudge;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Component
{
    internal static class BlockConnectorCandidateMatcher
    {
        internal static bool TryJudgeConnect(IReadOnlyList<IBlockConnector> selfOutputConnectors,
            BlockPositionInfo selfPositionInfo, IReadOnlyList<IBlockConnector> targetInputConnectors,
            BlockPositionInfo targetPositionInfo, IConnectorConnectJudge judge,
            out Vector3Int selfConnectorCell, out Vector3Int targetConnectorCell)
        {
            selfConnectorCell = Vector3Int.zero;
            targetConnectorCell = Vector3Int.zero;
            var outputs = BlockConnectorConnectPositionCalculator.CalculateConnectPosToConnector(
                selfOutputConnectors, selfPositionInfo);
            var inputs = BlockConnectorConnectPositionCalculator.CalculateConnectorToConnectPosList(
                targetInputConnectors, targetPositionInfo);
            foreach (var (targetPos, selfOutput) in outputs)
            {
                if (!inputs.TryGetValue(targetPos, out var accepted)) continue;
                if (!TryJudgeConnectorPair(selfOutput, accepted, selfPositionInfo,
                        targetPositionInfo, judge, out _, out _)) continue;
                selfConnectorCell = selfOutput.position;
                targetConnectorCell = targetPos;
                return true;
            }
            return false;
        }

        internal static bool TryJudgeConnectorPair(
            (Vector3Int position, IBlockConnector connector) output,
            List<(Vector3Int position, IBlockConnector connector)> accepted,
            BlockPositionInfo selfPosition, BlockPositionInfo targetPosition,
            IConnectorConnectJudge judge, out IBlockConnector selfConnector,
            out IBlockConnector targetConnector)
        {
            selfConnector = null;
            targetConnector = null;
            if (accepted == null)
                return Accept(output.connector, null, selfPosition, targetPosition, judge,
                    out selfConnector, out targetConnector);
            foreach (var candidate in accepted)
            {
                if (candidate.position != output.position) continue;
                if (Accept(output.connector, candidate.connector, selfPosition, targetPosition, judge,
                        out selfConnector, out targetConnector)) return true;
            }
            return false;
        }

        private static bool Accept(IBlockConnector source, IBlockConnector target,
            BlockPositionInfo selfPosition, BlockPositionInfo targetPosition,
            IConnectorConnectJudge judge, out IBlockConnector selfConnector,
            out IBlockConnector targetConnector)
        {
            selfConnector = null;
            targetConnector = null;
            if (!MasterHolder.BlockMaster.CanConnectConnectorShapes(source?.ShapeGuid, target?.ShapeGuid)) return false;
            if (!judge.CanConnect(new ConnectJudgeContext(source, target, selfPosition, targetPosition))) return false;
            selfConnector = source;
            targetConnector = target;
            return true;
        }
    }
}
