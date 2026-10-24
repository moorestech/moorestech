using System;
using Mooresmaster.Model.GameActionModule;
using Mooresmaster.Model.MachineRecipesModule;

namespace Core.Master.Validator.MachineUpgrade
{
    /// <summary>
    ///     機械の強化系アクション（スロット上限・製作上限）の引数を検証する。研究とチャレンジの両方から呼ぶ
    ///     Validates machine-upgrade action params (slot limit, craft cap); called from both research and challenges
    /// </summary>
    public static class MachineUpgradeGameActionValidator
    {
        // 製作上限は1始まり（Core.Master は Game.Block を参照できないので、実行時の初期値 LevelInheritanceCraftCapTable.InitialCraftCap とは別に持つ）
        // Craft caps are 1-based (Core.Master cannot reference Game.Block, so this is kept apart from the runtime LevelInheritanceCraftCapTable.InitialCraftCap)
        private const int MinCraftCapLevel = 1;

        public static string Validate(GameActionElement[] actions, string ownerLabel)
        {
            // 既存の検証と同じく、アクション配列の欠落は空として扱う
            // Like the existing validators, a missing action array counts as empty
            if (actions == null) return "";

            var logs = "";
            foreach (var action in actions)
            {
                switch (action.GameActionParam)
                {
                    case UnlockModuleSlotStackLevelGameActionParam slotLevel:
                        logs += ValidateModuleSlotStackLevel(slotLevel);
                        break;
                    case UnlockLevelInheritanceCraftLevelGameActionParam craftLevel:
                        logs += ValidateLevelInheritanceCraftLevel(craftLevel);
                        break;
                }
            }
            return logs;

            #region Internal

            string ValidateModuleSlotStackLevel(UnlockModuleSlotStackLevelGameActionParam param)
            {
                // 実行時の util（Game.Block.Interface）は使わず、生のマスタの段階表を読む
                // Read the raw master table instead of the runtime util (Game.Block.Interface)
                var maxLevel = MasterHolder.ItemMaster.Items.ModuleSlotStackLimitLevels.Length - 1;
                if (0 <= param.Level && param.Level <= maxLevel) return "";
                return $"[MachineUpgrade] {ownerLabel} unlockModuleSlotStackLevel level {param.Level} is outside 0..{maxLevel}\n";
            }

            string ValidateLevelInheritanceCraftLevel(UnlockLevelInheritanceCraftLevelGameActionParam param)
            {
                // 実行側が無ガードで走査するため、配列の欠落は検証で止める
                // The executor iterates without a guard, so a missing array stops here
                if (param.TargetItemGuids == null) return $"[MachineUpgrade] {ownerLabel} unlockLevelInheritanceCraftLevel has null targetItemGuids\n";

                var errors = "";
                foreach (var targetGuid in param.TargetItemGuids)
                {
                    if (!LevelInheritanceRecipeShape.IsLevelFamilyBase(targetGuid))
                    {
                        errors += $"[MachineUpgrade] {ownerLabel} unlockLevelInheritanceCraftLevel target {targetGuid} is not a level-family base item\n";
                        continue;
                    }

                    // 上限つきレシピの継承先でなければ上限は誰にも効かない
                    // Unless a capped recipe targets it, the cap would affect nothing
                    if (!HasCappedRecipeFor(targetGuid)) errors += $"[MachineUpgrade] {ownerLabel} unlockLevelInheritanceCraftLevel target {targetGuid} is not the target of any InheritUpToUnlocked recipe\n";

                    var familyLength = LevelInheritanceRecipeShape.GetFamilyLength(targetGuid);
                    if (param.Level < MinCraftCapLevel || familyLength < param.Level) errors += $"[MachineUpgrade] {ownerLabel} unlockLevelInheritanceCraftLevel level {param.Level} is outside 1..{familyLength} for {targetGuid}\n";
                }
                return errors;
            }

            bool HasCappedRecipeFor(Guid targetGuid)
            {
                foreach (var recipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
                {
                    if (recipe.LevelInheritance == MachineRecipeMasterElement.LevelInheritanceConst.InheritUpToUnlocked && LevelInheritanceRecipeShape.FindTargetBaseGuid(recipe) == targetGuid) return true;
                }
                return false;
            }

            #endregion
        }
    }
}
