using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component.ConnectJudge;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Component.ConnectionContext
{
    internal static class ConnectorPairJudge<TConnectJudge> where TConnectJudge : IConnectorConnectJudge, new()
    {
        private static readonly TConnectJudge Judge = new();
        /// <summary>
        ///     2ブロックのコネクタ定義から、実際に噛み合うセル対を1組だけ解く。サーバーの実接続とクライアントのプレビューが同じ規則で解くための正本
        ///     Resolves the single meshing cell pair from two blocks' connector definitions; the one rule both the server's real connection and the client's preview use
        /// </summary>
        public static bool TryJudgeConnect(
            IReadOnlyList<IBlockConnector> selfOutputConnectors, BlockPositionInfo selfPositionInfo,
            IReadOnlyList<IBlockConnector> targetInputConnectors, BlockPositionInfo targetPositionInfo,
            out Vector3Int selfConnectorCell, out Vector3Int targetConnectorCell)
        {
            selfConnectorCell = Vector3Int.zero;
            targetConnectorCell = Vector3Int.zero;

            var selfOutputs = BlockConnectorConnectPositionCalculator.CalculateConnectPosToConnector(selfOutputConnectors, selfPositionInfo);
            var targetInputs = BlockConnectorConnectPositionCalculator.CalculateConnectorToConnectPosList(targetInputConnectors, targetPositionInfo);

            foreach (var (outputTargetPos, selfOutput) in selfOutputs)
            {
                if (!targetInputs.TryGetValue(outputTargetPos, out var targetAcceptedCells)) continue;
                if (!TryJudgeConnectorPair(selfOutput, targetAcceptedCells, selfPositionInfo, targetPositionInfo, out _, out _)) continue;

                selfConnectorCell = selfOutput.position;
                targetConnectorCell = outputTargetPos;
                return true;
            }

            return false;
        }

        internal static bool TryJudgeConnectorPair(
            (Vector3Int position, IBlockConnector connector) outputConnector,
            List<(Vector3Int position, IBlockConnector connector)> targetAcceptedCells,
            BlockPositionInfo selfPositionInfo, BlockPositionInfo targetPositionInfo,
            out IBlockConnector validSelfConnector, out IBlockConnector validTargetConnector)
        {
            validSelfConnector = null;
            validTargetConnector = null;

            // 形状互換表とドメイン判定の両方を通る候補を探す
            // Find a candidate that passes both the shape table and domain judge
            foreach (var candidate in CollectPositionMatchedCandidates())
            {
                if (!MasterHolder.BlockMaster.CanConnectConnectorShapes(candidate.selfConnector?.ShapeGuid, candidate.targetConnector?.ShapeGuid)) continue;

                var judgeContext = new ConnectJudgeContext(candidate.selfConnector, candidate.targetConnector, selfPositionInfo, targetPositionInfo);
                if (!Judge.CanConnect(judgeContext)) continue;

                validSelfConnector = candidate.selfConnector;
                validTargetConnector = candidate.targetConnector;
                return true;
            }

            return false;

            #region Internal

            List<(IBlockConnector selfConnector, IBlockConnector targetConnector)> CollectPositionMatchedCandidates()
            {
                var candidates = new List<(IBlockConnector selfConnector, IBlockConnector targetConnector)>();

                // 方向無制限入力では自側コネクタだけを確定する
                // For unrestricted input, resolve only the source connector
                if (targetAcceptedCells == null)
                {
                    candidates.Add((outputConnector.connector, null));
                    return candidates;
                }

                // 同じ位置にある全ての候補ペアを評価対象に残す
                // Keep every candidate pair at the same connector position
                foreach (var target in targetAcceptedCells)
                {
                    if (target.position != outputConnector.position) continue;
                    candidates.Add((outputConnector.connector, target.connector));
                }

                return candidates;
            }

            #endregion
        }

    }
}
