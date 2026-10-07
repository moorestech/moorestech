using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailPositions;

namespace Server.Protocol.PacketResponse.Util.RailEdit
{
    // 列車が占有中のノードを持つブロックの撤去を防ぐ
    // Prevent removing blocks whose rail nodes are occupied by a train
    internal static class RailBlockRemovalGuard
    {
        internal static bool CanRemove(IBlock targetBlock, TrainRailPositionManager railPositionManager)
        {
            var railComponents = targetBlock.ComponentManager.GetComponents<RailComponent>();
            if (railComponents.Count == 0) return true;

            // レール系ブロックは列車位置が保持するノードを壊せない
            // Rail blocks cannot remove nodes currently held by train positions.
            for (var i = 0; i < railComponents.Count; i++)
            {
                if (!CanManualRemoveRailComponent(railComponents[i])) return false;
            }

            return true;

            #region Internal

            bool CanManualRemoveRailComponent(RailComponent railComponent)
            {
                // 橋脚削除はFront/Back両ノードの削除と同義として扱う
                // Removing a pier is equivalent to removing both front and back nodes.
                if (!railPositionManager.CanRemoveNode(railComponent.FrontNode)) return false;
                if (!railPositionManager.CanRemoveNode(railComponent.BackNode)) return false;

                return true;
            }

            #endregion
        }
    }
}
