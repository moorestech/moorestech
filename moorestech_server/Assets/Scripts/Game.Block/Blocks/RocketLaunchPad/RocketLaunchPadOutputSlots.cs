using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.Connector;
using Core.Item.Interface;
using Game.Block.Blocks.Service;
using Game.Block.Interface.Component;
using Game.Orbit;
using Game.Block.Interface.State;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 出力口へ成果を入れ、中身を接続先へ流す。入りきるかは機械の出力と同じく書き込まずに試す
    // Stores outputs into the output slots and pushes them onward; the fit check simulates without writing, like machine outputs
    internal class RocketLaunchPadOutputSlots
    {
        private readonly RocketLaunchPadSlotLayout _layout;
        private readonly OpenableInventoryItemDataStoreService _slots;
        private readonly IBlockInventoryInserter _beltOutput;

        public RocketLaunchPadOutputSlots(RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots, IBlockInventoryInserter beltOutput)
        {
            _layout = layout;
            _slots = slots;
            _beltOutput = beltOutput;
        }

        public bool CanStore(IReadOnlyList<IItemStack> outputs) => TrySimulate(outputs, out _);

        public bool TryCompleteArrival(ItemId itemId, int count, IOrbitalArrivalService arrivalService, out RocketLaunchPadWaitReason denyReason)
        {
            // 到着受理後に模擬配置を確定
            // Determine placement before notification and commit that placement after acceptance
            if (!TrySimulate(arrivalService.PreviewArrivalOutputs(itemId, count), out var simulated))
            {
                denyReason = RocketLaunchPadWaitReason.OutputFull;
                return false; // silent-ok: 出口待ちは通常の待機 / 呼出側がUIの待ち理由へ反映する
            }
            arrivalService.Arrive(itemId, count);
            for (var i = 0; i < _layout.OutputSlots.Count; i++) _slots.SetItem(_layout.OutputSlots[i], simulated[i]);
            denyReason = RocketLaunchPadWaitReason.None;
            return true;
        }

        private bool TrySimulate(IReadOnlyList<IItemStack> outputs, out List<IItemStack> simulated)
        {
            // 建造前と到着直前に同じ配置規則を使う
            // Use the same placement rules before construction and arrival
            simulated = CopyOutputSlots();
            foreach (var output in outputs)
                if (0 < FillInOrder(simulated, output).Count) return false;
            return true;

            #region Internal
            List<IItemStack> CopyOutputSlots()
            {
                var copy = new List<IItemStack>();
                foreach (var slot in _layout.OutputSlots) copy.Add(_slots.GetItem(slot));
                return copy;
            }

            // 同種枠を先に埋め別品目の空枠を残す
            // Fill existing matching stacks before empty slots to preserve room for other items
            IItemStack FillInOrder(List<IItemStack> slots, IItemStack item)
            {
                var order = new List<int>();
                for (var i = 0; i < slots.Count; i++)
                    if (slots[i].Id == item.Id) order.Add(i);
                for (var i = 0; i < slots.Count; i++)
                    if (slots[i].Count == 0) order.Add(i);
                return InventoryInsertItem.InsertInOrder(item, slots, order);
            }
            #endregion
        }

        public void PushToConnected()
        {
            // 機械と同じく毎tick接続先へ搬出
            // Push output contents to connected inventories each tick, like machines
            BlockOutputTransfer.Transfer(_slots, _layout.OutputSlots, _beltOutput);
        }

    }
}
