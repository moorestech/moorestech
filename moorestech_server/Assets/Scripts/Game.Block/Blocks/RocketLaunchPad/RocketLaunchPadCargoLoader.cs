using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Block.Interface.State;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // - 入れ口の資材・積荷を集計
    // - 選択した1便分を取り出して積載
    // - Count materials and payloads in inputs
    // - Take and load the selected launch worth
    internal class RocketLaunchPadCargoLoader
    {
        public int NextPayloadIndex { get; private set; }

        private readonly RocketLaunchPadSlotLayout _layout;
        private readonly OpenableInventoryItemDataStoreService _slots;
        private readonly RocketLaunchPadOutputSlots _outputSlots;

        public RocketLaunchPadCargoLoader(RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots, RocketLaunchPadOutputSlots outputSlots)
        {
            _layout = layout;
            _slots = slots;
            _outputSlots = outputSlots;
        }

        public void SetNextPayloadIndex(int nextPayloadIndex)
        {
            // 積荷表の行数がセーブ時から変わっても、負の値でも範囲内に収める（表が空でないことはマスタ検証が保証する）
            // Keep it in range for a changed table size or a negative value (master validation guarantees a non-empty table)
            var count = _layout.Payloads.Count;
            NextPayloadIndex = (nextPayloadIndex % count + count) % count;
        }

        // 手動合図を待つ間は在庫を変えず、同じ候補選択で待ち理由を得る
        // Read the wait reason through the same selection without consuming during the manual wait
        public RocketLaunchPadWaitReason GetLoadWaitReason() => SelectNextCargo(out _);

        public bool TryLoadNextCargo(out RocketCargo cargo, out RocketLaunchPadWaitReason reason)
        {
            reason = SelectNextCargo(out var payloadIndex);
            cargo = null;
            // silent-ok: 充足・出力待ちは正常な待機 / Sequenceが待ち理由をUI・操作応答へ伝える
            if (reason != RocketLaunchPadWaitReason.None) return false;

            // 選択が成功した同じ操作内で1便分を取り込み、次の巡回位置を進める
            // Consume one launch in the successful selection operation and advance the round-robin position
            var materials = new List<IItemStack>();
            for (var i = 0; i < _layout.Materials.Count; i++) materials.Add(TakeRequirement(_layout.MaterialSlots[i], _layout.Materials[i]));
            var payload = TakeRequirement(_layout.PayloadSlot(payloadIndex), _layout.Payloads[payloadIndex]);
            NextPayloadIndex = (payloadIndex + 1) % _layout.Payloads.Count;
            cargo = new RocketCargo(payload, materials);
            return true;

            #region Internal

            IItemStack TakeRequirement(int slot, RocketLaunchSlotRequirement requirement)
            {
                _slots.SetItem(slot, _slots.GetItem(slot).SubItem(requirement.Count));
                return ServerContext.ItemStackFactory.Create(requirement.ItemId, requirement.Count);
            }

            #endregion
        }

        // 充足した候補を巡回し、成果なしの積荷も出力満杯時に選べるようにする
        // Scan ready candidates in round-robin order, allowing output-free payloads even when full
        private RocketLaunchPadWaitReason SelectNextCargo(out int payloadIndex)
        {
            payloadIndex = 0;
            for (var i = 0; i < _layout.Materials.Count; i++)
                if (!HasRequirement(_layout.MaterialSlots[i], _layout.Materials[i])) return RocketLaunchPadWaitReason.MissingMaterials;

            var anyReady = false;
            for (var offset = 0; offset < _layout.Payloads.Count; offset++)
            {
                var index = (NextPayloadIndex + offset) % _layout.Payloads.Count;
                var payload = _layout.Payloads[index];
                if (!HasRequirement(_layout.PayloadSlot(index), payload)) continue;
                anyReady = true;
                if (!_outputSlots.CanAcceptArrival(payload.ItemId, payload.Count)) continue;
                payloadIndex = index;
                return RocketLaunchPadWaitReason.None;
            }
            return anyReady ? RocketLaunchPadWaitReason.OutputFull : RocketLaunchPadWaitReason.MissingPayload;

            #region Internal

            bool HasRequirement(int slot, RocketLaunchSlotRequirement requirement)
            {
                var stack = _slots.GetItem(slot);
                return stack.Id == requirement.ItemId && requirement.Count <= stack.Count;
            }

            #endregion
        }
    }
}
