using System.Collections.Generic;

namespace Client.Ui.State.BlockInventory.Logic.CleanRoom
{
    /// <summary>
    /// 役割ごとの最終の確率と「失敗率の改善方法」の行。値はサーバーが計算したものを運ぶだけ（ADR 0105 §2・§4）
    /// Per-role final odds plus the improvement rows; carries server-computed values only (ADR 0105 §2, §4)
    /// </summary>
    public abstract class CleanRoomYieldOdds
    {
        // サーバーの並び（部屋クラス → 最高 Lv を増やす → 1つ上を刺す）のまま。空ならボタンを出さない
        // Server order (room class, more of the top tier, the next tier); empty means no button
        public IReadOnlyList<CleanRoomOddsImprovement> Improvements { get; }

        public bool HasImprovements => Improvements.Count > 0;

        private protected CleanRoomYieldOdds(IReadOnlyList<CleanRoomOddsImprovement> improvements)
        {
            Improvements = improvements;
        }

        public static CleanRoomYieldOdds LayerExposure(double failedRate, IReadOnlyList<CleanRoomOddsImprovement> improvements)
        {
            return new LayerExposureYieldOdds(failedRate, improvements);
        }

        public static CleanRoomYieldOdds Dicing(int outputCount, IReadOnlyList<double> levelRates, double failedRate, IReadOnlyList<CleanRoomOddsImprovement> improvements)
        {
            return new DicingYieldOdds(outputCount, levelRates, failedRate, improvements);
        }
    }

    // 露光（EUV）は失敗の割合だけ
    // Exposure (EUV) carries only the failure share
    public sealed class LayerExposureYieldOdds : CleanRoomYieldOdds
    {
        public double FailedRate { get; }

        internal LayerExposureYieldOdds(double failedRate, IReadOnlyList<CleanRoomOddsImprovement> improvements) : base(improvements)
        {
            FailedRate = failedRate;
        }
    }

    // ダイシングはウェハ1枚のダイ数・Lv1 から順の最終割合（0 の Lv も入る）・失敗の割合
    // Dicing carries dies per wafer, final shares from Lv1 (zero levels included) and the failure share
    public sealed class DicingYieldOdds : CleanRoomYieldOdds
    {
        public int OutputCount { get; }
        public IReadOnlyList<double> LevelRates { get; }
        public double FailedRate { get; }

        internal DicingYieldOdds(int outputCount, IReadOnlyList<double> levelRates, double failedRate, IReadOnlyList<CleanRoomOddsImprovement> improvements) : base(improvements)
        {
            OutputCount = outputCount;
            LevelRates = levelRates;
            FailedRate = failedRate;
        }
    }
}
