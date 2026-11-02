using System;
using System.Collections.Generic;
using Mooresmaster.Model.MachineRecipesModule;

namespace Core.Master.Validator.MachineUpgrade
{
    /// <summary>
    ///     継承レシピの形（継承元1つ・継承先1つ・ファミリー長・上限モードの一貫性）を検証する。機械の品質可否は見ない
    ///     Validates an inheritance recipe's shape: one source, one target, family lengths and consistent cap modes; the machine's quality permission is not checked
    /// </summary>
    public static class MachineRecipeLevelInheritanceValidator
    {
        public static string Validate(MachineRecipes machineRecipes)
        {
            var logs = "";
            var modeByTargetGuid = new Dictionary<Guid, string>();
            foreach (var recipe in machineRecipes.Data)
            {
                if (recipe.LevelInheritance == MachineRecipeMasterElement.LevelInheritanceConst.None) continue;
                logs += ValidateRecipe(recipe);
            }
            return logs;

            #region Internal

            string ValidateRecipe(MachineRecipeMasterElement recipe)
            {
                var prefix = $"[MachineRecipesMaster] Recipe GUID:{recipe.MachineRecipeGuid} levelInheritance:{recipe.LevelInheritance}";

                // 継承元・継承先はちょうど1つずつ。ちょうど1つでなければ値が返らず、以降の判定ができない
                // Exactly one source and one target; otherwise no value comes back and later checks are meaningless
                if (LevelInheritanceRecipeShape.FindSourceFamilyLength(recipe) is not int sourceLength) return $"{prefix} must have exactly one level-family source input but has {LevelInheritanceRecipeShape.CountFamilyBaseInputs(recipe)}\n";
                if (LevelInheritanceRecipeShape.FindTargetBaseGuid(recipe) is not Guid targetBaseGuid) return $"{prefix} must have exactly one level-family target output but has {LevelInheritanceRecipeShape.CountFamilyBaseOutputs(recipe)}\n";

                var errors = "";
                var targetLength = LevelInheritanceRecipeShape.GetFamilyLength(targetBaseGuid);
                if (targetLength < sourceLength) errors += $"{prefix} target family has {targetLength} levels but the source family has {sourceLength}\n";

                // 同じ継承先ファミリーで上限ありと上限なしを混ぜると、研究の効き方が経路で変わる
                // Mixing capped and uncapped recipes for one target family would make research apply per path
                if (modeByTargetGuid.TryGetValue(targetBaseGuid, out var knownMode) && knownMode != recipe.LevelInheritance)
                {
                    errors += $"{prefix} mixes Inherit and InheritUpToUnlocked for target family base {targetBaseGuid}\n";
                }
                modeByTargetGuid[targetBaseGuid] = recipe.LevelInheritance;
                return errors;
            }

            #endregion
        }
    }
}
