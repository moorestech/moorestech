using System.Collections.Generic;
using Mooresmaster.Model.GameActionModule;

namespace Game.Action
{
    /// <summary>
    ///     ロード時・チャレンジ再開時に再実行する解放系アクションの種別。付与とスキットは一度きりなので入れない。全種別がこの集合か一度きりのどちらか一方に入ることは UnlockReplayGameActionTypesCoverageTest が固定する
    ///     Unlock action kinds re-run on load and challenge resume; grants and skits happen once, so they are excluded; UnlockReplayGameActionTypesCoverageTest pins that every kind is in exactly one of this set or the run-once ledger
    /// </summary>
    public static class UnlockReplayGameActionTypes
    {
        // 外へは読み取り専用で出す（書き換えでロード後の解放が黙って消えないように）
        // Exposed read-only so no caller can edit it and silently lose unlocks after loading
        public static IReadOnlyCollection<string> Types { get; } = new HashSet<string>()
        {
            GameActionElement.GameActionTypeConst.unlockCraftRecipe,
            GameActionElement.GameActionTypeConst.unlockItemRecipeView,
            GameActionElement.GameActionTypeConst.unlockChallengeCategory,
            GameActionElement.GameActionTypeConst.unlockMachineRecipe,
            GameActionElement.GameActionTypeConst.unlockItemStackLevel,
            GameActionElement.GameActionTypeConst.unlockBlock,
            GameActionElement.GameActionTypeConst.unlockTrainCar,
            GameActionElement.GameActionTypeConst.unlockConnectTool,
            GameActionElement.GameActionTypeConst.unlockPlayerInventorySlotLevel,
            GameActionElement.GameActionTypeConst.unlockBlueprint,
            GameActionElement.GameActionTypeConst.unlockModuleSlotStackLevel,
            GameActionElement.GameActionTypeConst.unlockLevelInheritanceCraftLevel,
        };
    }
}
