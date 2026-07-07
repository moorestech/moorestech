using System;
using Mooresmaster.Model.GameActionModule;

namespace Core.Item.Interface
{
    // アイテムスタックレベルの変更専用インターフェース（DI注入経由でのみアクセスする）
    // Mutation-only interface for item stack levels (accessible only via DI injection)
    public interface IItemStackLevelUnlocker
    {
        void UnlockStackLevel(Guid itemGuid, int level);
        
        void ApplyUnlockItemStackLevelAction(GameActionElement action);
    }
}
