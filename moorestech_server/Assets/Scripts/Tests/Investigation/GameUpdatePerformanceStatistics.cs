using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Investigation
{
    public class GameUpdatePerformanceStatistics
    {
        public int Count { get; }
        public double TotalMs { get; }
        public double AverageMs { get; }
        public double MinMs { get; }
        public double P50Ms { get; }
        public double P90Ms { get; }
        public double P95Ms { get; }
        public double P99Ms { get; }
        public double MaxMs { get; }
        public double StdDevMs { get; }

        public GameUpdatePerformanceStatistics(IReadOnlyList<double> samplesMs)
        {
            Count = samplesMs.Count;
            var ordered = samplesMs.OrderBy(x => x).ToArray();

            // 分布比較で使う代表値を同じ計算方法に固定する
            // Keep summary calculations stable for later comparison.
            TotalMs = samplesMs.Sum();
            AverageMs = TotalMs / Count;
            MinMs = ordered[0];
            P50Ms = Percentile(ordered, 0.50);
            P90Ms = Percentile(ordered, 0.90);
            P95Ms = Percentile(ordered, 0.95);
            P99Ms = Percentile(ordered, 0.99);
            MaxMs = ordered[^1];

            // 母集団標準偏差として tick 列全体のばらつきを見る
            // Use population standard deviation for the full tick series.
            var variance = samplesMs.Sum(x => Math.Pow(x - AverageMs, 2)) / Count;
            StdDevMs = Math.Sqrt(variance);
        }

        public string ToLogFields()
        {
            return $"count={Count} totalMs={TotalMs:F3} avgMs={AverageMs:F3} minMs={MinMs:F3} p50Ms={P50Ms:F3} p90Ms={P90Ms:F3} p95Ms={P95Ms:F3} p99Ms={P99Ms:F3} maxMs={MaxMs:F3} stdDevMs={StdDevMs:F3}";
        }

        private static double Percentile(double[] ordered, double percentile)
        {
            if (ordered.Length == 1) return ordered[0];
            var index = (int)Math.Ceiling((ordered.Length - 1) * percentile);
            return ordered[index];
        }
    }
}
