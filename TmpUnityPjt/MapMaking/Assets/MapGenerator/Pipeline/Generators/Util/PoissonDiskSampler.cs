using System.Collections.Generic;
using UnityEngine;

namespace MapGenerator.Pipeline.Generators.Util
{
    /// <summary>
    /// Bridson's Algorithm による Poisson Disk サンプリング。
    /// 木・オブジェクトの均等かつ自然な配置に使用する。
    /// TreePlacementGenerator から呼ばれ、モンテカルロ法を置き換える。
    /// </summary>
    public static class PoissonDiskSampler
    {
        public static List<Vector2> Generate(float width, float height,
            float minDistance, int seed, int maxAttempts = 30)
        {
            // セルサイズ = minDist/√2 で、各セルに最大1点しか入らないことを保証
            float cellSize = minDistance / Mathf.Sqrt(2f);
            int gridW = Mathf.CeilToInt(width / cellSize);
            int gridH = Mathf.CeilToInt(height / cellSize);
            var grid = new int[gridW * gridH];
            for (int i = 0; i < grid.Length; i++) grid[i] = -1;

            var points = new List<Vector2>();
            var active = new List<int>();
            var rng = new System.Random(seed);

            // 初期点をランダムに配置してアルゴリズムを開始
            var first = new Vector2((float)rng.NextDouble() * width, (float)rng.NextDouble() * height);
            AddPoint(first, points, active, grid, gridW, cellSize);

            // アクティブリストが空になるまで、各点の周囲に新しい点を試みる
            while (active.Count > 0)
            {
                int idx = rng.Next(active.Count);
                var center = points[active[idx]];
                bool found = false;

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    // minDistance〜2*minDistanceの環状領域にランダム候補を生成
                    float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                    float dist = minDistance + (float)rng.NextDouble() * minDistance;
                    var candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                    if (candidate.x < 0 || candidate.x >= width ||
                        candidate.y < 0 || candidate.y >= height) continue;

                    if (!HasNeighborWithinDistance(candidate, points, grid, gridW, gridH, cellSize, minDistance))
                    {
                        AddPoint(candidate, points, active, grid, gridW, cellSize);
                        found = true;
                        break;
                    }
                }

                // maxAttempts回失敗した点はこれ以上拡張できないので除外
                if (!found) active.RemoveAt(idx);
            }

            return points;
        }

        static void AddPoint(Vector2 point, List<Vector2> points, List<int> active,
            int[] grid, int gridW, float cellSize)
        {
            int idx = points.Count;
            points.Add(point);
            active.Add(idx);
            // グリッドに登録して近傍探索を O(1) にする
            int gx = Mathf.FloorToInt(point.x / cellSize);
            int gy = Mathf.FloorToInt(point.y / cellSize);
            grid[gy * gridW + gx] = idx;
        }

        /// <summary>
        /// 候補点の周囲2セル以内に minDist 未満の既存点があるか判定。
        /// グリッド分割により全点との比較を避け O(1) で近傍チェックする。
        /// </summary>
        static bool HasNeighborWithinDistance(Vector2 candidate, List<Vector2> points,
            int[] grid, int gridW, int gridH, float cellSize, float minDist)
        {
            int gx = Mathf.FloorToInt(candidate.x / cellSize);
            int gy = Mathf.FloorToInt(candidate.y / cellSize);
            float minDistSq = minDist * minDist;

            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int nx = gx + dx;
                    int ny = gy + dy;
                    if (nx < 0 || nx >= gridW || ny < 0 || ny >= gridH) continue;
                    int pidx = grid[ny * gridW + nx];
                    if (pidx < 0) continue;
                    if ((points[pidx] - candidate).sqrMagnitude < minDistSq) return true;
                }
            }

            return false;
        }
    }
}
