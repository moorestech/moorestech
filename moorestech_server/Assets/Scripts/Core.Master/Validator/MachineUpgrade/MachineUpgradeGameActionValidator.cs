using Mooresmaster.Model.GameActionModule;

namespace Core.Master.Validator.MachineUpgrade
{
    /// <summary>
    ///     機械の強化系アクション（スロット上限・製作上限）の引数を検証する。研究とチャレンジの両方から呼ぶ
    ///     Validates machine-upgrade action params (slot limit, craft cap); called from both research and challenges
    /// </summary>
    public static class MachineUpgradeGameActionValidator
    {
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

            #endregion
        }
    }
}
