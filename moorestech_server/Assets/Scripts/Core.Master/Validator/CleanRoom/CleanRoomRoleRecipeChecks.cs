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
    }
}
