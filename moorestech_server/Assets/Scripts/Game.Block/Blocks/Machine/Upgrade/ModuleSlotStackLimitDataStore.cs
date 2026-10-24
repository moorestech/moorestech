using System;
using Game.Block.Interface.MachineUpgrade;
using Mooresmaster.Model.GameActionModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Machine.Upgrade
{
    /// <summary>
    ///     研究で段階解放するモジュールスロットのスタック上限。保存せず、研究・チャレンジのロード時再実行で戻す
    ///     Module-slot stack limit unlocked in stages by research; not saved, restored by the load-time replay of research and challenges
    /// </summary>
    public class ModuleSlotStackLimitDataStore : IModuleSlotStackLimitLookup, IModuleSlotStackLimitUnlocker
    {
        public int CurrentLevel { get; private set; }
        public int CurrentStackLimit => ModuleSlotStackLimitMasterUtil.GetStackLimit(CurrentLevel);
        public IObservable<int> OnStackLimitChanged => _onStackLimitChanged;

        private readonly Subject<int> _onStackLimitChanged = new();

        public void UnlockLevel(int level)
        {
            // レベルは下がらない冪等操作。範囲 0..最大 は MachineUpgradeGameActionValidator が起動時に保証するので丸めず、範囲外は呼び出し側の誤りとして止める（製作上限と同じ扱い）
            // Idempotent and never decreasing; MachineUpgradeGameActionValidator guarantees 0..max at boot, so no clamping and an out-of-range level stops as a caller bug (same as the craft cap)
            if (level < 0 || ModuleSlotStackLimitMasterUtil.GetMaxLevel() < level) throw new ArgumentOutOfRangeException(nameof(level), level, $"モジュールスロットのスタックレベルが段階表の範囲 0..{ModuleSlotStackLimitMasterUtil.GetMaxLevel()} の外です");
            // silent-ok: 同じか下のレベルは研究のロード時再実行・チャレンジとの重なりで正常に来る / 現在の上限は機械UIのモジュール欄（C9）に出る
            if (level <= CurrentLevel) return;

            // 上限が実際に変わったときだけ通知する
            // Notify only when the limit actually changes
            var beforeLimit = CurrentStackLimit;
            CurrentLevel = level;
            if (CurrentStackLimit != beforeLimit) _onStackLimitChanged.OnNext(CurrentStackLimit);
        }

        public void ApplyUnlockModuleSlotStackLevelAction(GameActionElement action)
        {
            if (action.GameActionType != GameActionElement.GameActionTypeConst.unlockModuleSlotStackLevel)
            {
                Debug.LogError($"[ModuleSlotStackLimitDataStore] Unexpected action type: {action.GameActionType}");
                return;
            }
            UnlockLevel(((UnlockModuleSlotStackLevelGameActionParam)action.GameActionParam).Level);
        }
    }
}
