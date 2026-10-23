using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Blocks.Connector;
using Game.Block.Interface.Component;
using UnityEngine;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 出力口へ成果を入れ、中身を接続先へ流す。入りきるかは機械の出力と同じく書き込まずに試す
    // Stores outputs into the output slots and pushes them onward; the fit check simulates without writing, like machine outputs
    public class RocketLaunchPadOutputSlots
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
            // 出力口の写しへ順に詰め、余りが出たら入りきらない
            // Fill a copy of the output slots in order; any remainder means they do not fit
            var simulated = CopyOutputSlots();
            foreach (var output in outputs)
                if (FillInOrder(simulated, output).Count > 0) return false;
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
                if (remainder.Count > 0) Debug.LogError($"発射台の出力口に成果が入りきりませんでした ItemId:{remainder.Id} Count:{remainder.Count}");
            }
            for (var i = 0; i < _layout.OutputSlots.Count; i++) _slots.SetItem(_layout.OutputSlots[i], current[i]);
        }

        public void PushToConnected()
        {
            // 出力口の中身は機械の出力と同じく毎tick接続先へ流す
            // Push output slot contents to connected inventories every tick, as machine outputs do
            foreach (var slot in _layout.OutputSlots) _slots.SetItem(slot, _beltOutput.InsertItem(_slots.GetItem(slot)));
        }

        private List<IItemStack> CopyOutputSlots()
        {
            var copy = new List<IItemStack>();
            foreach (var slot in _layout.OutputSlots) copy.Add(_slots.GetItem(slot));
            return copy;
        }

        // 写しのスロットへ先頭から詰め、余りを返す
        // Fills the copied slots from the first one and returns the remainder
        private static IItemStack FillInOrder(List<IItemStack> slots, IItemStack item)
        {
            var rest = item;
            for (var i = 0; i < slots.Count && rest.Count > 0; i++)
            {
                var result = slots[i].AddItem(rest);
                slots[i] = result.ProcessResultItemStack;
                rest = result.RemainderItemStack;
            }
            return rest;
        }
    }
}
