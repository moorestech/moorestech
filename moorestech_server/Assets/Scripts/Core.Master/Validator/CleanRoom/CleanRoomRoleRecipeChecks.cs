using System;
using System.Collections.Generic;
using Core.Master.Validator.MachineUpgrade;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;

namespace Core.Master.Validator
{
    /// <summary>
    ///     チップ工程の役割表（成膜・EUV・ダイシング）に共通する検証。生のマスタだけを読む
    ///     Validation shared by the chip-process role tables (deposition, EUV, dicing); reads the raw master only
    /// </summary>
    internal static class CleanRoomRoleRecipeChecks
    {
        // レシピが実在し、清浄室機械のレシピで、他の役割に使われていないこと
        // The recipe must exist, belong to a clean-room machine, and not already carry another role
        public static string Validate(string roleName, Guid machineRecipeGuid, HashSet<Guid> roleRecipeGuids, out MachineRecipeMasterElement recipe)
        {
            recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(machineRecipeGuid);
            if (recipe == null) return $"[CleanRoomMaster] {roleName} has invalid MachineRecipeGuid:{machineRecipeGuid}\n";

            var logs = "";
            if (!(MasterHolder.BlockMaster.GetBlockMaster(recipe.BlockGuid).BlockParam is CleanRoomMachineBlockParam))
            {
                logs += $"[CleanRoomMaster] {roleName} MachineRecipeGuid:{machineRecipeGuid} is not a CleanRoomMachine recipe (BlockGuid:{recipe.BlockGuid})\n";
            }
            if (!roleRecipeGuids.Add(machineRecipeGuid))
            {
                logs += $"[CleanRoomMaster] {roleName} MachineRecipeGuid:{machineRecipeGuid} already has another clean-room role\n";
            }
            return logs;
        }

        // 出力に指定アイテムがちょうど1回あること（添字は役割レコードが索引のときに引く）
        // The item must appear exactly once among the outputs (the role record looks up the index at indexing)
        public static string RequireSingleOutput(string roleName, MachineRecipeMasterElement recipe, Guid itemGuid)
        {
            var hitCount = 0;
            foreach (var output in recipe.OutputItems)
            {
                if (output.ItemGuid == itemGuid) hitCount++;
            }
            return hitCount == 1 ? "" : $"[CleanRoomMaster] {roleName} MachineRecipeGuid:{recipe.MachineRecipeGuid} must list ItemGuid:{itemGuid} exactly once in outputItems (found {hitCount})\n";
        }

        // レベルファミリーの基準アイテムであること。変種数（グレード数・レベル数）を返し、満たさなければ null
        // The item must be a level family's base; returns the variant count (grade or level count), or null when it is not
        public static string RequireFamilyBase(string roleName, Guid machineRecipeGuid, Guid itemGuid, out int? variantCount)
        {
            variantCount = null;
            if (MasterHolder.ItemMaster.GetItemIdOrNull(itemGuid) == null) return $"[CleanRoomMaster] {roleName} MachineRecipeGuid:{machineRecipeGuid} has invalid ItemGuid:{itemGuid}\n";
            if (!LevelInheritanceRecipeShape.IsLevelFamilyBase(itemGuid)) return $"[CleanRoomMaster] {roleName} MachineRecipeGuid:{machineRecipeGuid} ItemGuid:{itemGuid} is not the base item of a level family\n";

            // ファミリーの長さの式は C2 の LevelInheritanceRecipeShape の1か所
            // The family-length formula lives only in C2's LevelInheritanceRecipeShape
            variantCount = LevelInheritanceRecipeShape.GetFamilyLength(itemGuid);
            return "";
        }

        // グレードを入力から受け継ぐ役割（EUV・ダイシング）の共通検証。入力と出力のグレード数を返し、引けなければ null
        // Shared validation for roles that inherit the grade from the input (EUV, dicing); returns the input and output grade counts, or null when unresolved
        public static string ValidateGradeInheritingRole(string roleName, MachineRecipeMasterElement recipe, Guid outputBaseItemGuid, out int? sourceGradeCount, out int? outputVariantCount)
        {
            var logs = RequireInheritance(roleName, recipe);
            logs += ResolveSourceGradeCount(roleName, recipe, out sourceGradeCount);
            logs += RequireFamilyBase(roleName, recipe.MachineRecipeGuid, outputBaseItemGuid, out outputVariantCount);
            return logs;
        }

        // レベル継承レシピでなければ、入力のグレードは常にG1として扱われてしまう
        // Unless the recipe is a level-inheritance recipe, the input grade would always be treated as G1
        private static string RequireInheritance(string roleName, MachineRecipeMasterElement recipe)
        {
            if (recipe.LevelInheritance == MachineRecipeMasterElement.LevelInheritanceConst.Inherit) return "";
            return $"[CleanRoomMaster] {roleName} MachineRecipeGuid:{recipe.MachineRecipeGuid} must use levelInheritance Inherit (found {recipe.LevelInheritance})\n";
        }

        // グレードの出どころ（継承元の入力）はちょうど1つ。機械レシピの検証の順序に頼らず、ここでも確かめる
        // The grade source (the inheritance source input) must be exactly one; checked here too instead of relying on the machine-recipe validation order
        private static string ResolveSourceGradeCount(string roleName, MachineRecipeMasterElement recipe, out int? gradeCount)
        {
            gradeCount = null;
            if (LevelInheritanceRecipeShape.FindSourceFamilyLength(recipe) is not int length) return $"[CleanRoomMaster] {roleName} MachineRecipeGuid:{recipe.MachineRecipeGuid} must have exactly one level-family base input to take the grade from (found {LevelInheritanceRecipeShape.CountFamilyBaseInputs(recipe)})\n";
            gradeCount = length;
            return "";
        }
    }
}
