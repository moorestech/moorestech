using System;
using System.Collections.Generic;

namespace Core.Master.Validator
{
    // 部屋要約を出す境界ブロックと汚染接続点のGUID集合を検証する（参照の実在と集合内の重複）
    // Validates the GUID sets of room-summary boundary blocks and pollution connectors (references exist, no duplicates within a set)
    internal static class CleanRoomBoundaryBlockSetValidator
    {
        public static string Validate(string setName, Guid[] blockGuids)
        {
            var logs = "";
            var seen = new HashSet<Guid>();
            foreach (var blockGuid in blockGuids)
            {
                if (MasterHolder.BlockMaster.GetBlockIdOrNull(blockGuid) == null)
                {
                    logs += $"[CleanRoomMaster] {setName} has invalid BlockGuid:{blockGuid}\n";
                }

                if (!seen.Add(blockGuid))
                {
                    logs += $"[CleanRoomMaster] {setName} has duplicated BlockGuid:{blockGuid}\n";
                }
            }

            return logs;
        }
    }
}
