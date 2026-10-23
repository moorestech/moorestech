using System;
using System.Collections.Generic;
using Mooresmaster.Model.ItemsModule;

namespace Core.Master.Validator.Item
{
    // スタックテーブルの中身と、各アイテムからの参照先テーブルの実在を検証する
    // Validates stack level table contents and that each item references an existing table
    internal static class ItemStackLevelTableValidator
    {
        internal static string Validate(Items items)
        {
            // 空配列・1未満のスタック数・アイテムからの参照先テーブル実在を検証
            // Validate empty arrays, sub-1 counts, and that each item references an existing table
            var logs = "";
            if (items.ItemStackLevelTables == null) return logs;

            var tableGuids = new HashSet<Guid>();
            foreach (var table in items.ItemStackLevelTables)
            {
                // TableGuid重複はInitializeのDictionary.Addが生の例外で落ちるため検証で弾く
                // Duplicate TableGuid would throw raw in Initialize's Dictionary.Add, so reject it here
                if (!tableGuids.Add(table.TableGuid))
                    logs += $"[ItemMaster.itemStackLevelTables] Name:{table.Name} has duplicate TableGuid:{table.TableGuid}\n";
                if (table.StackCounts.Length == 0)
                    logs += $"[ItemMaster.itemStackLevelTables] Name:{table.Name} has empty StackCounts\n";
                foreach (var count in table.StackCounts)
                    if (count < 1)
                        logs += $"[ItemMaster.itemStackLevelTables] Name:{table.Name} has invalid StackCount:{count} (must be >= 1)\n";
                // 研究解放で容量が減る表は、起動時の容量保証を壊す
                // A capacity decrease after research breaks boot-time capacity guarantees
                for (var i = 1; i < table.StackCounts.Length; i++)
                    if (table.StackCounts[i] < table.StackCounts[i - 1])
                        logs += $"[ItemMaster.itemStackLevelTables] Name:{table.Name} StackCounts must be nondecreasing: level {i} ({table.StackCounts[i - 1]}) to level {i + 1} ({table.StackCounts[i]})\n";
            }
            foreach (var item in items.Data)
                if (!tableGuids.Contains(item.StackLevelTableGuid))
                    logs += $"[ItemMaster.data] Name:{item.Name} references missing StackLevelTableGuid:{item.StackLevelTableGuid}\n";

            return logs;
        }
    }
}
