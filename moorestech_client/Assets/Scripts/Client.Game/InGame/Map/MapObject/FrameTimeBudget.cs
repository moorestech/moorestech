using System.Diagnostics;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     フレームあたりの処理時間予算（超過で次フレームへ）
    ///     A per-frame processing time budget (crosses to the next frame when exceeded)
    /// </summary>
    public sealed class FrameTimeBudget
    {
        // 実時間API禁止規約はサーバーゲームロジック対象。クライアントの描画分散であるここは適用外（ADR 0030）
        // The no-realtime-API rule targets server game logic; client-side render spreading here is exempt (ADR 0030)
        private readonly double _budgetMilliseconds;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public FrameTimeBudget(double budgetMilliseconds)
        {
            _budgetMilliseconds = budgetMilliseconds;
        }

        public bool IsExhausted => _budgetMilliseconds <= _stopwatch.Elapsed.TotalMilliseconds;

        public void Restart()
        {
            _stopwatch.Restart();
        }
    }
}
