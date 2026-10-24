using Mooresmaster.Model.GameActionModule;

namespace Game.Block.Interface.MachineUpgrade
{
    // モジュールスロットのスタック上限の変更専用（GameActionExecutor だけが受け取る）
    // Mutation-only module-slot stack limit (only GameActionExecutor receives it)
    public interface IModuleSlotStackLimitUnlocker
    {
        void ApplyUnlockModuleSlotStackLevelAction(GameActionElement action);
    }
}
