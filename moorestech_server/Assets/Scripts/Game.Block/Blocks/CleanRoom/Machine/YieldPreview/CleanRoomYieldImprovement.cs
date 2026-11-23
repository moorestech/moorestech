using System.Collections.Generic;

namespace Game.Block.Blocks.CleanRoom.Machine.YieldPreview
{
    /// <summary>
    ///     「失敗率の改善方法」の1行。今の最終の内訳との差分（仮の条件 − 今）を持つ。EUV のLv別差分は空
    ///     One row of the improvement list, holding the difference from the current final odds (hypothetical minus current); EUV has no per-level deltas
    /// </summary>
    public abstract class CleanRoomYieldImprovement
    {
        public IReadOnlyList<double> LevelRateDeltas { get; }
        public double FailedRateDelta { get; }

        // 種別はこのファイルの型に閉じる
        // The kinds are closed to the types in this file
        private protected CleanRoomYieldImprovement(IReadOnlyList<double> levelRateDeltas, double failedRateDelta)
        {
            LevelRateDeltas = levelRateDeltas;
            FailedRateDelta = failedRateDelta;
        }
    }

    // 部屋クラスを1つ上げる
    // Raise the room class by one
    public sealed class CleanRoomRoomClassUpImprovement : CleanRoomYieldImprovement
    {
        public string TargetClassName { get; }

        public CleanRoomRoomClassUpImprovement(string targetClassName, IReadOnlyList<double> levelRateDeltas, double failedRateDelta) : base(levelRateDeltas, failedRateDelta)
        {
            TargetClassName = targetClassName;
        }
    }

    // 品質モジュールを1個足す（刺さっている最高 tier を増やす／1つ上の tier を刺す）
    // Add one quality module (more of the highest equipped tier, or one of the next tier)
    public sealed class CleanRoomAddQualityModuleImprovement : CleanRoomYieldImprovement
    {
        public int ModuleTier { get; }

        public CleanRoomAddQualityModuleImprovement(int moduleTier, IReadOnlyList<double> levelRateDeltas, double failedRateDelta) : base(levelRateDeltas, failedRateDelta)
        {
            ModuleTier = moduleTier;
        }
    }
}
