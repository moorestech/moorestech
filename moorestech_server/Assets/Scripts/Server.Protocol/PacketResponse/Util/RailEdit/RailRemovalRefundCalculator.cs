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
        public static bool TryCreateRefundItems(IBlock block, IRailGraphDatastore railGraphDatastore, out List<IItemStack> items)
        {
            var refundItems = new List<IItemStack>();
            items = refundItems;
            var countedRails = new HashSet<(int, int)>();

            // ブロックの全ノードから出る区間を見て、物理レール1本を1回だけ数える
            // Walk every edge leaving the block's nodes and count each physical rail once
            foreach (var railComponent in block.ComponentManager.GetComponents<RailComponent>())
            {
                if (AddRefundOfNode(railComponent.FrontNode) && AddRefundOfNode(railComponent.BackNode)) continue;
                refundItems.Clear();
                return false;
            }

            return true;

            #region Internal

            bool AddRefundOfNode(RailNode node)
            {
                foreach (var (target, _) in railGraphDatastore.GetConnectedNodesWithDistance(node))
                {
                    if (!countedRails.Add(RailSegmentPairing.SelectCanonicalPair(node.NodeId, target.NodeId))) continue;
                    if (!AddRefundOfSegment(node, target)) return false;
                }

                return true;
            }

            bool AddRefundOfSegment(IRailNode from, IRailNode to)
            {
                // 駅内部・駅隣接の自動接続はGuid.Emptyの無償区間なので返さない
                // Station-internal and adjacency auto links are costless Guid.Empty segments, so skip them
                if (!railGraphDatastore.TryGetRailSegmentType(from.NodeId, to.NodeId, out var connectToolGuid))
                {
                    Debug.LogWarning($"[RailRemovalRefund] removal denied: segment metadata missing. from={from.NodeId} to={to.NodeId}");
                    return false;
                }
                if (connectToolGuid == Guid.Empty) return true;

                // 切断時と同じく現在の曲線長から返却素材を算出する
                // Compute the refund from the current curve length, exactly like a manual disconnect
                var length = RailConnectionEditProtocol.GetRailLength(from, to);
                if (!ConnectToolCostCalculator.TryCalculate(connectToolGuid, length, out var materials))
                {
                    Debug.LogWarning($"[RailRemovalRefund] removal denied: cost not computable. connectToolGuid={connectToolGuid} length={length} from={from.NodeId} to={to.NodeId}");
                    return false;
                }

                refundItems.AddRange(ConnectToolMaterialConsumer.CreateRefundItems(materials));
                return true;
            }

            #endregion
        }
    }
}
