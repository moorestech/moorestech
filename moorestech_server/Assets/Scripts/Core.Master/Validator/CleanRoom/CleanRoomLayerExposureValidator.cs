using System;
using System.Collections.Generic;
using Mooresmaster.Model.CleanRoomModule;

namespace Core.Master.Validator
{
    // EUV（層ごとの失敗判定）の役割表を検証する
    // Validates the EUV (per-layer failure roll) role table
    internal static class CleanRoomLayerExposureValidator
    {
        private const string RoleName = "LayerExposures";

        public static string Validate(LayerExposuresElement[] exposures, HashSet<Guid> roleRecipeGuids)
        {
            var logs = "";
            foreach (var exposure in exposures)
            {
                logs += CleanRoomRoleRecipeChecks.Validate(RoleName, exposure.MachineRecipeGuid, roleRecipeGuids, out var recipe);
                if (recipe == null) continue;

                // 入力のグレードを受け継ぎ、次層ウェハは同じグレード数のファミリーで持つ（両方引けたときだけ比べる）
                // Inherit the input grade and keep the layered wafer as a family with the same grade count (compared only when both resolve)
                logs += CleanRoomRoleRecipeChecks.ValidateGradeInheritingRole(RoleName, recipe, exposure.LayeredWaferItemGuid, out var inputGradeCount, out var layeredGradeCount);
                if (inputGradeCount is int inputCount && layeredGradeCount is int layeredCount && inputCount != layeredCount)
                {
                    logs += $"[CleanRoomMaster] {RoleName} MachineRecipeGuid:{exposure.MachineRecipeGuid} input grade count {inputCount} differs from layered wafer grade count {layeredCount}\n";
                }

                // 出力はちょうど [次層ウェハ ×c, 失敗パターンウェハ ×c]
                // Outputs are exactly [layered wafer xc, failed pattern wafer xc]
                logs += OutputLayoutValidation(recipe.OutputItems.Length == 2
                    && recipe.OutputItems[0].ItemGuid == exposure.LayeredWaferItemGuid
                    && recipe.OutputItems[1].ItemGuid == exposure.FailedWaferItemGuid
                    && recipe.OutputItems[0].Count == recipe.OutputItems[1].Count, exposure.MachineRecipeGuid);
                if (exposure.MaxFailReduction < 0f || 1f < exposure.MaxFailReduction)
                {
                    logs += $"[CleanRoomMaster] {RoleName} MachineRecipeGuid:{exposure.MachineRecipeGuid} MaxFailReduction:{exposure.MaxFailReduction} is out of [0,1]\n";
                }
            }
            return logs;

            #region Internal

            string OutputLayoutValidation(bool isValid, Guid machineRecipeGuid)
            {
                return isValid ? "" : $"[CleanRoomMaster] {RoleName} MachineRecipeGuid:{machineRecipeGuid} outputItems must be exactly [layered wafer, failed wafer] with equal counts\n";
            }

            #endregion
        }
    }
}
