using System;
using System.Collections.Generic;
using Game.Block.Interface.MachineUpgrade;
using Mooresmaster.Model.GameActionModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Machine.Upgrade
{
    /// <summary>
    ///     継承先ファミリーごとの製作上限。保存せず、研究・チャレンジのロード時再実行で戻す
    ///     Craft cap per target family; not saved, restored by the load-time replay of research and challenges
    /// </summary>
    public class LevelInheritanceCraftCapDataStore : ILevelInheritanceCraftCapLookup, ILevelInheritanceCraftCapUnlocker
    {
        public IReadOnlyDictionary<Guid, int> UnlockedCaps => _capTable.UnlockedCaps;
        public IObservable<(Guid familyBaseItemGuid, int level)> OnCraftCapUnlocked => _onCraftCapUnlocked;

        private readonly Subject<(Guid familyBaseItemGuid, int level)> _onCraftCapUnlocked = new();
        private readonly LevelInheritanceCraftCapTable _capTable = new();

        public int GetCraftCap(Guid familyBaseItemGuid)
        {
            return _capTable.GetCraftCap(familyBaseItemGuid);
        }

        public void UnlockCraftCap(Guid familyBaseItemGuid, int level)
        {
            // 冪等で上がる方向だけ。レベルの範囲 1..ファミリー長 は MachineUpgradeGameActionValidator が起動時に保証する
            // Idempotent and only rising; MachineUpgradeGameActionValidator guarantees the 1..family-length range at boot
            if (!_capTable.TryRaise(familyBaseItemGuid, level)) return;
            _onCraftCapUnlocked.OnNext((familyBaseItemGuid, level));
        }

        public void ApplyUnlockLevelInheritanceCraftLevelAction(GameActionElement action)
        {
            if (action.GameActionType != GameActionElement.GameActionTypeConst.unlockLevelInheritanceCraftLevel)
            {
                Debug.LogError($"[LevelInheritanceCraftCapDataStore] Unexpected action type: {action.GameActionType}");
                return;
            }

            var param = (UnlockLevelInheritanceCraftLevelGameActionParam)action.GameActionParam;
            foreach (var targetGuid in param.TargetItemGuids)
            {
                UnlockCraftCap(targetGuid, param.Level);
            }
        }
    }
}
