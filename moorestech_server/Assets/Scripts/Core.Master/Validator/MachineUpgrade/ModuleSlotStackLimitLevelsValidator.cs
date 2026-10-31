using Mooresmaster.Model.ItemsModule;

namespace Core.Master.Validator.MachineUpgrade
{
    /// <summary>
    ///     モジュールスロットのスタック上限表（1段以上・各1以上・単調非減少）を検証する
    ///     Validates the module-slot stack limit table: at least one level, each at least 1, never decreasing
    /// </summary>
    public static class ModuleSlotStackLimitLevelsValidator
    {
        public static string Validate(Items items)
        {
            var levels = items.ModuleSlotStackLimitLevels;
            if (levels.Length == 0) return "[ItemMaster] moduleSlotStackLimitLevels must have at least one level\n";

            var logs = "";
            for (var i = 0; i < levels.Length; i++)
            {
                if (levels[i].StackLimit < 1) logs += $"[ItemMaster] moduleSlotStackLimitLevels[{i}].stackLimit ({levels[i].StackLimit}) must be at least 1\n";
                if (0 < i && levels[i].StackLimit < levels[i - 1].StackLimit) logs += $"[ItemMaster] moduleSlotStackLimitLevels[{i}].stackLimit ({levels[i].StackLimit}) must not be below level {i - 1} ({levels[i - 1].StackLimit})\n";
            }
            return logs;
        }
    }
}
