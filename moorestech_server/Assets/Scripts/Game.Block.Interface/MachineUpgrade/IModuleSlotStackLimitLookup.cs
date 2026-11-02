using System;

namespace Game.Block.Interface.MachineUpgrade
{
    // モジュールスロットのスタック上限の取得専用（機械のインベントリ・効果集計・同期が読む）
    // Read-only module-slot stack limit (machine inventories, effect aggregation and sync read it)
    public interface IModuleSlotStackLimitLookup
    {
        int CurrentLevel { get; }
        int CurrentStackLimit { get; }

        // 値は変化後の上限
        // The value is the limit after the change
        IObservable<int> OnStackLimitChanged { get; }
    }
}
