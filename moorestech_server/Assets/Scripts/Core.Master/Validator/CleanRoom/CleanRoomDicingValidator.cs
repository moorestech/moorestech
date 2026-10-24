using System;
using System.Collections.Generic;
using Mooresmaster.Model.CleanRoomModule;
using Mooresmaster.Model.MachineRecipesModule;

namespace Core.Master.Validator
{
    // ダイシング（損失とレベル別の切り分け）の役割表を検証する
    // Validates the dicing (loss and per-level split) role table
    internal static class CleanRoomDicingValidator
    {
        // 1サイクルの出力セット数の上限。生産性は最大1セット追加する（ExtraOutputChanceは1でクランプ）
        // Maximum output sets per cycle; productivity adds at most one extra set (ExtraOutputChance is clamped to 1)
        public const int MaxOutputSetsPerCycle = 2;

        private const string RoleName = "Dicings";

        public static string Validate(DicingsElement[] dicings, HashSet<Guid> roleRecipeGuids)
        {
            var logs = "";
            foreach (var dicing in dicings)
            {
                logs += CleanRoomRoleRecipeChecks.Validate(RoleName, dicing.MachineRecipeGuid, roleRecipeGuids, out var recipe);
                if (recipe == null) continue;

                // 入力ウェハのグレードを受け継ぎ、グレード数ぶんの表を持つ（グレード数が引けたときだけ比べる）
                // Inherit the input wafer's grade and hold one table per grade (compared only when the grade count resolves)
                logs += CleanRoomRoleRecipeChecks.ValidateGradeInheritingRole(RoleName, recipe, dicing.DieItemGuid, out var gradeCount, out var dieLevelCount);
                if (gradeCount is int waferGradeCount && dicing.GradeDieTables.Length != waferGradeCount) logs += $"[CleanRoomMaster] {RoleName} MachineRecipeGuid:{dicing.MachineRecipeGuid} has {dicing.GradeDieTables.Length} grade tables for {waferGradeCount} wafer grades\n";
                if (dicing.DiesPerWafer < 1) logs += $"[CleanRoomMaster] {RoleName} MachineRecipeGuid:{dicing.MachineRecipeGuid} DiesPerWafer:{dicing.DiesPerWafer} must be >= 1\n";
                if (dicing.MaxLossReduction < 0f || 1f < dicing.MaxLossReduction) logs += $"[CleanRoomMaster] {RoleName} MachineRecipeGuid:{dicing.MachineRecipeGuid} MaxLossReduction:{dicing.MaxLossReduction} is out of [0,1]\n";

                // ダイのレベル数が引けたときだけ表の形と出力の並びを見る（引けない理由は上でログ済み）
                // Check the table shape and output layout only when the die level count resolves (the reason it did not is already logged above)
                if (dieLevelCount is not int levelCount) continue;
                var tables = new List<IReadOnlyList<float>>();
                foreach (var table in dicing.GradeDieTables) tables.Add(table.LevelRates);
                logs += CleanRoomYieldTableRules.ValidateDieTables($"{RoleName} MachineRecipeGuid:{dicing.MachineRecipeGuid}", tables, levelCount);
                logs += OutputLayoutValidation(recipe, dicing, levelCount);
            }
            return logs;
        }

        // 出力はちょうど [ダイLv1..LvN, 失敗ダイ]、個数はどれもdiesPerWafer、スタック表は最小でも2セット分を1スロットに積める
        // Outputs are exactly [die Lv1..LvN, failed die], each counting diesPerWafer, and every stack table fits two sets in one slot
        private static string OutputLayoutValidation(MachineRecipeMasterElement recipe, DicingsElement dicing, int dieLevelCount)
        {
            var expectedGuids = new List<Guid>();
            foreach (var variantId in MasterHolder.ItemMaster.GetLevelVariants(MasterHolder.ItemMaster.GetItemId(dicing.DieItemGuid))) expectedGuids.Add(MasterHolder.ItemMaster.GetItemGuid(variantId));
            expectedGuids.Add(dicing.FailedDieItemGuid);

            var prefix = $"[CleanRoomMaster] {RoleName} MachineRecipeGuid:{dicing.MachineRecipeGuid}";
            if (recipe.OutputItems.Length != dieLevelCount + 1) return $"{prefix} outputItems must list {dieLevelCount} die levels then the failed die (found {recipe.OutputItems.Length})\n";

            var logs = "";
            for (var i = 0; i < expectedGuids.Count; i++)
            {
                var output = recipe.OutputItems[i];
                if (output.ItemGuid != expectedGuids[i]) logs += $"{prefix} outputItems[{i}] must be ItemGuid:{expectedGuids[i]} (found {output.ItemGuid})\n";
                if (output.Count != dicing.DiesPerWafer) logs += $"{prefix} outputItems[{i}] Count:{output.Count} must equal DiesPerWafer:{dicing.DiesPerWafer}\n";
                var minStack = MinStackCount(output.ItemGuid);
                if (minStack < dicing.DiesPerWafer * MaxOutputSetsPerCycle) logs += $"{prefix} outputItems[{i}] ItemGuid:{output.ItemGuid} min stack {minStack} is below {dicing.DiesPerWafer * MaxOutputSetsPerCycle} (DiesPerWafer x {MaxOutputSetsPerCycle})\n";
            }
            return logs;
        }

        private static int MinStackCount(Guid itemGuid)
        {
            var stackCounts = MasterHolder.ItemMaster.GetStackLevelTable(MasterHolder.ItemMaster.GetItemMaster(itemGuid).StackLevelTableGuid).StackCounts;
            var min = int.MaxValue;
            foreach (var count in stackCounts) min = Math.Min(min, count);
            return min;
        }
    }
}
