using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Client.UitkPoc.RecipeGraph.Layout
{
    // dagre相当の階層レイアウト（Sugiyama法）。原料を左、目標を右に置き、長い辺は中継点で折る
    // Sugiyama-style layered layout like dagre: raw on the left, target on the right, long edges bent via dummies
    public static class LayeredGraphLayout
    {
        public const float DummyHeight = 12f;

        public static LayoutResult Compute(float[] nodeHeights, List<(int from, int to)> edges, float nodeWidth, float columnGap, float rowGap)
        {
            // 1. 各ノードの目標までの最長距離を段とする
            // 1. Rank each node by its longest path to the sink
            var realCount = nodeHeights.Length;
            var rank = new int[realCount];
            var outgoing = Enumerable.Range(0, realCount).Select(_ => new List<int>()).ToArray();
            foreach (var (from, to) in edges) outgoing[from].Add(to);
            var memo = new bool[realCount];
            for (var v = 0; v < realCount; v++) RankOf(v);
            var maxRank = rank.Max();

            // 2. 2段以上またぐ辺に中継点を挟み、隣接段だけの辺に分解する
            // 2. Split edges spanning several ranks with dummy nodes
            var heights = new List<float>(nodeHeights);
            var nodeRank = new List<int>(rank);
            var segments = new List<(int upper, int lower)>();
            var edgeChains = new List<int>[edges.Count];
            for (var e = 0; e < edges.Count; e++) SplitEdge(e);

            // 3. 段ごとの並びを重心法で詰め、4. 縦位置を割り当てる
            // 3. Order each rank by barycenters, then 4. assign vertical positions
            var layers = Enumerable.Range(0, maxRank + 1).Select(r => Enumerable.Range(0, heights.Count).Where(v => nodeRank[v] == r).ToList()).ToList();
            LayerOrdering.Minimize(layers, segments, heights.Count, 16);
            var y = LayerCoordinateAssigner.Assign(layers, segments, heights, rowGap, 24);

            var result = new LayoutResult(realCount, edges.Count);
            var columnWidth = nodeWidth + columnGap;
            for (var v = 0; v < realCount; v++) result.NodePositions[v] = new Vector2((maxRank - rank[v]) * columnWidth, y[v]);
            for (var e = 0; e < edges.Count; e++)
            {
                foreach (var dummy in edgeChains[e]) result.EdgeWaypoints[e].Add(new Vector2((maxRank - nodeRank[dummy]) * columnWidth + nodeWidth / 2f, y[dummy] + DummyHeight / 2f));
            }
            result.SetSize(new Vector2((maxRank + 1) * columnWidth - columnGap, Enumerable.Range(0, heights.Count).Max(v => y[v] + heights[v])));
            return result;

            #region Internal

            int RankOf(int v)
            {
                if (memo[v]) return rank[v];
                memo[v] = true;
                var best = 0;
                foreach (var consumer in outgoing[v]) best = Mathf.Max(best, RankOf(consumer) + 1);
                rank[v] = best;
                return best;
            }

            void SplitEdge(int e)
            {
                var (from, to) = edges[e];
                edgeChains[e] = new List<int>();
                var previous = from;
                // 生産側（高い段）から消費側へ1段ずつ中継点を置く
                // Place one dummy per rank from producer (higher rank) toward consumer
                for (var r = rank[from] - 1; r > rank[to]; r--)
                {
                    var dummy = heights.Count;
                    heights.Add(DummyHeight);
                    nodeRank.Add(r);
                    segments.Add((previous, dummy));
                    edgeChains[e].Add(dummy);
                    previous = dummy;
                }
                segments.Add((previous, to));
            }

            #endregion
        }
    }

    public class LayoutResult
    {
        public readonly Vector2[] NodePositions;
        public readonly List<Vector2>[] EdgeWaypoints;
        public Vector2 Size { get; private set; }

        public LayoutResult(int nodeCount, int edgeCount)
        {
            NodePositions = new Vector2[nodeCount];
            EdgeWaypoints = Enumerable.Range(0, edgeCount).Select(_ => new List<Vector2>()).ToArray();
        }

        public void SetSize(Vector2 size)
        {
            Size = size;
        }
    }
}
