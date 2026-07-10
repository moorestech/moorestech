using System;
using System.Collections.Generic;

namespace Game.CleanRoom.Util
{
    /// <summary>
    ///     純度シミュレーションの純関数群（1tick積分と閾値行判定）
    ///     Pure functions for purity simulation (per-tick integration and threshold row decision)
    /// </summary>
    public static class CleanRoomPurityLogic
    {
        // 昇格時のみ課すヒステリシス係数（濃度は厳しく、換気は多めに要求）
        // Hysteresis factors imposed only on promotion (stricter concentration, more air change)
        public const double PromoteConcentrationFactor = 0.8;
        public const double PromoteAirChangeFactor = 1.25;

        public static double IntegrateTick(double impurityCount, int volume, double aTotalPerSecond, double removalVolumePerSecond, double deltaSeconds)
        {
            // N' = max(0, N + (A − q·N/V)·dt)。体積0では除去項を持たない
            // N' = max(0, N + (A − q·N/V)·dt); a zero volume has no removal term
            var removedPerSecond = volume <= 0 ? 0 : removalVolumePerSecond * impurityCount / volume;
            return Math.Max(0, impurityCount + (aTotalPerSecond - removedPerSecond) * deltaSeconds);
        }

        public static int DecideThresholdIndex(int currentIndex, double concentration, double airChangeRate, IReadOnlyList<CleanRoomThresholdRow> rows)
        {
            // 良い行から順に判定し、現在行より上を狙うときだけ昇格マージンを課す
            // Scan rows best-first, imposing the promotion margin only when aiming above the current row
            for (var i = 0; i < rows.Count; i++)
            {
                var isPromotion = i < currentIndex;
                var maxConcentration = isPromotion ? rows[i].MaxConcentration * PromoteConcentrationFactor : rows[i].MaxConcentration;
                var requiredAirChange = isPromotion ? rows[i].RequiredAirChangeRate * PromoteAirChangeFactor : rows[i].RequiredAirChangeRate;
                if (concentration <= maxConcentration && airChangeRate >= requiredAirChange) return i;
            }

            // どの行も満たさなければ Out（rows.Count）を返す
            // Return Out (rows.Count) when no row is satisfied
            return rows.Count;
        }
    }

    /// <summary>
    ///     閾値行のロジック入力。マスタから初期化時に1回だけ変換される
    ///     Threshold row input for the logic, converted once from master at initialization
    /// </summary>
    public readonly struct CleanRoomThresholdRow
    {
        public readonly double MaxConcentration;
        public readonly double RequiredAirChangeRate;

        public CleanRoomThresholdRow(double maxConcentration, double requiredAirChangeRate)
        {
            MaxConcentration = maxConcentration;
            RequiredAirChangeRate = requiredAirChangeRate;
        }
    }
}
