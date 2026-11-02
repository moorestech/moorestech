using System;
using System.Collections.Generic;

namespace Game.Block.Interface.MachineUpgrade
{
    // 継承レシピの製作上限の取得専用（入力束縛・同期が読む）
    // Read-only craft cap of inheritance recipes (the input binding and sync read it)
    public interface ILevelInheritanceCraftCapLookup
    {
        // 継承先ファミリーの基準アイテムGUID → 解放済みの上限（初期値 Lv1 は含まない）
        // Target family base item GUID → unlocked cap (the initial Lv1 is excluded)
        IReadOnlyDictionary<Guid, int> UnlockedCaps { get; }

        IObservable<(Guid familyBaseItemGuid, int level)> OnCraftCapUnlocked { get; }

        int GetCraftCap(Guid familyBaseItemGuid);
    }
}
