using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;

namespace Game.CleanRoom.Util
{
    /// <summary>
    ///     部屋クラス判定の純関数群（換気回数と閾値行判定）
    ///     Pure functions for the room class decision (air change rate and threshold row decision)
    /// </summary>
    public static class CleanRoomPurityLogic
    {
        // 昇格時のヒステリシス係数
        // Hysteresis factor imposed only on promotion (more air change required)
        public const double PromoteAirChangeFactor = 1.25;

        // 換気回数 nq/V。体積0の部屋は0とする。クラス判定と表示の両方がここを呼ぶ
        // Air change rate nq/V; zero for a zero-volume room, shared by the class decision and the display
        public static double AirChangeRate(double removalVolumePerSecond, int volume)
        {
            return volume <= 0 ? 0 : removalVolumePerSecond / volume;
        }

        public static int DecideThresholdIndex(int currentIndex, double airChangeRate, IReadOnlyList<double> requiredAirChangeRates)
        {
            // 良い行から順に判定し、現在行より上を狙うときだけ昇格マージンを課す
            // Scan rows best-first, imposing the promotion margin only when aiming above the current row
            for (var i = 0; i < requiredAirChangeRates.Count; i++)
            {
                var isPromotion = i < currentIndex;
                var requiredAirChange = isPromotion ? requiredAirChangeRates[i] * PromoteAirChangeFactor : requiredAirChangeRates[i];
                if (requiredAirChange <= airChangeRate) return i;
            }

            // どの行も満たさなければ Out（行数）を返す
            // Return Out (the row count) when no row is satisfied
            return requiredAirChangeRates.Count;
        }

        // 今の除去量に新しく置く台を1台ずつ足し、一つ上の行へ昇格する最小台数を数える
        // Add new units one at a time and count the fewest that promote to the next better row
        public static bool TryCountUnitsToPromote(int currentIndex, double removalVolumePerSecond, int volume, double unitRemovalVolumePerSecond, int unitCellCount, IReadOnlyList<double> requiredAirChangeRates, out int units)
        {
            // セルを占めない台は数え終わらないため、形の契約違反として止める
            // A unit taking no cells would never end the count, so reject the broken shape contract
            if (unitCellCount <= 0) throw new ArgumentOutOfRangeException(nameof(unitCellCount), unitCellCount, "清浄機は1セル以上を占める / A purifier occupies at least one cell");

            units = 0;
            if (!CleanRoomClassYield.TryGetPromotionTargetIndex(currentIndex, out var targetIndex)) return false;

            // クラス判定と同じ関数へ渡し、昇格倍率を含む同じしきい値で判定する
            // Use the class decision function so the promotion margin and threshold stay identical
            for (var count = 1; count * unitCellCount < volume; count++)
            {
                var remainingVolume = volume - count * unitCellCount;
                var airChangeRate = AirChangeRate(removalVolumePerSecond + count * unitRemovalVolumePerSecond, remainingVolume);
                if (targetIndex < DecideThresholdIndex(currentIndex, airChangeRate, requiredAirChangeRates)) continue;
                units = count;
                return true;
            }

            return false;
        }
    }
}
