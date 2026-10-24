using System;
using System.Collections.Generic;
using System.Linq;
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
        // 生DTOだけで初期上限を引き、参照異常の診断は元の検証器へ残す
        // Resolve the initial limit from raw DTOs, leaving reference diagnostics to their validators
        internal static int? GetInitialStackLimit(Items items, Guid itemGuid)
        {
            var item = items.Data.FirstOrDefault(element => element.ItemGuid == itemGuid);
            // silent-ok: 未知アイテムは各利用側が診断する / マスタ読込エラーで開発者が気づく
            if (item == null) return null;
            var table = items.ItemStackLevelTables.FirstOrDefault(element => element.TableGuid == item.StackLevelTableGuid);
            // silent-ok: 参照切れ・段数0はValidateが診断する / マスタ読込エラーで開発者が気づく
            return table == null || table.StackCounts.Length == 0 ? null : table.StackCounts[0];
        }
    }
}
