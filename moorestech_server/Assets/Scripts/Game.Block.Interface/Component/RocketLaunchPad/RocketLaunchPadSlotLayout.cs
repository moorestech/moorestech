using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Interface.Component
{
    // 発射台のスロット並び。資材→積荷→出力の順で、入れ口は1アイテムにつき1スロット。サーバーと表示が同じ並びを使う
    // Slot order of a launch pad: materials, payloads, outputs, one input slot per item; the server and the display share it
    public class RocketLaunchPadSlotLayout
    {
        public readonly IReadOnlyList<RocketLaunchSlotRequirement> Materials;
        public readonly IReadOnlyList<RocketLaunchSlotRequirement> Payloads;
        public readonly IReadOnlyList<int> MaterialSlots;
        public readonly IReadOnlyList<int> PayloadSlots;
        public readonly IReadOnlyList<int> OutputSlots;
        public readonly int OutputSlotStart;
        public readonly int OutputSlotCount;
        public int TotalSlotCount => OutputSlotStart + OutputSlotCount;

        public RocketLaunchPadSlotLayout(RocketLaunchPadBlockParam param)
        {
            // 資材はブロックパラメータ、積荷は積荷表の並びのまま並べる
            // Materials follow the block parameter and payloads follow the payload table order
            Materials = param.LaunchMaterials.Select(material => new RocketLaunchSlotRequirement(MasterHolder.ItemMaster.GetItemId(material.ItemGuid), material.Count)).ToList();
            Payloads = MasterHolder.ItemMaster.Items.OrbitalPayloads.Select(payload => new RocketLaunchSlotRequirement(MasterHolder.ItemMaster.GetItemId(payload.PayloadItemGuid), payload.CountPerLaunch)).ToList();
            OutputSlotStart = Materials.Count + Payloads.Count;
            OutputSlotCount = param.OutputSlotCount;

            // 群ごとの絶対添字。表示側はこれをそのまま使い、並びを計算し直さない
            // Absolute indices per group; the display uses them as-is and never recomputes the order
            MaterialSlots = Enumerable.Range(0, Materials.Count).ToList();
            PayloadSlots = Enumerable.Range(Materials.Count, Payloads.Count).ToList();
            OutputSlots = Enumerable.Range(OutputSlotStart, OutputSlotCount).ToList();
        }

        public int PayloadSlot(int payloadIndex)
        {
            return PayloadSlots[payloadIndex];
        }

        public bool IsOutputSlot(int slot)
        {
            return OutputSlotStart <= slot;
        }

        public RocketLaunchSlotRequirement RequirementAt(int inputSlot)
        {
            return inputSlot < Materials.Count ? Materials[inputSlot] : Payloads[inputSlot - Materials.Count];
        }

        // 資材と積荷が同じアイテムにならないことはマスタ検証が保証する
        // Master validation guarantees no item is both a material and a payload
        public bool TryGetInputSlot(ItemId itemId, out int slot)
        {
            for (var i = 0; i < OutputSlotStart; i++)
            {
                if (RequirementAt(i).ItemId != itemId) continue;
                slot = i;
                return true;
            }
            slot = 0;
            return false;
        }

        // 積荷表に載る積荷の1便ぶんの要求を引く。資材は対象にしない
        // Looks up one launch worth of a listed payload; materials are not considered
        public bool TryGetPayloadRequirement(ItemId itemId, out RocketLaunchSlotRequirement requirement)
        {
            foreach (var payload in Payloads)
            {
                if (payload.ItemId != itemId) continue;
                requirement = payload;
                return true;
            }
            requirement = default;
            return false;
        }
    }

    public readonly struct RocketLaunchSlotRequirement
    {
        public readonly ItemId ItemId;
        public readonly int Count;

        public RocketLaunchSlotRequirement(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
