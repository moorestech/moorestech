using System.Collections.Generic;

namespace Game.Block.Blocks.BeltConveyor.Topology
{
    // マス一覧の並び順。比較子は静的に1つだけ持ち、並べ替えのたびにデリゲートを作らない
    // Ordering of the cell list; comparers are single static instances so sorting never allocates a delegate
    internal static class BeltTopologyOrder
    {
        internal static readonly IComparer<BeltTopologyCell> Cell = new CellComparer();
        internal static readonly IComparer<BeltTopologyConnection> Connection = new ConnectionComparer();

        // 接続は方向値、次に相手マスの座標で並べる
        // Connections are ordered by direction value, then by partner cell position
        private sealed class ConnectionComparer : IComparer<BeltTopologyConnection>
        {
            public int Compare(BeltTopologyConnection a, BeltTopologyConnection b)
            {
                var byDirection = ((int)a.Direction).CompareTo((int)b.Direction);
                return byDirection != 0 ? byDirection : BeltTopologyGeometry.ComparePosition(a.PartnerCell, b.PartnerCell);
            }
        }

        private sealed class CellComparer : IComparer<BeltTopologyCell>
        {
            public int Compare(BeltTopologyCell a, BeltTopologyCell b)
            {
                return BeltTopologyGeometry.ComparePosition(a.Position, b.Position);
            }
        }
    }
}
