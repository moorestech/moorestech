using System.Collections.Generic;
using System.Linq;

namespace Client.UitkPoc.RecipeGraph.Layout
{
    // 並び順を保ったまま、隣接段の相手の中心へ寄せる縦位置を等間隔制約つき最小二乗（PAV）で解く
    // Solves vertical positions pulled toward neighbor centers via spacing-constrained least squares (PAV), keeping order
    public static class LayerCoordinateAssigner
    {
        public static float[] Assign(List<List<int>> layers, List<(int upper, int lower)> segments, List<float> heights, float gap, int iterations)
        {
            var neighbors = Enumerable.Range(0, heights.Count).Select(_ => new List<int>()).ToArray();
            foreach (var (upper, lower) in segments)
            {
                neighbors[upper].Add(lower);
                neighbors[lower].Add(upper);
            }

            // 初期値は単純な積み上げ
            // Start from a plain stack
            var y = new float[heights.Count];
            foreach (var layer in layers) Stack(layer);

            for (var i = 0; i < iterations; i++)
            {
                foreach (var layer in layers) Relax(layer);
            }

            // 最上端を0へそろえる
            // Shift so the topmost edge sits at zero
            var minY = y.Min();
            for (var v = 0; v < y.Length; v++) y[v] -= minY;
            return y;

            #region Internal

            void Stack(List<int> layer)
            {
                var cursor = 0f;
                foreach (var v in layer)
                {
                    y[v] = cursor;
                    cursor += heights[v] + gap;
                }
            }

            void Relax(List<int> layer)
            {
                // 目標中心を「累積間隔」で差し引くと、制約は単調非減少になりPAVで解ける
                // Subtracting cumulative spacing turns the constraint into monotonicity solvable by PAV
                var offsets = new float[layer.Count];
                var targets = new float[layer.Count];
                for (var i = 0; i < layer.Count; i++)
                {
                    var v = layer[i];
                    offsets[i] = i == 0 ? 0 : offsets[i - 1] + heights[layer[i - 1]] + gap;
                    var desiredTop = neighbors[v].Count == 0 ? y[v] : neighbors[v].Average(n => y[n] + heights[n] / 2f) - heights[v] / 2f;
                    targets[i] = desiredTop - offsets[i];
                }
                var solved = IsotonicRegression(targets);
                for (var i = 0; i < layer.Count; i++) y[layer[i]] = solved[i] + offsets[i];
            }

            float[] IsotonicRegression(float[] values)
            {
                // 違反する隣接ブロックを平均で併合していく（Pool Adjacent Violators）
                // Merge adjacent violating blocks into their mean (Pool Adjacent Violators)
                var means = new List<float>();
                var sizes = new List<int>();
                foreach (var value in values)
                {
                    means.Add(value);
                    sizes.Add(1);
                    while (means.Count > 1 && means[^2] > means[^1])
                    {
                        var size = sizes[^2] + sizes[^1];
                        var mean = (means[^2] * sizes[^2] + means[^1] * sizes[^1]) / size;
                        means.RemoveAt(means.Count - 1);
                        sizes.RemoveAt(sizes.Count - 1);
                        means[^1] = mean;
                        sizes[^1] = size;
                    }
                }
                var result = new float[values.Length];
                var index = 0;
                for (var b = 0; b < means.Count; b++)
                {
                    for (var k = 0; k < sizes[b]; k++) result[index++] = means[b];
                }
                return result;
            }

            #endregion
        }
    }
}
