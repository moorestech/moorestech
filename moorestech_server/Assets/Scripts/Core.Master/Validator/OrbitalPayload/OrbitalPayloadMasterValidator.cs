using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.ItemsModule;

namespace Core.Master.Validator.OrbitalPayload
{
    // 積荷表の行がアイテム定義と矛盾しないことを読込時に確かめる
    // Validates at load that orbital payload rows agree with the item definitions
    internal static class OrbitalPayloadMasterValidator
    {
        internal static string Validate(Items items)
        {
            var logs = "";
            var itemByGuid = items.Data.ToDictionary(item => item.ItemGuid);
            var tableByGuid = items.ItemStackLevelTables.ToDictionary(table => table.TableGuid);
            var seenPayloads = new HashSet<Guid>();
            foreach (var payload in items.OrbitalPayloads)
            {
                logs += ValidatePayloadItem(payload);
                foreach (var output in payload.ArrivalOutputs) logs += ValidateOutput(payload, output);
            }
            return logs;

            #region Internal

            string ValidatePayloadItem(OrbitalPayloadMasterElement payload)
            {
                // 積荷は実在し、表に1回だけ載り、1便ぶんが1スロットに収まる
                // A payload must exist, appear once, and fit one launch into a single slot
                if (!itemByGuid.TryGetValue(payload.PayloadItemGuid, out var item)) return $"[ItemMaster.orbitalPayloads] unknown payloadItemGuid:{payload.PayloadItemGuid}\n";
                if (!seenPayloads.Add(payload.PayloadItemGuid)) return $"[ItemMaster.orbitalPayloads] duplicate payload:{item.Name}\n";
                if (payload.CountPerLaunch < 1) return $"[ItemMaster.orbitalPayloads] {item.Name} countPerLaunch must be >= 1 (was {payload.CountPerLaunch})\n";

                // 発射台は積荷1種につき1スロットなので、レベル1のスタック上限を超える1便は積めない
                // A launch pad has one slot per payload, so a launch above the level-1 stack limit can never be loaded
                // silent-ok: テーブル欠落・段数0のテーブルは ItemStackLevelTableValidator が報告する / マスタ読込時のエラーログで開発者が気づく
                if (!tableByGuid.TryGetValue(item.StackLevelTableGuid, out var table) || table.StackCounts.Length == 0) return "";
                var firstLevelStack = table.StackCounts[0];
                return payload.CountPerLaunch > firstLevelStack ? $"[ItemMaster.orbitalPayloads] {item.Name} countPerLaunch {payload.CountPerLaunch} exceeds level-1 stack {firstLevelStack}\n" : "";
            }

            string ValidateOutput(OrbitalPayloadMasterElement payload, OrbitalArrivalOutputElement output)
            {
                if (!itemByGuid.ContainsKey(output.ItemGuid)) return $"[ItemMaster.orbitalPayloads] payload:{payload.PayloadItemGuid} has unknown arrival output:{output.ItemGuid}\n";
                return output.Count < 1 ? $"[ItemMaster.orbitalPayloads] payload:{payload.PayloadItemGuid} arrival output count must be >= 1\n" : "";
            }

            #endregion
        }
    }
}
