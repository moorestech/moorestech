using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch
{
    /// <summary>
    ///     既存のベルトを分岐の始点にできない理由
    ///     Why an existing belt cannot become a branch start
    /// </summary>
    public enum BeltRouteBranchBlockReason
    {
        None,
        NotStraightBelt,
        NoSplitter,
        MultipleInputs,
        UnusedExitHasReceiver,
    }

    /// <summary>
    ///     分岐器の向きと出口の割り当て
    ///     向き=入ってくる向き
    ///     出口=新経路と残り
    ///     The splitter's facing and exit assignment
    ///     Facing = where items come in from
    ///     Exits = the new route's and the remaining one
    /// </summary>
    public readonly struct BeltRouteBranchShape
    {
        public readonly BlockDirection SplitterFacing;
        public readonly BlockDirection BranchExit;

        public BeltRouteBranchShape(BlockDirection splitterFacing, BlockDirection branchExit)
        {
            SplitterFacing = splitterFacing;
            BranchExit = branchExit;
        }
    }

    /// <summary>
    ///     入出力辺だけから向きと出口を決める
    ///     Decides the facing and exits from port edges alone
    /// </summary>
    public static class BeltRouteBranchGeometry
    {
        // edges は対象ベルト自身を除いた隣の辺だけを写した入れ物（辺は向きを持たないので、自分の入力口を混ぜると受け手と区別できない）
        // edges holds only the neighbours' edges, never the belt's own (an edge has no direction, so the belt's own input port would read as a receiver)
        // 分岐器の入口は背面1つ。背面・左・右のどの面から受けているかを数え、2方向以上なら置けない
        // A splitter has one back entry; count which of the back, left and right faces feed the belt, and two or more cannot be placed
        public static BeltRouteBranchBlockReason Resolve(BeltRouteNeighbourEdges edges, Vector3Int cell, BlockDirection beltFacing, bool useAlternateExit, out BeltRouteBranchShape shape)
        {
            shape = default;
            var fedFromBack = IsFed(edges, cell, BeltRouteDirection.Opposite(beltFacing));
            var fedFromLeft = IsFed(edges, cell, BeltRouteDirection.Left(beltFacing));
            var fedFromRight = IsFed(edges, cell, BeltRouteDirection.Right(beltFacing));
            var sideFeeds = (fedFromLeft ? 1 : 0) + (fedFromRight ? 1 : 0);
            // silent-ok: 理由行「2方向以上からアイテムを受けています」で分岐不可と分かる
            if (sideFeeds == 2 || (fedFromBack && sideFeeds == 1)) return BeltRouteBranchBlockReason.MultipleInputs;

            // 直線は入ってくる向きがベルトの向き。角は横の面から受けるので、入ってくる向きはその面の反対
            // A straight belt's inflow is its facing; a corner is fed through a side face, so the inflow is that face's opposite
            var isCorner = sideFeeds == 1;
            var flow = isCorner ? (fedFromLeft ? BeltRouteDirection.Right(beltFacing) : BeltRouteDirection.Left(beltFacing)) : beltFacing;

            // 既存の続きはベルトの向きの出口を使う。残り2つのうち、直線は右が既定、角は正面が既定（ユーザー裁定 2026-11-09）
            // The existing continuation uses the exit in the belt's facing; of the other two, a straight belt defaults to right and a corner to the front (user decision 2026-11-09)
            // 角で空いている横は元のベルトの背面（入ってくる面でも続きの面でもない残りの面）
            // A corner's free side is the original belt's back, the one face that is neither the inflow nor the continuation
            var primary = isCorner ? flow : BeltRouteDirection.Right(flow);
            var alternate = isCorner ? BeltRouteDirection.Opposite(beltFacing) : BeltRouteDirection.Left(flow);
            var branchExit = useAlternateExit ? alternate : primary;
            var unusedExit = useAlternateExit ? primary : alternate;

            // 分岐器は使わない出口にも流すので、その先に受け手がいてはいけない
            // The splitter also emits through its unused exit, so nothing may receive there
            // silent-ok: 理由行「使わない出口の隣に受け手があります」で分岐不可と分かる
            if (edges.HasInputEdge(BeltRouteEdge.FromFace(cell, unusedExit, cell.y))) return BeltRouteBranchBlockReason.UnusedExitHasReceiver;

            shape = new BeltRouteBranchShape(flow, branchExit);
            return BeltRouteBranchBlockReason.None;
        }

        private static bool IsFed(BeltRouteNeighbourEdges edges, Vector3Int cell, BlockDirection face)
        {
            return edges.HasOutputEdge(BeltRouteEdge.FromFace(cell, face, cell.y));
        }
    }
}
