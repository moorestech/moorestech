namespace Client.Ui.State.Recipe.Logic
{
    /// <summary>
    /// 長押しクラフトの 1 フレームの結果
    /// Result of one hold-to-craft frame
    /// </summary>
    public readonly struct HoldCraftStep
    {
        public readonly double Elapsed;
        public readonly bool DidCraft;

        public HoldCraftStep(double elapsed, bool didCraft)
        {
            Elapsed = elapsed;
            DidCraft = didCraft;
        }
    }

    /// <summary>
    /// 長押しクラフトの経過時間モデル（uGUI CraftButton.Update の累積を純関数化した Web holdCraftLogic の写し）
    /// Elapsed-time model for hold-to-craft (port of Web holdCraftLogic, itself from uGUI CraftButton.Update)
    /// </summary>
    public static class HoldCraftLogic
    {
        // 復帰直後の巨大な経過で連続発火しないよう、呼び出し側が 1 フレームの進みをこの秒数で丸める
        // Callers cap one frame's progress to this many seconds so a huge resume delta cannot burst-fire
        public const double MaxFrameDeltaSeconds = 0.25;

        // craftTime に届いたら 1 回発火して経過を 0 に戻す（連続クラフト）。1 フレーム最大 1 回
        // On reaching craftTime fire once and reset to 0 (continuous craft); at most once per frame
        public static HoldCraftStep Advance(double elapsed, double deltaSeconds, double craftTime)
        {
            var next = elapsed + deltaSeconds;
            if (craftTime <= next) return new HoldCraftStep(0, true);
            return new HoldCraftStep(next, false);
        }

        // 進捗率 0..1。craftTime<=0 は常に満杯（uGUI SetProgressAllow 相当）、NaN は 0
        // Progress 0..1; craftTime<=0 is always full (uGUI SetProgressAllow), NaN is 0
        public static double Progress(double elapsed, double craftTime)
        {
            if (craftTime <= 0) return 1;
            var ratio = elapsed / craftTime;
            if (double.IsNaN(ratio) || ratio < 0) return 0;
            return 1 < ratio ? 1 : ratio;
        }
    }
}
