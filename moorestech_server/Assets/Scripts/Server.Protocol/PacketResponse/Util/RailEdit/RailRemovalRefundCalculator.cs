using Core.Master;
using Game.Block.Blocks.ConnectionLine;
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailGraph;
using Game.Train.RailGraph.Utility;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.RailEdit
{
    /// <summary>
    ///     レールを持つブロックの撤去で一緒に消えるレール区間の返却アイテムを算出する
    ///     Computes refund items for the rail segments that vanish when a rail-holding block is removed
    /// </summary>
    public static class RailRemovalRefundCalculator
    {
        // 区間1本の返却素材を算出する。無償区間はfalse（ログなし）、メタ欠落・算出不能はログを残してfalse
        // Compute one segment's refund materials; costless segments return false silently, missing metadata or uncomputable cost logs and returns false
        public static bool TryCalculateSegmentRefundMaterials(IRailGraphDatastore railGraphDatastore, IRailNode from, IRailNode to, out IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            materials = null;

            // 駅内部・駅隣接の自動接続はGuid.Emptyの無償区間なので返さない
            // Station-internal and adjacency auto links are costless Guid.Empty segments, so skip them
            if (!railGraphDatastore.TryGetRailSegmentType(from.NodeId, to.NodeId, out var connectToolGuid))
            {
                Debug.LogWarning($"[RailRemovalRefund] refund skipped: segment metadata missing. from={from.NodeId} to={to.NodeId}");
                return false;
            }
            if (connectToolGuid == Guid.Empty) return false;

            // 現在の曲線長から返却素材を算出する（切断・撤去で共通）
            // Compute the refund from the current curve length (shared by disconnect and removal)
            var length = RailConnectionEditProtocol.GetRailLength(from, to);
            if (!ConnectToolCostCalculator.TryCalculate(connectToolGuid, length, out materials))
            {
                Debug.LogWarning($"[RailRemovalRefund] refund skipped: cost not computable. connectToolGuid={connectToolGuid} length={length} from={from.NodeId} to={to.NodeId}");
                return false;
            }
            return true;
        }

        public static List<IItemStack> CreateRefundItems(IBlock block, IRailGraphDatastore railGraphDatastore)
        {
            var refundItems = new List<IItemStack>();
            var countedRails = new HashSet<(int, int)>();

            // ブロックの全ノードから出る区間を見て、物理レール1本を1回だけ数える
            // Walk every edge leaving the block's nodes and count each physical rail once
            foreach (var railComponent in block.ComponentManager.GetComponents<RailComponent>())
            {
                AddRefundOfNode(railComponent.FrontNode);
                AddRefundOfNode(railComponent.BackNode);
            }

            return refundItems;

            #region Internal

            void AddRefundOfNode(RailNode node)
            {
                foreach (var (target, _) in railGraphDatastore.GetConnectedNodesWithDistance(node))
                {
                    if (!countedRails.Add(RailSegmentPairing.SelectCanonicalPair(node.NodeId, target.NodeId))) continue;
                    AddRefundOfSegment(node, target);
                }
            }

            void AddRefundOfSegment(IRailNode from, IRailNode to)
            {
                if (!TryCalculateSegmentRefundMaterials(railGraphDatastore, from, to, out var materials)) return;
                refundItems.AddRange(ConnectionLineRefundItems.Create(materials));
            }

            #endregion
        }
    }
}
