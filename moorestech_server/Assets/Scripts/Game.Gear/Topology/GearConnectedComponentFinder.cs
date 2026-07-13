using System.Collections.Generic;
using Game.Block.Interface;
using Game.Gear.Common;

namespace Game.Gear.Topology
{
    // gear削除後の残存集合を連結成分へ分解するBFS。訪問済みは配列スロットのnull化で表現する
    // BFS decomposing the surviving gears into connected components; visited state is encoded by null-ing array slots
    public static class GearConnectedComponentFinder
    {
        public static List<List<IGearEnergyTransformer>> FindComponents(IGearEnergyTransformer[] remaining, Dictionary<BlockInstanceId, int> idToIndex)
        {
            var components = new List<List<IGearEnergyTransformer>>();
            var queue = new Queue<IGearEnergyTransformer>();

            // 全スロットを昇順に走査し、未回収のgearを新しい連結成分の起点として採用する
            // Walk every slot in ascending order; any gear not yet consumed seeds a new connected component
            for (var i = 0; i < remaining.Length; i++)
            {
                var start = remaining[i];
                if (start == null) continue;

                // 起点を先にnull化して重複登録を防ぐ
                // Null out the seed first so it can never be enqueued twice
                remaining[i] = null;
                var component = new List<IGearEnergyTransformer>();
                queue.Clear();
                queue.Enqueue(start);

                // キューが空になるまで到達可能なgearをこの成分へ集める
                // Drain the queue, collecting every reachable gear into this component
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    component.Add(current);

                    foreach (var connect in current.GetGearConnects())
                    {
                        // idToIndexに無い接続先は残存集合の外（削除gear・別ネット）なので辺ごと遮断する
                        // Neighbors missing from idToIndex lie outside the surviving set (removed gear or another network); cut the edge
                        if (!idToIndex.TryGetValue(connect.Transformer.BlockInstanceId, out var index)) continue;
                        if (remaining[index] == null) continue;
                        remaining[index] = null;
                        queue.Enqueue(connect.Transformer);
                    }
                }

                components.Add(component);
            }

            return components;
        }
    }
}
