using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.OrbitalCargo;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 発射台の積荷入れ口。積荷表の全種類を運び、選び直しで外れた種類の入れ口を返す
    // The pad's payload inlets; carries every kind in the payload table and refunds inlets of kinds dropped by a reselection
    internal class RocketLaunchPadCargoInletSlots : IOrbitalCargoInletSlots
    {
        public IReadOnlyCollection<ItemId> CarriedPayloadItemIds { get; }

        private readonly RocketLaunchPadSlotLayout _layout;
        private readonly OpenableInventoryItemDataStoreService _slots;

        public RocketLaunchPadCargoInletSlots(RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            _layout = layout;
            _slots = slots;
            CarriedPayloadItemIds = layout.Payloads.Select(payload => payload.ItemId).ToList();
        }

        public void RefundUnselectedPayloads(SelectedOrbitalCargo selection, IOpenableInventory refundInventory)
        {
            for (var i = 0; i < _layout.Payloads.Count; i++)
            {
                var slot = _layout.PayloadSlot(i);
                var stack = _slots.GetItem(slot);
                // silent-ok: 空の入れ口と選んだ種類の入れ口は返さない / 返却なしが正しい結果
                if (stack.Count == 0 || selection.IsSelected(_layout.Payloads[i].ItemId)) continue;
                // 入らない分は元の入れ口に残る。積載は選んだ種類だけを取るので詰まらない
                // What does not fit stays in its own inlet; loading takes only the selected kind, so nothing jams
                _slots.SetItem(slot, refundInventory.InsertItem(stack));
            }
        }
    }
}
