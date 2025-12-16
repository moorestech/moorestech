using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    /// <summary>
    /// 肥大化するRailGraphDatastore から切り出したIDベースの経路探索クラス
    /// Delegates ID-based path finding to a reusable Dijkstra helper
    /// </summary>
    internal sealed class RailGraphPathFinder
    {
        private readonly RailGraphIdPathFinder _idPathFinder;

        public RailGraphPathFinder()
        {
            _idPathFinder = new RailGraphIdPathFinder();
        }

        public List<RailNode> FindShortestPath(
            List<RailNode> railNodes,
            List<List<(int targetId, int distance)>> connectNodes,
            int startId,
            int targetId)
        {
            // IDベースで経路を探索し、RailNodeへ変換する
            // Search by node ids then map results back to RailNode instances
            var pathResult = _idPathFinder.FindShortestPath(connectNodes, startId, targetId);
            if (pathResult.Distance < 0)
                return new List<RailNode>();
            return ConvertToNodes(pathResult.Path, railNodes);

            #region Internal

            List<RailNode> ConvertToNodes(IReadOnlyList<int> pathIds, List<RailNode> nodes)
            {
                var mapped = new List<RailNode>(pathIds.Count);
                for (var i = 0; i < pathIds.Count; i++)
                {
                    var id = pathIds[i];
                    mapped.Add(id >= 0 && id < nodes.Count ? nodes[id] : null);
                }
                return mapped;
            }

            #endregion
        }

        public RailPathResult FindShortestPathIds(
            List<List<(int targetId, int distance)>> connectNodes,
            int startId,
            int targetId)
        {
            // RailNode参照を伴わないパス探索を提供する
            // Provide id-only path search for cache-driven callers
            return _idPathFinder.FindShortestPath(connectNodes, startId, targetId);
        }
    }
}
