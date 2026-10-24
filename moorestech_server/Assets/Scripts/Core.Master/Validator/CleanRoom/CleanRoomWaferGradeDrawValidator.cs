using System;
using System.Collections.Generic;
using Mooresmaster.Model.CleanRoomModule;
using Mooresmaster.Model.MachineRecipesModule;

namespace Core.Master.Validator
{
    // 成膜（ウェハグレード抽選）の役割表を検証する
    // Validates the deposition (wafer grade draw) role table
    internal static class CleanRoomWaferGradeDrawValidator
    {
        private const string RoleName = "WaferGradeDraws";

        public static string Validate(WaferGradeDrawsElement[] draws, HashSet<Guid> roleRecipeGuids)
        {
            var logs = "";
            foreach (var draw in draws)
            {
                logs += CleanRoomRoleRecipeChecks.Validate(RoleName, draw.MachineRecipeGuid, roleRecipeGuids, out var recipe);
                if (recipe == null) continue;

                // 成膜は継承しないレシピに限る。継承レシピだと工場が当てた変種をグレード抽選が上書きし、継承が黙って消える
                // Deposition must be a non-inheritance recipe; otherwise the grade draw overwrites the variant the factory applied and the inheritance silently vanishes
                if (recipe.LevelInheritance != MachineRecipeMasterElement.LevelInheritanceConst.None)
                {
                    logs += $"[CleanRoomMaster] {RoleName} MachineRecipeGuid:{draw.MachineRecipeGuid} must use levelInheritance None (found {recipe.LevelInheritance})\n";
                }

                // グレード付きウェハは出力に1回だけあり、グレードをレベルファミリーで持つ
                // The graded wafer appears once among the outputs and carries its grades as a level family
                logs += CleanRoomRoleRecipeChecks.RequireSingleOutput(RoleName, recipe, draw.GradedWaferItemGuid);
                logs += CleanRoomRoleRecipeChecks.RequireFamilyBase(RoleName, draw.MachineRecipeGuid, draw.GradedWaferItemGuid, out _);
                if (float.IsNaN(draw.MaxPromotionChance) || float.IsInfinity(draw.MaxPromotionChance) || draw.MaxPromotionChance < 0f || 1f < draw.MaxPromotionChance)
                {
                    logs += $"[CleanRoomMaster] {RoleName} MachineRecipeGuid:{draw.MachineRecipeGuid} MaxPromotionChance:{draw.MaxPromotionChance} must be finite and in [0,1]\n";
                }
            }
            return logs;
        }
    }
}
