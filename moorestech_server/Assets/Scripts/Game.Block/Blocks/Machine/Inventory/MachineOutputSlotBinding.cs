using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;

namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     出力スロットの許可アイテム集合を保持し束縛判定する。清浄室のようにチップ等でファミリー外を許す必要がある機械は
    ///     呼び出し側が許可集合を組み立てて渡す（2026-08-30裁定D3）
    ///     Holds the allowed-item set per output slot and decides the binding. Machines that must widen beyond the level
    ///     family (e.g. clean-room chips) have the caller assemble the allowed set (2026-08-30 ruling D3)
    /// </summary>
    internal class MachineOutputSlotBinding
    {
        private IReadOnlyList<IReadOnlyCollection<ItemId>> _allowedItemsPerSlot = System.Array.Empty<IReadOnlyCollection<ItemId>>();

        public void SetBoundOutputs(IReadOnlyList<IReadOnlyCollection<ItemId>> allowedItemsPerSlot)
        {
            _allowedItemsPerSlot = allowedItemsPerSlot ?? System.Array.Empty<IReadOnlyCollection<ItemId>>();
        }

        // 実現出力realizedOutputIndexが積まれるスロット番号。束縛先が無ければ-1
        // Slot realized output realizedOutputIndex lands in; -1 when no binding exists
        public int ResolveSlot(int realizedOutputIndex)
        {
            if (_allowedItemsPerSlot.Count == 0) return -1;
            return realizedOutputIndex % _allowedItemsPerSlot.Count;
        }

        // 空アイテムは取り出しなのでどのスロットでも許す
        // An empty stack means a take-out, so it is allowed on any slot
        public bool IsAllowedToPlace(int localSlot, IItemStack itemStack)
        {
            if (itemStack.Id == ItemMaster.EmptyItemId) return true;
            if (localSlot < 0 || _allowedItemsPerSlot.Count <= localSlot) return false;
            return _allowedItemsPerSlot[localSlot].Contains(itemStack.Id);
        }
    }
}
