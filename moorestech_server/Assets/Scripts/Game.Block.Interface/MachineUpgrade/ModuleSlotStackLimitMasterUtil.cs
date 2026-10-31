using Core.Master;

namespace Game.Block.Interface.MachineUpgrade
{
    /// <summary>
    ///     モジュールスロットのスタック上限の段階表を引く。表の形はマスタ検証が保証する
    ///     Looks up the module-slot stack limit table; master validation guarantees its shape
    /// </summary>
    public static class ModuleSlotStackLimitMasterUtil
    {
        // level は 0..GetMaxLevel()。範囲は起動時の検証が保証し、DataStore の UnlockLevel は範囲外を例外で止めるので、ここでは丸めない
        // level is 0..GetMaxLevel(); boot validation guarantees the range and the DataStore's UnlockLevel throws on anything outside, so no clamping here
        public static int GetStackLimit(int level)
        {
            return MasterHolder.ItemMaster.Items.ModuleSlotStackLimitLevels[level].StackLimit;
        }

        public static int GetMaxLevel()
        {
            return MasterHolder.ItemMaster.Items.ModuleSlotStackLimitLevels.Length - 1;
        }
    }
}
