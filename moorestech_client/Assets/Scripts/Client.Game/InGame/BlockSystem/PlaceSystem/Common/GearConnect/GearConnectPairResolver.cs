using System.Collections.Generic;
using Core.Master;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.GearConnect
{
    /// <summary>
    ///     噛み合う自コネクタと相手コネクタのセル対
    ///     A meshing pair of the own connector cell and the partner connector cell
    /// </summary>
    public readonly struct GearConnectPair
    {
        public readonly Vector3Int SelfConnectorCell;
        public readonly Vector3Int TargetConnectorCell;

        public GearConnectPair(Vector3Int selfConnectorCell, Vector3Int targetConnectorCell)
        {
            SelfConnectorCell = selfConnectorCell;
            TargetConnectorCell = targetConnectorCell;
        }
    }

    /// <summary>
    ///     設置予定の歯車ブロックが隣接ブロックのどのコネクタと繋がるかを、サーバーと同じ「位置一致→形状表→GearConnectJudge」で解く
    ///     Resolves which neighbour connectors a gear block about to be placed will mesh with, using the server's position/shape/judge rule
    /// </summary>
    public static class GearConnectPairResolver
    {
        private static readonly GearConnectJudge Judge = new();

        public static List<GearConnectPair> Resolve(BlockId selfBlockId, BlockPositionInfo selfPositionInfo, IReadOnlyList<(BlockId blockId, BlockPositionInfo positionInfo)> neighbours)
        {
            var pairs = new List<GearConnectPair>();
            if (MasterHolder.BlockMaster.GetBlockMaster(selfBlockId).BlockParam is not IGearConnectors selfGear) return pairs;

            // 自コネクタが向く先セル → (自コネクタセル, コネクタ)。サーバーの出力側と同じ引き方
            // Target cell each own connector faces → (own connector cell, connector); the same lookup the server's output side uses
            var selfOutputs = BlockConnectorConnectPositionCalculator.CalculateConnectPosToConnector(selfGear.Gear.GearConnects, selfPositionInfo);

            foreach (var (neighbourId, neighbourPositionInfo) in neighbours)
            {
                if (MasterHolder.BlockMaster.GetBlockMaster(neighbourId).BlockParam is not IGearConnectors neighbourGear) continue;

                // 相手コネクタセル → そのコネクタが受け入れるセル列。サーバーの入力側と同じ引き方
                // Partner connector cell → the cells it accepts from; the same lookup the server's input side uses
                var neighbourInputs = BlockConnectorConnectPositionCalculator.CalculateConnectorToConnectPosList(neighbourGear.Gear.GearConnects, neighbourPositionInfo);

                foreach (var (outputTargetCell, selfOutput) in selfOutputs)
                {
                    if (!neighbourInputs.TryGetValue(outputTargetCell, out var acceptedCells)) continue;
                    if (!TryConnect(selfOutput.connector, acceptedCells, selfOutput.position, neighbourPositionInfo)) continue;

                    pairs.Add(new GearConnectPair(selfOutput.position, outputTargetCell));
                }
            }
            return pairs;

            #region Internal

            // 方向無制限（accepted列がnull）は位置一致だけで通す。制限付きは受け入れ元が自コネクタセルと一致すること
            // Unrestricted partners (a null accepted list) pass on position alone; restricted ones must accept from the own connector cell
            bool TryConnect(IBlockConnector selfConnector, List<(Vector3Int position, IBlockConnector connector)> acceptedCells, Vector3Int selfConnectorCell, BlockPositionInfo neighbourPositionInfo)
            {
                if (acceptedCells == null) return CanConnect(selfConnector, null, neighbourPositionInfo);

                foreach (var accepted in acceptedCells)
                {
                    if (accepted.position != selfConnectorCell) continue;
                    if (CanConnect(selfConnector, accepted.connector, neighbourPositionInfo)) return true;
                }
                return false;
            }

            bool CanConnect(IBlockConnector selfConnector, IBlockConnector targetConnector, BlockPositionInfo neighbourPositionInfo)
            {
                if (!MasterHolder.BlockMaster.CanConnectConnectorShapes(selfConnector?.ShapeGuid, targetConnector?.ShapeGuid)) return false;
                return Judge.CanConnect(new ConnectJudgeContext(selfConnector, targetConnector, selfPositionInfo, neighbourPositionInfo));
            }

            #endregion
        }
    }
}
