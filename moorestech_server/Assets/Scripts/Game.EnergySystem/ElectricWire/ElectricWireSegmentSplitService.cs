using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    // 登録済み電線コネクタを隣接辺一回のBFSで連結成分へ分解する
    // Decomposes registered wire connectors into components with one BFS pass over adjacency edges
    internal static class ElectricWireSegmentSplitService
    {
        public static List<List<IElectricWireConnector>> FindComponents(
            IElectricWireConnector[] remaining,
            IReadOnlyDictionary<BlockInstanceId, int> idToIndex)
        {
            var components = new List<List<IElectricWireConnector>>();
            var queue = new Queue<IElectricWireConnector>();

            // 各頂点を一度だけBFS起点にする
            // Consider each registered vertex once as a seed and encode visits by nulling its array slot
            for (var i = 0; i < remaining.Length; i++)
            {
                var start = remaining[i];
                if (start == null) continue;
                remaining[i] = null;
                var component = new List<IElectricWireConnector>();
                queue.Enqueue(start);

                // live登録外の接続先を除外する
                // Resolve neighbor IDs through the live lookup so stale unregistered references never enter the graph
                while (0 < queue.Count)
                {
                    var current = queue.Dequeue();
                    component.Add(current);
                    foreach (var connection in current.WireConnections.Values)
                    {
                        if (!idToIndex.TryGetValue(connection.Connector.BlockInstanceId, out var index)) continue;
                        var registeredNeighbor = remaining[index];
                        if (registeredNeighbor == null) continue;
                        remaining[index] = null;
                        queue.Enqueue(registeredNeighbor);
                    }
                }

                components.Add(component);
            }

            return components;
        }
    }
}
