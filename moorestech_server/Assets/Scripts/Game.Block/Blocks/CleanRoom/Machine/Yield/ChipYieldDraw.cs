using System.Collections.Generic;

namespace Game.Block.Blocks.CleanRoom.Machine.Yield
{
    /// <summary>
    ///     ChipYieldFormula の確率で実際に引く。乱数は呼び出し側の決定的な列から取る
    ///     Draws with ChipYieldFormula's probabilities, pulling from the caller's deterministic sequence
    /// </summary>
    public static class ChipYieldDraw
    {
        public static bool DrawLayerFailure(double layerFailRate, CleanRoomDrawRandom random)
        {
            return random.NextDouble() < layerFailRate;
        }

        // ダイごとに損失を判定し、残りを [Lv1..LvMax, 失敗ダイ] の割合で振り分ける。損失と失敗枠はどちらも失敗ダイ
        // Roll loss per die, then assign survivors by [Lv1..LvMax, failed die]; losses and failure-slot picks both become failed dies
        public static DicingDrawResult DrawDicing(int diesPerWafer, double dieLossRate, IReadOnlyList<double> survivorRates, CleanRoomDrawRandom random)
        {
            var levelCount = survivorRates.Count - 1;
            var goodCounts = new int[levelCount];
            var failedCount = 0;
            for (var die = 0; die < diesPerWafer; die++)
            {
                if (random.NextDouble() < dieLossRate) { failedCount++; continue; }
                var picked = PickIndex(random.NextDouble());
                if (picked == levelCount) failedCount++;
                else goodCounts[picked]++;
            }
            return new DicingDrawResult(goodCounts, failedCount);

            #region Internal

            // 累積比率で添字を選ぶ。丸め誤差で外れた分は末尾（失敗ダイ）へ寄せる
            // Pick an index by cumulative rate; rounding leftovers fall to the last slot (failed die)
            int PickIndex(double roll)
            {
                var cumulative = 0d;
                for (var i = 0; i < survivorRates.Count - 1; i++)
                {
                    cumulative += survivorRates[i];
                    if (roll < cumulative) return i;
                }
                return survivorRates.Count - 1;
            }

            #endregion
        }
    }
}
