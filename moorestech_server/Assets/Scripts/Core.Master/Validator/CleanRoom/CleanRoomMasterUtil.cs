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
            errorLogs += CleanRoomWaferGradeDrawValidator.Validate(element.WaferGradeDraws, roleRecipeGuids);
            errorLogs += CleanRoomLayerExposureValidator.Validate(element.LayerExposures, roleRecipeGuids);
            errorLogs += CleanRoomDicingValidator.Validate(element.Dicings, roleRecipeGuids);

            // 汚染接続点の集合の参照と重複を見る
            // Check references and duplicates in the pollution connector set
            errorLogs += CleanRoomBoundaryBlockSetValidator.Validate("PollutionConnectorBlockGuids", element.PollutionConnectorBlockGuids);
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

                    // 濃度上限は前行より大きい（昇順）こと
                    // Concentration caps must be strictly ascending across rows
                    if (i > 0 && threshold.MaxConcentration <= element.Thresholds[i - 1].MaxConcentration)
                    {
                        logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{threshold.ClassName} MaxConcentration:{threshold.MaxConcentration} is not ascending.\n";
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
