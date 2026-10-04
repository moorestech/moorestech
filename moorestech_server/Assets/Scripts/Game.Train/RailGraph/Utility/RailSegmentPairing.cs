namespace Game.Train.RailGraph.Utility
{
    /// <summary>
    /// 物理レール1本を表す有向区間の対（A→B と (B^1)→(A^1)）を1つのキーへ正規化する正本
    /// Canonical normalization of the directed-edge pair (A→B and (B^1)→(A^1)) that represents one physical rail
    /// </summary>
    public static class RailSegmentPairing
    {
        // 起点Idが小さい方を正とし、同値なら A→B を返す（同値のときは対が自分自身と一致する）
        // The pair with the smaller start id is canonical; ties return A→B (on a tie the pair equals itself)
        public static (int canonicalFrom, int canonicalTo) SelectCanonicalPair(int fromNodeId, int toNodeId)
        {
            var pairedFrom = toNodeId ^ 1;
            var pairedTo = fromNodeId ^ 1;
            return fromNodeId <= pairedFrom ? (fromNodeId, toNodeId) : (pairedFrom, pairedTo);
        }
    }
}
