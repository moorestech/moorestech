using System.Collections.Generic;
using Core.Inventory;
using Game.Block.Blocks.Connector;
using Core.Item.Interface;
using Game.Block.Blocks.Service;
using Game.Block.Interface.Component;
using UnityEngine;

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

        public bool CanStore(IReadOnlyList<IItemStack> outputs)
        {
            // 出力口の写しへ順に詰めて余りを検査
            // Fill copied output slots in order and check for remainders
            var simulated = CopyOutputSlots();
            foreach (var output in outputs)
                if (0 < FillInOrder(simulated, output).Count) return false;
            return true;
        }

        public void Store(IReadOnlyList<IItemStack> outputs)
        {
            // 同じtickの直前に CanStore を通したので余りは出ない。出たら判定と書き込みの食い違いとして残す（機械の出力と同じ）
            // CanStore passed earlier in the same tick, so no remainder is expected; log a mismatch if one appears (as machine outputs do)
            var current = CopyOutputSlots();
            foreach (var output in outputs)
            {
                var remainder = FillInOrder(current, output);
                if (0 < remainder.Count) Debug.LogError($"発射台の出力口に成果が入りきりませんでした ItemId:{remainder.Id} Count:{remainder.Count}");
            }
            for (var i = 0; i < _layout.OutputSlots.Count; i++) _slots.SetItem(_layout.OutputSlots[i], current[i]);
        }

        public void PushToConnected()
        {
            // 機械と同じく毎tick接続先へ搬出
            // Push output contents to connected inventories each tick, like machines
            BlockOutputTransfer.Transfer(_slots, _layout.OutputSlots, _beltOutput);
        }

        private List<IItemStack> CopyOutputSlots()
        {
            var copy = new List<IItemStack>();
            foreach (var slot in _layout.OutputSlots) copy.Add(_slots.GetItem(slot));
            return copy;
        }

        // 同種スタックを埋めてから空枠を使い、別品目の場所を残す
        // Fill existing matching stacks before empty slots to preserve room for other items
        private static IItemStack FillInOrder(List<IItemStack> slots, IItemStack item)
        {
            var order = new List<int>();
            for (var i = 0; i < slots.Count; i++)
                if (slots[i].Id == item.Id) order.Add(i);
            for (var i = 0; i < slots.Count; i++)
                if (slots[i].Count == 0) order.Add(i);
            return InventoryInsertItem.InsertInOrder(item, slots, order);
        }
    }
}
