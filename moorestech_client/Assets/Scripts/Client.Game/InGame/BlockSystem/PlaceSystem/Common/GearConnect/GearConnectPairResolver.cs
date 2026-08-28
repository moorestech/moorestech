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
    ///     設置予定の歯車ブロックが隣接ブロックのどのコネクタと繋がるかを、サーバーの実接続と同じ判定へ委ねて解く
    ///     Resolves which neighbour connectors a gear block about to be placed will mesh with, delegating to the very judge the server's real connection uses
    /// </summary>
    public static class GearConnectPairResolver
    {
        public static List<GearConnectPair> Resolve(BlockId selfBlockId, BlockPositionInfo selfPositionInfo, IReadOnlyList<(BlockId blockId, BlockPositionInfo positionInfo)> neighbours)
        {
            var pairs = new List<GearConnectPair>();
            if (MasterHolder.BlockMaster.GetBlockMaster(selfBlockId).BlockParam is not IGearConnectors selfGear) return pairs;

            foreach (var (neighbourId, neighbourPositionInfo) in neighbours)
            {
                if (MasterHolder.BlockMaster.GetBlockMaster(neighbourId).BlockParam is not IGearConnectors neighbourGear) continue;

                // 歯車は同じコネクタ定義を入力にも出力にも使う（各Templateが同一リストを両側へ渡している）
                // A gear uses the same connector list for both input and output, exactly as every gear template passes it
                if (!BlockConnectorComponent<IGearEnergyTransformer, GearConnectJudge>.TryJudgeConnect(
                        selfGear.Gear.GearConnects, selfPositionInfo,
                        neighbourGear.Gear.GearConnects, neighbourPositionInfo,
                        out var selfConnectorCell, out var targetConnectorCell)) continue;

                pairs.Add(new GearConnectPair(selfConnectorCell, targetConnectorCell));
            }

            return pairs;
        }
    }
}
