using System.Collections.Generic;
using System.Linq;

namespace Client.UitkPoc.RecipeGraph.Layout
{
    // 隣接段の重心で並べ替えを往復し、辺の交差を減らす
    // Sweeps barycenter sorting back and forth between adjacent ranks to reduce crossings
    public static class LayerOrdering
    {
        public static void Minimize(List<List<int>> layers, List<(int upper, int lower)> segments, int nodeCount, int sweepCount)
        {
            // segmentsのupperは段が1つ高い（左）側、lowerは低い（右）側
            // In segments, upper is one rank higher (left), lower is one rank lower (right)
            var towardLower = Enumerable.Range(0, nodeCount).Select(_ => new List<int>()).ToArray();
            var towardUpper = Enumerable.Range(0, nodeCount).Select(_ => new List<int>()).ToArray();
            foreach (var (upper, lower) in segments)
            {
                towardLower[upper].Add(lower);
                towardUpper[lower].Add(upper);
            }
            var position = new float[nodeCount];
            foreach (var layer in layers) Reindex(layer);

            for (var sweep = 0; sweep < sweepCount; sweep++)
            {
                // 偶数回は目標側から原料側へ、奇数回はその逆へ掃く
                // Even sweeps go target to raw, odd sweeps go back
                if (sweep % 2 == 0) for (var r = 1; r < layers.Count; r++) SortByNeighbors(layers[r], towardLower);
                else for (var r = layers.Count - 2; r >= 0; r--) SortByNeighbors(layers[r], towardUpper);
            }

            #region Internal

            void Reindex(List<int> layer)
            {
                for (var i = 0; i < layer.Count; i++) position[layer[i]] = i;
            }

            void SortByNeighbors(List<int> layer, List<int>[] neighbors)
            {
                // 隣が無いノードは現在位置を重心とみなして据え置く
                // Nodes without neighbors keep their current index as barycenter
                var barycenter = layer.ToDictionary(v => v, v => neighbors[v].Count == 0 ? position[v] : neighbors[v].Average(n => position[n]));
                var sorted = layer.OrderBy(v => barycenter[v]).ThenBy(v => position[v]).ToList();
                layer.Clear();
                layer.AddRange(sorted);
                Reindex(layer);
            }

            #endregion
        }
    }
}
