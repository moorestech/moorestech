using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MapGenerator.Pipeline
{
    /// <summary>
    /// パイプライン各ステージの所要時間を計測する軽量ユーティリティ。
    /// Enabled=true のときのみ記録し、無効時はほぼゼロコスト。
    /// </summary>
    public static class PipelineProfiler
    {
        public static bool Enabled;

        static readonly Stopwatch Sw = new();
        static readonly List<(string name, long ms)> Entries = new();

        /// <summary>計測開始。過去の記録をクリアしてタイマーを開始する。</summary>
        public static void Begin()
        {
            if (!Enabled) return;
            Entries.Clear();
            Sw.Restart();
        }

        /// <summary>現在のラップタイムを記録し、タイマーをリセットする。</summary>
        public static void Lap(string name)
        {
            if (!Enabled) return;
            Entries.Add((name, Sw.ElapsedMilliseconds));
            Sw.Restart();
        }

        /// <summary>計測結果を整形文字列で返す。</summary>
        public static string Report()
        {
            var sb = new StringBuilder();
            long total = 0;
            foreach (var (name, ms) in Entries)
            {
                total += ms;
                float pct = total > 0 ? (float)ms / total * 100f : 0f; // 暫定割合（後で再計算）
                sb.AppendLine($"  {name}: {ms}ms");
            }
            sb.AppendLine($"  ──────────────────");
            sb.AppendLine($"  合計: {total}ms");

            // 割合付きサマリ
            if (Entries.Count > 0 && total > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  [割合]");
                foreach (var (name, ms) in Entries)
                {
                    float pct = (float)ms / total * 100f;
                    sb.AppendLine($"  {pct,5:F1}% | {ms,6}ms | {name}");
                }
            }

            return sb.ToString();
        }

        /// <summary>記録済みエントリのリストを返す（外部からの集計用）。</summary>
        public static IReadOnlyList<(string name, long ms)> GetEntries() => Entries;
    }
}
