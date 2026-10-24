using System.Collections.Generic;

namespace Game.Block.Blocks.CleanRoom.Machine.Yield
{
    /// <summary>
    ///     ChipYieldFormula の確率で実際に引く。乱数は呼び出し側の決定的な列から取る
    ///     Draws with ChipYieldFormula's probabilities, pulling from the caller's deterministic sequence
    /// </summary>
    public static class ChipYieldDraw
    {
        // G1から始め、同じ確率で1段ずつ上げ、外れか上限で止める
        // Start at G1 and climb one step per success at the same chance, stopping on a miss or at the cap
        public static int DrawWaferGrade(double promotionChance, int maxGrade, CleanRoomDrawRandom random)
        {
            var grade = 1;
            while (grade < maxGrade && random.NextDouble() < promotionChance) grade++;
            return grade;
        }

        public static bool DrawLayerFailure(double layerFailRate, CleanRoomDrawRandom random)
        {
            return random.NextDouble() < layerFailRate;
        }

        // ダイごとに損失を判定し（損失数は二項分布）、残りをダイ表でレベルへ振り分ける
        // Roll loss per die (the loss count is binomial), then assign survivors a level from the die table
        public static DicingDrawResult DrawDicing(int diesPerWafer, double dieLossRate, IReadOnlyList<float> levelRates, CleanRoomDrawRandom random)
        {
            var goodCounts = new int[levelRates.Count];
            var failedCount = 0;
            for (var die = 0; die < diesPerWafer; die++)
            {
                if (random.NextDouble() < dieLossRate)
                {
                    failedCount++;
                    continue;
                }
                goodCounts[PickLevelIndex(levelRates, random.NextDouble())]++;
            }
            return new DicingDrawResult(goodCounts, failedCount);
        }

        // 累積比率で添字を選ぶ。丸め誤差で外れた分は最後のレベルへ寄せる
        // Pick an index by cumulative rate; rounding leftovers fall to the last level
        private static int PickLevelIndex(IReadOnlyList<float> levelRates, double roll)
        {
            var cumulative = 0d;
            for (var i = 0; i < levelRates.Count - 1; i++)
            {
                cumulative += levelRates[i];
                if (roll < cumulative) return i;
            }
            return levelRates.Count - 1;
        }
    }
}
