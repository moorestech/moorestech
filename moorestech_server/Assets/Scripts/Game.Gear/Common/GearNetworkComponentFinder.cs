using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Gear.Common
{
    public static class GearNetworkComponentFinder
    {
        public static List<List<IGearEnergyTransformer>> FindComponents(IGearEnergyTransformer[] remaining, Dictionary<BlockInstanceId, int> idToIdx)
        {
            var components = new List<List<IGearEnergyTransformer>>();
            var queue = new Queue<IGearEnergyTransformer>();

            // 配列のnull化をvisitedとして使い、追加の集合を持たない。
            // Nulling array slots acts as visited state without another set.
            for (var i = 0; i < remaining.Length; i++)
            {
                var start = remaining[i];
                if (start == null) continue;

                remaining[i] = null;
                var component = new List<IGearEnergyTransformer>();
                queue.Clear();
                queue.Enqueue(start);

                // BFSで削除後も到達可能なgearだけを同じnetwork候補へ集める。
                // BFS collects only gears still reachable after the removal.
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    component.Add(current);
                    EnqueueConnectedGears(current, remaining, idToIdx, queue);
                }

                components.Add(component);
            }

            return components;
        }

        private static void EnqueueConnectedGears(
            IGearEnergyTransformer current,
            IGearEnergyTransformer[] remaining,
            Dictionary<BlockInstanceId, int> idToIdx,
            Queue<IGearEnergyTransformer> queue)
        {
            foreach (var connect in current.GetGearConnects())
            {
                if (!idToIdx.TryGetValue(connect.Transformer.BlockInstanceId, out var idx)) continue;
                if (remaining[idx] == null) continue;

                // enqueueと同時にnull化し、同じgearの重複queue投入を防ぐ。
                // Null before enqueueing to prevent duplicate queue entries.
                remaining[idx] = null;
                queue.Enqueue(connect.Transformer);
            }
        }
    }
}
