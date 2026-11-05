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

        // 各Tierの表は失敗込みで和1、品質ゲートと既定レベルを満たす
        // Each tier sums to one including failures and respects the quality gate and default level
        public static string ValidateDieTables(string roleLabel, IReadOnlyList<(IReadOnlyList<float> levelRates, float failedRate)> tierTables, int dieLevelCount)
        {
            var logs = "";
            if (tierTables.Count != dieLevelCount + 1) logs += $"[CleanRoomMaster] {roleLabel} requires {dieLevelCount + 1} quality tier tables (found {tierTables.Count})\n";
            for (var tier = 0; tier < tierTables.Count; tier++)
            {
                var (rates, failedRate) = tierTables[tier];
                var prefix = $"[CleanRoomMaster] {roleLabel} QualityTierDieTables[{tier}]";
                if (rates.Count != dieLevelCount) logs += $"{prefix} requires {dieLevelCount} levels (found {rates.Count})\n";

                // 非有限値も独立に記録し、合計の検査から漏らさない
                // Report non-finite values independently so the sum check cannot hide them
                var sum = failedRate;
                logs += ValidateRate(prefix, "FailedRate", failedRate);
                for (var index = 0; index < rates.Count; index++)
                {
                    var rate = rates[index];
                    logs += ValidateRate(prefix, $"LevelRates[{index}]", rate);
                    sum += rate;
                    if (tier + 1 <= index && rate != 0f) logs += $"{prefix} Lv{index + 1} violates the quality gate\n";
                }
                if (float.IsNaN(sum) || float.IsInfinity(sum) || RateTolerance < System.Math.Abs(sum - 1f)) logs += $"{prefix} rates sum to {sum}, not 1\n";
                if (0 < tier && tier <= dieLevelCount && tier <= rates.Count && !(0f < rates[tier - 1])) logs += $"{prefix} default Lv{tier} must be positive\n";
            }
            return logs;

            #region Internal

            static string ValidateRate(string prefix, string name, float value)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) return $"{prefix} {name} contains non-finite value {value}\n";
                return value < 0f ? $"{prefix} {name} has a negative rate {value}\n" : "";
            }

            #endregion
        }
    }
}
