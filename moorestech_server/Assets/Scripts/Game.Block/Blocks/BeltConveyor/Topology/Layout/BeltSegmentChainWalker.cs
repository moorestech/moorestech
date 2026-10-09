using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Block.Blocks.BeltConveyor.Topology.Layout
{
    // マス一覧を、1本のsegmentにまとめられる連鎖へ切り分ける
    // Splits the cell list into chains that each become one segment
    // まとめられる条件: 送り側が分配器でも合流でもなく出力がベルト1本、受け側の入力がその1本だけ、速度が同じ
    // A link is kept when the sender is neither a splitter nor a merge and has exactly one belt output, the receiver has only that input, and speeds match
    internal static class BeltSegmentChainWalker
    {
        // 連鎖は先頭マスの座標順。輪は先行マスを持たないので、残ったマスのうち座標最小のものを先頭にする
        // Chains come in head cell position order; a ring has no head, so its smallest-position cell starts it
        internal static List<int[]> Walk(List<BeltTopologyCell> cells, Dictionary<BlockInstanceId, int> cellIndexByBlock)
        {
            var successor = new int[cells.Count];
            var hasPredecessor = new bool[cells.Count];
            for (var i = 0; i < cells.Count; i++)
            {
                successor[i] = FindSuccessor(i);
                if (successor[i] >= 0) hasPredecessor[successor[i]] = true;
            }

            // 先行マスを持たないマスから歩き、残りは輪として座標順に歩く
            // Walk from cells without a predecessor, then walk the leftovers as rings in position order
            var visited = new bool[cells.Count];
            var chains = new List<int[]>();
            for (var i = 0; i < cells.Count; i++)
                if (!hasPredecessor[i]) chains.Add(WalkFrom(i));
            for (var i = 0; i < cells.Count; i++)
                if (!visited[i]) chains.Add(WalkFrom(i));
            chains.Sort(CompareByHead);
            return chains;

            #region Internal

            int FindSuccessor(int index)
            {
                var cell = cells[index];
                if (cell.IsSplitter || 2 <= cell.Inputs.Length || cell.Outputs.Length != 1) return -1;
                ref readonly var output = ref cell.Outputs[0];
                if (output.PartnerKind != BeltTopologyPartnerKind.Belt || !cellIndexByBlock.TryGetValue(output.PartnerBlock.BlockInstanceId, out var next)) return -1;
                var target = cells[next];
                return target.Inputs.Length == 1 && target.BeltSpeedPerTick == cell.BeltSpeedPerTick ? next : -1;
            }

            int[] WalkFrom(int start)
            {
                var chain = new List<int>();
                var current = start;
                while (0 <= current && !visited[current])
                {
                    visited[current] = true;
                    chain.Add(current);
                    current = successor[current];
                }
                return chain.ToArray();
            }

            int CompareByHead(int[] a, int[] b)
            {
                return BeltTopologyGeometry.ComparePosition(cells[a[0]].Position, cells[b[0]].Position);
            }

            #endregion
        }
    }
}
