using System.Collections.Generic;

namespace Core.Master.Validator
{
    /// <summary>
    ///     クラスを上げても良品とLv2以上が減らないことを支える、マスタ値の純粋な規則
    ///     Pure master-value rules that keep a better class from lowering the good total or the Lv2+ count
    /// </summary>
    public static class CleanRoomYieldTableRules
    {
        // 行は良いクラスから順。倍率は0以上で非増加、失敗率と損失率は[0,1]で非減少
        // Rows run best class first: multipliers are >= 0 and non-increasing, fail and loss rates are in [0,1] and non-decreasing
        public static string ValidateClassRows(IReadOnlyList<(string className, float qualityMultiplier, float layerFailRate, float dieLossRate)> rows)
        {
            var logs = "";
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (float.IsNaN(row.qualityMultiplier) || float.IsInfinity(row.qualityMultiplier) || row.qualityMultiplier < 0f) logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{row.className} QualityMultiplier:{row.qualityMultiplier} must be finite and >= 0\n";
                if (float.IsNaN(row.layerFailRate) || float.IsInfinity(row.layerFailRate) || row.layerFailRate < 0f || 1f < row.layerFailRate) logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{row.className} LayerFailRate:{row.layerFailRate} must be finite and in [0,1]\n";
                if (float.IsNaN(row.dieLossRate) || float.IsInfinity(row.dieLossRate) || row.dieLossRate < 0f || 1f < row.dieLossRate) logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{row.className} DieLossRate:{row.dieLossRate} must be finite and in [0,1]\n";
                if (i == 0) continue;

                var better = rows[i - 1];
                if (better.qualityMultiplier < row.qualityMultiplier) logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{row.className} QualityMultiplier:{row.qualityMultiplier} exceeds the better class {better.className}:{better.qualityMultiplier}\n";
                if (row.layerFailRate < better.layerFailRate) logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{row.className} LayerFailRate:{row.layerFailRate} is below the better class {better.className}:{better.layerFailRate}\n";
                if (row.dieLossRate < better.dieLossRate) logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{row.className} DieLossRate:{row.dieLossRate} is below the better class {better.className}:{better.dieLossRate}\n";
            }
            return logs;
        }

        // 浮動小数の和の許容誤差
        // Tolerance for floating-point sums
        private const float RateTolerance = 1e-4f;

        // 各表は長さ1〜min(グレード, ダイのレベル数)・非負・和1。上位の裾（Lvk以上の割合）はグレードとともに減らない
        // Each table has length 1..min(grade, die level count), is non-negative and sums to 1; the upper tail (share at Lvk+) never shrinks as the grade rises
        public static string ValidateDieTables(string roleLabel, IReadOnlyList<IReadOnlyList<float>> gradeDieTables, int dieLevelCount)
        {
            var logs = "";
            for (var gradeIndex = 0; gradeIndex < gradeDieTables.Count; gradeIndex++)
            {
                var rates = gradeDieTables[gradeIndex];
                var maxLength = gradeIndex + 1 < dieLevelCount ? gradeIndex + 1 : dieLevelCount;
                if (rates.Count < 1 || maxLength < rates.Count) logs += $"[CleanRoomMaster] {roleLabel} GradeDieTables[{gradeIndex}] has {rates.Count} levels; allowed 1..{maxLength}\n";

                var sum = 0f;
                foreach (var rate in rates)
                {
                    if (float.IsNaN(rate) || float.IsInfinity(rate))
                    {
                        logs += $"[CleanRoomMaster] {roleLabel} GradeDieTables[{gradeIndex}] LevelRates contains non-finite value {rate}\n";
                        continue;
                    }
                    if (rate < 0f) logs += $"[CleanRoomMaster] {roleLabel} GradeDieTables[{gradeIndex}] has a negative rate {rate}\n";
                    sum += rate;
                }
                if (RateTolerance < System.Math.Abs(sum - 1f)) logs += $"[CleanRoomMaster] {roleLabel} GradeDieTables[{gradeIndex}] rates sum to {sum}, not 1\n";
                if (gradeIndex == 0) continue;

                // Lvk以上の割合を1つ下のグレードと比べる
                // Compare the share at Lvk and above with the grade just below
                for (var level = 1; level < dieLevelCount; level++)
                {
                    var tail = TailFrom(rates, level);
                    var lowerTail = TailFrom(gradeDieTables[gradeIndex - 1], level);
                    if (tail + RateTolerance < lowerTail) logs += $"[CleanRoomMaster] {roleLabel} GradeDieTables[{gradeIndex}] tail from Lv{level + 1} ({tail}) is below the lower grade ({lowerTail})\n";
                }
            }
            return logs;

            #region Internal

            static float TailFrom(IReadOnlyList<float> rates, int fromLevelIndex)
            {
                var tail = 0f;
                for (var i = fromLevelIndex; i < rates.Count; i++) tail += rates[i];
                return tail;
            }

            #endregion
        }


    }
}
