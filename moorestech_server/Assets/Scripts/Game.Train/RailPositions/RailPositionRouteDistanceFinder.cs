using Game.Train.RailCalc;

namespace Game.Train.RailPositions
{
    // 日本語: 入力RailPositionは TrainLength==0（点位置）を想定する経路距離計算ユーティリティ。
    // English: Route-distance utility assuming input RailPosition values are point positions (TrainLength==0).
    public static class RailPositionRouteDistanceFinder
    {
        // 日本語: 呼び出し側契約は「start/endともにTrainLength==0」。防御的に先頭点へ正規化して計算する。
        // English: Caller contract is start/end with TrainLength==0; defensively normalizes to head points.
        public static int FindShortestDistance(RailPosition start, RailPosition end)
        {
            if (start == null || end == null) return -1;
            // 日本語: 列車長の影響を除外し、先頭点同士の距離計算に正規化する。
            // English: Normalize both inputs to head points to ignore train length effects.
            var startHead = start.GetHeadRailPosition();
            var endHead = end.GetHeadRailPosition();
            
            // 日本語: 経路探索用にnodeを割り出す
            // English: Derive nodes for pathfinding.
            var startNode = startHead.GetNodeApproaching();
            var endNode = endHead.GetDistanceToNextNode() != 0 ? endHead.GetNodeJustPassed() : endHead.GetNodeApproaching();
            if (startNode == null || endNode == null) return -1;
            
            // 例外として、startとendがセグメント上かつ点に接してない場合。startHeadが点上(かつendHeadが任意位置)なら上記処理で解決できる。startNodeが点上でなくてもendHeadが点上なら上記処理で解決できる。なのでやるべきはstartHeadが点上でないかつendHeadも点上でない場合の、同一セグメント上で前方向にendHeadへ到達できるかの判定だけ。
            // Exception case: when start and end are on the same segment and not touching nodes. This is handled by above logic as long as startNode is on a point (and endNode can be anywhere).
            if (startHead.GetDistanceToNextNode() > 0 && endHead.GetDistanceToNextNode() > 0)
            {
                var startNextNode = startHead.GetNodeApproaching();
                var startPrevNode = startHead.GetNodeJustPassed();
                
                var endNextNode   = endHead.GetNodeApproaching();
                var endPrevNode   = endHead.GetNodeJustPassed();
                
                if (startNextNode == null || startPrevNode == null || endNextNode == null || endPrevNode == null) return -1;
                
                if (startNextNode == endNextNode && startPrevNode == endPrevNode)
                {
                    var startDistToNext = startHead.GetDistanceToNextNode();
                    var endDistToNext   = endHead.GetDistanceToNextNode();
                    
                    var delta = startDistToNext - endDistToNext;
                    if (delta >= 0)
                    {
                        return delta;
                    }
                    // endがstartより手前方向にあるので普通に経路探索
                    // English: end is before start, so do normal pathfinding.
                }
            }
            
            int distance = 0;
            // 日本語: 例外ケース：startとendが同一node上にある場合は経路探索しない
            // English: Exception case: when start and end are on the same node, do not pathfind.
            if (startNode.NodeGuid != endNode.NodeGuid)
            {
                var path = startNode.GraphProvider.FindShortestPath(startNode, endNode);
                if (path == null || path.Count == 0) return -1;
                distance = RailNodeCalculate.CalculateTotalDistanceF(path);                
            }
            
            distance += startHead.GetDistanceToNextNode();
            if (endHead.GetDistanceToNextNode() != 0)
            {
                // endHead.GetNodeJustPassed()からendHead.GetNodeApproaching()まではかってから引く
                // measure from endHead.GetNodeJustPassed() to endHead.GetNodeApproaching() and then subtract endHead.GetDistanceToNextNode().
                distance += endHead.GetNodeJustPassed().GetDistanceToNode(endHead.GetNodeApproaching()) - endHead.GetDistanceToNextNode();
            }
            return distance;
        }
    }
}
