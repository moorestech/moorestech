using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.OrbitalCargo;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // - 入れ口の資材と選んだ積荷を集計
    // - 揃った1便分を取り出して積載
    // - Count materials and the selected payload in the inputs
    // - Take and load one launch worth once ready
    internal class RocketLaunchPadCargoLoader
    {
        private readonly RocketLaunchPadSlotLayout _layout;
        private readonly OpenableInventoryItemDataStoreService _slots;
        private readonly RocketLaunchPadOutputSlots _outputSlots;
        private readonly IOrbitalCargoSelectionReader _cargoSelection;

        public RocketLaunchPadCargoLoader(RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots, RocketLaunchPadOutputSlots outputSlots, IOrbitalCargoSelectionReader cargoSelection)
        {
            _layout = layout;
            _slots = slots;
            _outputSlots = outputSlots;
            _cargoSelection = cargoSelection;
        }

        // 手動合図を待つ間は在庫を変えず、同じ判定で待ち理由を得る
        // Read the wait reason through the same check without consuming during the manual wait
        public RocketLaunchPadWaitReason GetLoadWaitReason() => CheckSelectedCargo(out _);

        public bool TryLoadNextCargo(out RocketCargo cargo, out RocketLaunchPadWaitReason reason)
        {
            reason = CheckSelectedCargo(out var payloadIndex);
            cargo = null;
            // silent-ok: 未選択・充足・出力待ちは正常な待機 / Sequenceが待ち理由をUI・操作応答へ伝える
            if (reason != RocketLaunchPadWaitReason.None) return false;

            // 判定が通った同じ操作内で1便分を取り込む
            // Consume one launch within the same operation that passed the check
            var materials = new List<IItemStack>();
            for (var i = 0; i < _layout.Materials.Count; i++) materials.Add(TakeRequirement(_layout.MaterialSlots[i], _layout.Materials[i]));
            var payload = TakeRequirement(_layout.PayloadSlot(payloadIndex), _layout.Payloads[payloadIndex]);
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

        // 選んだ積荷だけを見る。未選択を最優先で返し、成果なしの積荷は出力満杯でも積める（ADR 0106 1・2）
        // Look only at the selected payload; report unselected first, and output-free payloads load even when output is full (ADR 0106 1, 2)
        private RocketLaunchPadWaitReason CheckSelectedCargo(out int payloadIndex)
        {
            payloadIndex = 0;
            // silent-ok: 未選択は通常の待機 / プレイヤーは状態表示の「積荷を選んでいない」で気づく
            if (_cargoSelection.Current is not SelectedOrbitalCargo selected) return RocketLaunchPadWaitReason.CargoNotSelected;
            for (var i = 0; i < _layout.Materials.Count; i++)
                if (!HasRequirement(_layout.MaterialSlots[i], _layout.Materials[i])) return RocketLaunchPadWaitReason.MissingMaterials;

            payloadIndex = IndexOfPayload(selected.PayloadItemId);
            var payload = _layout.Payloads[payloadIndex];
            if (!HasRequirement(_layout.PayloadSlot(payloadIndex), payload)) return RocketLaunchPadWaitReason.MissingPayload;
            return _outputSlots.CanAcceptArrival(payload.ItemId, payload.Count) ? RocketLaunchPadWaitReason.None : RocketLaunchPadWaitReason.OutputFull;

            #region Internal

            bool HasRequirement(int slot, RocketLaunchSlotRequirement requirement)
            {
                var stack = _slots.GetItem(slot);
                return stack.Id == requirement.ItemId && requirement.Count <= stack.Count;
            }

            int IndexOfPayload(ItemId payloadItemId)
            {
                // 選択部品は積荷表にある種類しか選ばせないので必ず見つかる
                // The selector only allows kinds in the payload table, so the index always exists
                for (var i = 0; i < _layout.Payloads.Count; i++)
                    if (_layout.Payloads[i].ItemId == payloadItemId) return i;
                throw new InvalidOperationException($"選んだ積荷 {payloadItemId} が発射台の積荷表にありません");
            }

            #endregion
        }
    }
}
