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

            errorLogs += ThresholdsValidation();
            errorLogs += ChipDrawsValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ThresholdsValidation()
            {
                var logs = "";
                var classNames = new HashSet<string>();
                for (var i = 0; i < element.Thresholds.Length; i++)
                {
                    var threshold = element.Thresholds[i];

                    // 濃度上限は前行より大きい（昇順）こと
                    // Concentration caps must be strictly ascending across rows
                    if (i > 0 && threshold.MaxConcentration <= element.Thresholds[i - 1].MaxConcentration)
                    {
                        logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{threshold.ClassName} MaxConcentration:{threshold.MaxConcentration} is not ascending.\n";
                    }

                    if (threshold.DownBinRate < 0f || 1f < threshold.DownBinRate)
                    {
                        logs += $"[CleanRoomMaster] Thresholds[{i}] ClassName:{threshold.ClassName} has out-of-range DownBinRate:{threshold.DownBinRate}\n";
                    }

                    if (!classNames.Add(threshold.ClassName))
                    {
                        logs += $"[CleanRoomMaster] Thresholds[{i}] has duplicated ClassName:{threshold.ClassName}\n";
                    }
                }

                return logs;
            }

            string ChipDrawsValidation()
            {
                var logs = "";
                var recipeGuids = new HashSet<Guid>();
                foreach (var chipDraw in element.ChipDraws)
                {
                    if (MasterHolder.MachineRecipesMaster.GetRecipeElement(chipDraw.MachineRecipeGuid) == null)
                    {
                        logs += $"[CleanRoomMaster] ChipDraws has invalid MachineRecipeGuid:{chipDraw.MachineRecipeGuid}\n";
                    }

                    if (!recipeGuids.Add(chipDraw.MachineRecipeGuid))
                    {
                        logs += $"[CleanRoomMaster] ChipDraws has duplicated MachineRecipeGuid:{chipDraw.MachineRecipeGuid}\n";
                    }

                    foreach (var distribution in chipDraw.OutputDistributions)
                    {
                        if (MasterHolder.ItemMaster.GetItemIdOrNull(distribution.OutputItemGuid) == null)
                        {
                            logs += $"[CleanRoomMaster] ChipDraws MachineRecipeGuid:{chipDraw.MachineRecipeGuid} has invalid OutputItemGuid:{distribution.OutputItemGuid}\n";
                        }

                        foreach (var level in distribution.Levels)
                        {
                            if (MasterHolder.ItemMaster.GetItemIdOrNull(level.ChipItemGuid) == null)
                            {
                                logs += $"[CleanRoomMaster] ChipDraws MachineRecipeGuid:{chipDraw.MachineRecipeGuid} Level:{level.Level} has invalid ChipItemGuid:{level.ChipItemGuid}\n";
                            }
                        }
                    }
                }

                return logs;
            }

            #endregion
        }

        public static void Initialize(CleanRoomMasterElement element, out Dictionary<string, int> thresholdIndexByClassName, out Dictionary<Guid, ChipDrawsElement> chipDrawByMachineRecipeGuid)
        {
            thresholdIndexByClassName = new Dictionary<string, int>();
            chipDrawByMachineRecipeGuid = new Dictionary<Guid, ChipDrawsElement>();
            if (element == null) return;

            // クラス名→行インデックスとレシピGUID→抽選テーブルの索引を構築
            // Build className-to-row-index and recipeGuid-to-draw-table lookups
            for (var i = 0; i < element.Thresholds.Length; i++) thresholdIndexByClassName.Add(element.Thresholds[i].ClassName, i);
            foreach (var chipDraw in element.ChipDraws) chipDrawByMachineRecipeGuid.Add(chipDraw.MachineRecipeGuid, chipDraw);
        }
    }
}
