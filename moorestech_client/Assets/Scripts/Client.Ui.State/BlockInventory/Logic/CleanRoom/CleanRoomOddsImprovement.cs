using System.Collections.Generic;

namespace Client.Ui.State.BlockInventory.Logic.CleanRoom
{
    /// <summary>
    /// 「失敗率の改善方法」の1行。何をするかと、今との差分（サーバーが抽選と同じ式で計算した値）
    /// One improvement row: what to do and the deltas from now (computed by the server with the draw's formula)
    /// </summary>
    public abstract class CleanRoomOddsImprovement
    {
        // Lv1 から順の差分（露光は空）と失敗の差分（負なら失敗が減る）
        // Deltas from Lv1 (empty for exposure) and the failure delta (negative means fewer failures)
        public IReadOnlyList<double> LevelRateDeltas { get; }
        public double FailedRateDelta { get; }

        private protected CleanRoomOddsImprovement(IReadOnlyList<double> levelRateDeltas, double failedRateDelta)
        {
            LevelRateDeltas = levelRateDeltas;
            FailedRateDelta = failedRateDelta;
        }

        public static CleanRoomOddsImprovement RoomClassUp(string targetClassName, IReadOnlyList<double> levelRateDeltas, double failedRateDelta)
        {
            return new RoomClassUpOddsImprovement(targetClassName, levelRateDeltas, failedRateDelta);
        }

        public static CleanRoomOddsImprovement AddQualityModule(int moduleTier, IReadOnlyList<double> levelRateDeltas, double failedRateDelta)
        {
            return new AddQualityModuleOddsImprovement(moduleTier, levelRateDeltas, failedRateDelta);
        }
    }

    // 部屋クラスを1つ上げる（「部屋クラスを B に」）
    // Raise the room class by one ("Raise room class to B")
    public sealed class RoomClassUpOddsImprovement : CleanRoomOddsImprovement
    {
        public string TargetClassName { get; }

        internal RoomClassUpOddsImprovement(string targetClassName, IReadOnlyList<double> levelRateDeltas, double failedRateDelta) : base(levelRateDeltas, failedRateDelta)
        {
            TargetClassName = targetClassName;
        }
    }

    // その Lv の品質モジュールを1個足す（「品質モジュールLv1 を追加」）
    // Add one quality module of that tier ("Add quality module Lv1")
    public sealed class AddQualityModuleOddsImprovement : CleanRoomOddsImprovement
    {
        public int ModuleTier { get; }

        internal AddQualityModuleOddsImprovement(int moduleTier, IReadOnlyList<double> levelRateDeltas, double failedRateDelta) : base(levelRateDeltas, failedRateDelta)
        {
            ModuleTier = moduleTier;
        }
    }
}
