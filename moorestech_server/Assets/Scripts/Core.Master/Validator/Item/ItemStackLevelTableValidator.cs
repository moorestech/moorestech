using System;
using System.Collections.Generic;
using Mooresmaster.Model.ItemsModule;

namespace Core.Master.Validator.Item
{
    // - スタックテーブルの内容を検証
    // - アイテムの参照先を検証
    // - Validate stack table contents
    // - Validate item references to tables
    internal static class ItemStackLevelTableValidator
    {
        internal static string Validate(Items items)
        {
            // 空配列・非正数・参照切れを検証
            // Validate empty arrays, nonpositive stack counts and missing table references
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
            }
            foreach (var item in items.Data)
                if (!tableGuids.Contains(item.StackLevelTableGuid))
                    logs += $"[ItemMaster.data] Name:{item.Name} references missing StackLevelTableGuid:{item.StackLevelTableGuid}\n";

            return logs;
        }
    }
}
