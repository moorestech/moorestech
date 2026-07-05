using System;
using System.Collections.Generic;
using Core.Master;
using UniRx;

namespace Core.Item
{
    // アイテムごとの解放済みスタックレベルを保持する実行時状態ストア
    // Runtime store that owns unlocked stack levels per item
    public class ItemStackLevelDataStore
    {
        public static ItemStackLevelDataStore Instance { get; private set; }

        public IObservable<(Guid itemGuid, int level)> OnStackLevelUnlocked => _onStackLevelUnlocked;
        private readonly Subject<(Guid itemGuid, int level)> _onStackLevelUnlocked = new();

        // アイテムGUID → 解放済み最大レベル（未登録はレベル1）
        // Item GUID → unlocked max level (absent means level 1)
        private readonly Dictionary<Guid, int> _unlockedLevels = new();

        public ItemStackLevelDataStore()
        {
            Instance = this;
        }

        public int GetMaxStack(ItemId itemId)
        {
            var element = MasterHolder.ItemMaster.GetItemMaster(itemId);
            var table = MasterHolder.ItemMaster.GetStackLevelTable(element.StackLevelTableGuid);
            var index = Math.Clamp(GetUnlockedLevel(element.ItemGuid) - 1, 0, table.StackCounts.Length - 1);
            return table.StackCounts[index];
        }

        public int GetUnlockedLevel(Guid itemGuid)
        {
            return _unlockedLevels.GetValueOrDefault(itemGuid, 1);
        }

        public void UnlockStackLevel(Guid itemGuid, int level)
        {
            // 冪等: 既に同レベル以上なら何もしない。テーブル長でクランプ
            // Idempotent: no-op when already unlocked; clamp to table length
            var element = MasterHolder.ItemMaster.GetItemMaster(itemGuid);
            var table = MasterHolder.ItemMaster.GetStackLevelTable(element.StackLevelTableGuid);
            var clamped = Math.Clamp(level, 1, table.StackCounts.Length);
            if (clamped <= GetUnlockedLevel(itemGuid)) return;

            _unlockedLevels[itemGuid] = clamped;
            _onStackLevelUnlocked.OnNext((itemGuid, clamped));
        }

        #region SaveLoad

        public Dictionary<string, int> GetSaveJsonObject()
        {
            // レベル1（初期値）は保存しない
            // Skip level 1 (default) entries
            var result = new Dictionary<string, int>();
            foreach (var (guid, level) in _unlockedLevels)
            {
                if (level <= 1) continue;
                result[guid.ToString()] = level;
            }
            return result;
        }

        public void LoadUnlockedLevels(Dictionary<string, int> saveData)
        {
            _unlockedLevels.Clear();
            if (saveData == null) return;
            foreach (var (guidStr, level) in saveData)
            {
                if (!Guid.TryParse(guidStr, out var guid)) continue;
                if (!MasterHolder.ItemMaster.ExistItemId(guid)) continue;
                UnlockStackLevel(guid, level);
            }
        }

        #endregion
    }
}
