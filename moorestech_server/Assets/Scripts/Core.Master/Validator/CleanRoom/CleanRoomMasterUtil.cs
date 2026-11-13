using System;
using System.Collections.Generic;
using Mooresmaster.Model.CleanRoomModule;

namespace Core.Master.Validator
{
    public static class CleanRoomMasterUtil
    {
        public static bool Validate(CleanRoomMasterElement element, out string errorLogs)
        {
            errorLogs = "";
            if (element == null) return true;

            // 閾値行と、チップ工程の役割表（1レシピ1役割）を検証する
            // Validate the threshold rows and the chip-process role tables (one role per recipe)
            errorLogs += ThresholdsValidation();
            var roleRecipeGuids = new HashSet<Guid>();
            errorLogs += CleanRoomLayerExposureValidator.Validate(element.LayerExposures, roleRecipeGuids);
            errorLogs += CleanRoomDicingValidator.Validate(element.Dicings, roleRecipeGuids);
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ThresholdsValidation()
            {
                var logs = "";
                var classNames = new HashSet<string>();
                var yieldRows = new List<(string, float, float, float)>();
                for (var i = 0; i < element.Thresholds.Length; i++)
                {
                    var threshold = element.Thresholds[i];

                    // クラス判定は良い行から走査するため、必要換気回数は前行より小さい（降順）こと
                    // The class decision scans best-first, so required air change rates must be strictly descending across rows
                    if (0 < i && element.Thresholds[i - 1].RequiredAirChangeRate <= threshold.RequiredAirChangeRate)
                    {
                        logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{threshold.ClassName} RequiredAirChangeRate:{threshold.RequiredAirChangeRate} is not descending.\n";
                    }

                    // 必要換気回数が0・負・非有限だとクラス条件が空洞化するため正の有限値を要求する
                    // A zero, negative or non-finite required rate hollows out the class condition, so require a positive finite value
                    if (!(0 < threshold.RequiredAirChangeRate) || double.IsInfinity(threshold.RequiredAirChangeRate))
                    {
                        logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{threshold.ClassName} RequiredAirChangeRate:{threshold.RequiredAirChangeRate} is not positive and finite.\n";
                    }

                    if (!classNames.Add(threshold.ClassName))
                    {
                        logs += $"[CleanRoomMaster] Thresholds[{i}] has duplicated ClassName:{threshold.ClassName}\n";
                    }

                    yieldRows.Add((threshold.ClassName, threshold.QualityMultiplier, threshold.LayerFailRate, threshold.DieLossRate));
                }

                // クラスを上げて歩留まりが下がらないよう、値の範囲と行間の単調性を見る
                // Check value ranges and row-to-row monotonicity so a better class never yields less
                logs += CleanRoomYieldTableRules.ValidateClassRows(yieldRows);
                return logs;
            }

            #endregion
        }
    }
}
