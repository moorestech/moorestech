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
                if (row.qualityMultiplier < 0f) logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{row.className} QualityMultiplier:{row.qualityMultiplier} must be >= 0\n";
                if (row.layerFailRate < 0f || 1f < row.layerFailRate) logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{row.className} LayerFailRate:{row.layerFailRate} is out of [0,1]\n";
                if (row.dieLossRate < 0f || 1f < row.dieLossRate) logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{row.className} DieLossRate:{row.dieLossRate} is out of [0,1]\n";
                if (i == 0) continue;

                var better = rows[i - 1];
                if (better.qualityMultiplier < row.qualityMultiplier) logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{row.className} QualityMultiplier:{row.qualityMultiplier} exceeds the better class {better.className}:{better.qualityMultiplier}\n";
                if (row.layerFailRate < better.layerFailRate) logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{row.className} LayerFailRate:{row.layerFailRate} is below the better class {better.className}:{better.layerFailRate}\n";
                if (row.dieLossRate < better.dieLossRate) logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{row.className} DieLossRate:{row.dieLossRate} is below the better class {better.className}:{better.dieLossRate}\n";
            }
            return logs;
        }
    }
}
