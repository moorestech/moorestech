using System;
using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block
{
    // 宇宙エレベーター拠点の参照と数値を検証する。軌道到着の表との突き合わせは起動時の検査サービスが行う
    // Validates the space elevator base's references and numbers; the orbital-table cross-check runs in the boot-time check service
    internal static class SpaceElevatorBaseParamValidator
    {
        internal static string Validate(Blocks blocks)
        {
            var logs = "";
            var cableItemGuids = new HashSet<Guid>();
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is not SpaceElevatorBaseBlockParam param) continue;
                cableItemGuids.Add(param.CableItemGuid);
                logs += ValidateBase(block.Name, param);
            }

            // プールはワールドに1つなので、全拠点が同じケーブルを数える
            // The pool is world-wide, so every base must count the same cable item
            if (1 < cableItemGuids.Count) logs += $"[BlockMaster] SpaceElevatorBase blocks must share one cableItemGuid. found:{string.Join(",", cableItemGuids)}\n";
            return logs;
            #region Internal
            string ValidateBase(string name, SpaceElevatorBaseBlockParam param)
            {
                var logs = "";
                if (MasterHolder.ItemMaster.GetItemIdOrNull(param.CableItemGuid) == null) logs += $"[BlockMaster] Name:{name} has invalid cableItemGuid:{param.CableItemGuid}\n";
                if (param.CarriedPayloadItemGuids.Length == 0) logs += $"[BlockMaster] Name:{name} carriedPayloadItemGuids must not be empty\n";
                foreach (var payloadGuid in param.CarriedPayloadItemGuids)
                {
                    if (MasterHolder.ItemMaster.GetItemIdOrNull(payloadGuid) == null) logs += $"[BlockMaster] Name:{name} has invalid carriedPayloadItemGuid:{payloadGuid}\n";

                    // ケーブルを昇降機で運べるとロケット無しでプールが増える
                    // Carrying cables on the climber would grow the pool without rockets
                    if (payloadGuid == param.CableItemGuid) logs += $"[BlockMaster] Name:{name} must not carry its own cable item:{payloadGuid}\n";
                }

                // 数値は正で有限。昇降時間は1秒以上（tick換算で0にならない）
                // Numbers must be positive and finite; the climb lasts at least one second so it never rounds to zero ticks
                if (param.CablesPerElevator < 1) logs += $"[BlockMaster] Name:{name} cablesPerElevator must be >= 1. value:{param.CablesPerElevator}\n";
                if (!(1f <= param.ClimbSeconds) || float.IsInfinity(param.ClimbSeconds)) logs += $"[BlockMaster] Name:{name} climbSeconds must be finite and >= 1. value:{param.ClimbSeconds}\n";
                if (!(0f < param.RequiredPower) || float.IsInfinity(param.RequiredPower)) logs += $"[BlockMaster] Name:{name} requiredPower must be finite and > 0. value:{param.RequiredPower}\n";
                if (param.OutputSlotCount < 1) logs += $"[BlockMaster] Name:{name} outputSlotCount must be >= 1. value:{param.OutputSlotCount}\n";
                return logs;
            }
            #endregion
        }


    }
}
