using System.Collections.Generic;
using Game.Block.Interface;
using Game.Gear.Common;

namespace Game.Gear.Topology
{
    // 登録済みgearを隣接辺一回のBFSで連結成分へ分解する
    // Decomposes registered gears into components with one BFS pass over adjacency edges
    internal static class GearConnectedComponentFinder
    {
        public static List<List<IGearEnergyTransformer>> FindComponents(
            IGearEnergyTransformer[] remaining,
            IReadOnlyDictionary<BlockInstanceId, int> idToIndex)
        {
            var components = new List<List<IGearEnergyTransformer>>();
            var queue = new Queue<IGearEnergyTransformer>();

            // 各頂点を一度だけBFS起点にする
            // Consider each registered vertex once as a seed and encode visits by nulling its array slot
            for (var i = 0; i < remaining.Length; i++)
            {
                var start = remaining[i];
                if (start == null) continue;
                remaining[i] = null;
                var component = new List<IGearEnergyTransformer>();
                queue.Enqueue(start);

                // live登録外の接続先を除外する
                // Resolve neighbor IDs through the live lookup so stale unregistered references never enter the graph
                while (0 < queue.Count)
                {
                    var current = queue.Dequeue();
                    component.Add(current);
                    foreach (var connection in current.GetGearConnects())
                    {
                        if (!idToIndex.TryGetValue(connection.Transformer.BlockInstanceId, out var index)) continue;
                        var registeredNeighbor = remaining[index];
                        if (registeredNeighbor == null) continue;
                        remaining[index] = null;
                        queue.Enqueue(registeredNeighbor);
                    }
                }

                // 成分内の並びもID昇順へ正準化する（BFS到達順は隣接リストの履歴に依存するため）
                // Canonicalize member order by ID too; BFS visit order depends on adjacency-list history
                component.Sort((a, b) => a.BlockInstanceId.CompareTo(b.BlockInstanceId));
                components.Add(component);
            }

            return components;
        }
    }
}
