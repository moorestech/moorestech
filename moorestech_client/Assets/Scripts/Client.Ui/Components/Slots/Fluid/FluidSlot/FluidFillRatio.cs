using Client.Ui.Components.Gauges;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// フィルの割合 0..1 を出す（Web の fillRatio）
    /// Fill ratio 0..1 from amount and capacity (the Web fillRatio and clamp01)
    /// </summary>
    internal static class FluidFillRatio
    {
        public static float Of(double amount, double capacity)
        {
            // 容量 0 以下は 0、NaN は 0、範囲外は 0..1 へ丸める
            // Capacity at or below 0 gives 0, NaN gives 0, and out-of-range values clamp to 0..1
            if (capacity <= 0) return 0f;
            // silent-ok: Web の clamp01 と同じく数でない割合は空のフィルとして描く。容量はサーバーの FluidContainer が有限で 0 以上に保つ（D-18）ので割合が NaN になるのは量が NaN のときだけ / そのとき同じマスの量の字が SlotAmountFormat で "NaN" と出て、プレイテストの画面で気づく
            return GaugeRatio.Clamp01((float)(amount / capacity));
        }
    }
}
