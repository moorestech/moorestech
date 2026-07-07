using System;
using System.Collections.Generic;
using Core.Master;

namespace Core.Item.Interface
{
    // アイテムスタックレベルの取得専用インターフェース（static公開はこちらのみ）
    // Read-only lookup interface for item stack levels (only this side is exposed statically)
    public interface IItemStackLevelLookup
    {
        // 解放済みレベル一覧（レベル1の初期値は含まない）
        // All unlocked levels (default level 1 entries are excluded)
        IReadOnlyDictionary<Guid, int> UnlockedLevels { get; }
        
        IObservable<(Guid itemGuid, int level)> OnStackLevelUnlocked { get; }

        int GetMaxStack(ItemId itemId);

        int GetUnlockedLevel(Guid itemGuid);
    }
}
