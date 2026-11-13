using System.Collections.Generic;

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
    }
}
