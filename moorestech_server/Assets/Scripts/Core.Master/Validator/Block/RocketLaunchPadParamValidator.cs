using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.BlocksModule;
using Core.Master.Validator.Item;

namespace Core.Master.Validator.Block
{
    // 発射台が建造・打ち上げを進められないパラメータを読込時に止める
    // Stops at load any launch pad parameters that could never build or launch
    internal static class RocketLaunchPadParamValidator
    {
        internal static string Validate(Blocks blocks)
        {
            var logs = "";
            var payloads = MasterHolder.ItemMaster.Items.OrbitalPayloads;
            var payloadGuids = new HashSet<Guid>(payloads.Select(payload => payload.PayloadItemGuid));
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is not RocketLaunchPadBlockParam param) continue;
                logs += ValidateScalars(block.Name, param);
                logs += ValidateMaterials(block.Name, param);
                logs += ValidateOutputCapacity(block.Name, param);

                // 積荷表が空だと揃う積荷が無く、永遠に建造しない
                // With an empty payload table nothing can ever be loaded, so the pad would never build
                if (payloadGuids.Count == 0) logs += $"[BlockMaster] {block.Name} is a RocketLaunchPad but items.orbitalPayloads is empty\n";
            }
            return logs;

            #region Internal

            string ValidateScalars(string name, RocketLaunchPadBlockParam param)
            {
                var result = "";
                // 非有限の電力・秒数では建造や発射が完了しない
                // Non-finite power or durations cannot complete construction or launch
                if (!IsPositiveFinite(param.RequiredPower)) result += $"[BlockMaster] {name} requiredPower must be finite and > 0\n";
                if (!IsPositiveFinite(param.BuildSeconds)) result += $"[BlockMaster] {name} buildSeconds must be finite and > 0\n";
                if (!IsPositiveFinite(param.LaunchSeconds)) result += $"[BlockMaster] {name} launchSeconds must be finite and > 0\n";
                if (param.OutputSlotCount < 1) result += $"[BlockMaster] {name} outputSlotCount must be >= 1\n";
                if (param.LaunchMaterials.Length == 0) result += $"[BlockMaster] {name} launchMaterials must not be empty\n";
                return result;
            }

            bool IsPositiveFinite(float value) => 0 < value && !float.IsInfinity(value);

            string ValidateMaterials(string name, RocketLaunchPadBlockParam param)
            {
                // 資材は実在・重複なし・積荷と別アイテム・1便ぶんがレベル1のスタックに収まる
                // Materials must exist, be unique, differ from payloads, and fit one launch in a level-1 stack
                var result = "";
                var seen = new HashSet<Guid>();
                foreach (var material in param.LaunchMaterials)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(material.ItemGuid);
                    if (itemId == null) { result += $"[BlockMaster] {name} has unknown launch material:{material.ItemGuid}\n"; continue; }
                    if (!seen.Add(material.ItemGuid)) result += $"[BlockMaster] {name} has duplicate launch material:{material.ItemGuid}\n";
                    if (payloadGuids.Contains(material.ItemGuid)) result += $"[BlockMaster] {name} launch material {material.ItemGuid} is also an orbital payload\n";
                    if (material.Count < 1) result += $"[BlockMaster] {name} launch material count must be >= 1\n";

                    var firstLevelStack = ItemStackLevelTableValidator.GetInitialStackLimit(MasterHolder.ItemMaster.Items, material.ItemGuid);
                    if (firstLevelStack != null && firstLevelStack < material.Count) result += $"[BlockMaster] {name} launch material {material.ItemGuid} count {material.Count} exceeds level-1 stack {firstLevelStack}\n";
                }
                return result;
            }

            string ValidateOutputCapacity(string name, RocketLaunchPadBlockParam param)
            {
                // 空の出力口に1便ぶんの成果が入りきらない積荷は、その台で永遠に出力満杯のまま打ち上がらない
                // A payload whose single launch outputs cannot fit empty output slots would stay output-full forever on this pad
                var result = "";
                foreach (var payload in payloads)
                {
                    // 数量の合算と切り上げはItemMasterの共通計算へ渡す
                    // Delegate quantity aggregation and ceiling division to ItemMaster
                    var counts = payload.ArrivalOutputs.Select(output => (MasterHolder.ItemMaster.GetItemId(output.ItemGuid), output.Count));
                    var stackCount = MasterHolder.ItemMaster.CountRequiredSlotsAtLevelOne(counts);
                    if (param.OutputSlotCount < stackCount) result += $"[BlockMaster] {name} outputSlotCount {param.OutputSlotCount} cannot hold one launch of payload {payload.PayloadItemGuid} ({stackCount} stacks at level-1 stack)\n";
                }
                return result;
            }

            #endregion
        }
    }
}
