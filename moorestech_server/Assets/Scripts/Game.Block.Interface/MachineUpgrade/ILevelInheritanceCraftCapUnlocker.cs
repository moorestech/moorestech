using System;
using Mooresmaster.Model.GameActionModule;

namespace Game.Block.Interface.MachineUpgrade
{
    // 継承レシピの製作上限の変更専用（GameActionExecutor だけが受け取る）
    // Mutation-only craft cap of inheritance recipes (only GameActionExecutor receives it)
    public interface ILevelInheritanceCraftCapUnlocker
    {
        void ApplyUnlockLevelInheritanceCraftLevelAction(GameActionElement action);
    }
}
