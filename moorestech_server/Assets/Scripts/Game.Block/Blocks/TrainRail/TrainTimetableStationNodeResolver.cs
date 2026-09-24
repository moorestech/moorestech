using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Train.RailGraph;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Game.Block.Blocks.TrainRail
{
    // 駅ブロックから時刻表に登録するノードを解決する
    // Resolve the node registered in a timetable from a station block
    public static class TrainTimetableStationNodeResolver
    {
        public static bool TryResolve(IBlock block, out IRailNode node)
        {
            node = null;
            if (block == null) return false;
            if (block.BlockMasterElement.BlockType != BlockTypeConst.TrainStation) return false;

            // 駅のレールからBack側Exitノードを探す
            // Find the back-side exit node among the station rails
            foreach (var rail in block.GetComponents<RailComponent>())
            {
                if (IsBackExit(rail.BackNode)) { node = rail.BackNode; return true; }
                if (IsBackExit(rail.FrontNode)) { node = rail.FrontNode; return true; }
            }

            return false;

            #region Internal

            bool IsBackExit(RailNode candidate)
            {
                if (candidate == null || candidate.StationRef == null) return false;
                return candidate.StationRef.NodeSide == StationNodeSide.Back && candidate.StationRef.NodeRole == StationNodeRole.Exit;
            }

            #endregion
        }
    }
}
