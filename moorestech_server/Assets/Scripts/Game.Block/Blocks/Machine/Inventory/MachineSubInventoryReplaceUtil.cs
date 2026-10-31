using System;
using Core.Item.Interface;
using Core.Master;

namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     統合インベントリの ReplaceItem 本体。空きか同じIDならスロット上限の空きまで足して残りを返し、別IDなら入れ替える
    ///     Body of the unified inventory's ReplaceItem: fill an empty or same-ID slot up to its limit and return the rest, or swap a different ID
    /// </summary>
    internal static class MachineSubInventoryReplaceUtil
    {
        // 空きでも同じIDでもなければ入れ替え（モジュールの CheckPlacement と Replace が共有する判定）
        // A swap when the slot is neither empty nor the same ID (shared by the module CheckPlacement and Replace)
        public static bool IsSwap(IItemStack current, IItemStack incoming)
        {
            return current.Id != ItemMaster.EmptyItemId && current.Id != incoming.Id;
        }

        public static IItemStack Replace(IVanillaMachineSubInventory subInventory, int localSlot, IItemStack itemStack)
        {
            var current = subInventory.Items[localSlot];

            // 別IDは入れ替える（上限超えの入れ替えは CheckPlacement が先に拒否している）
            // Swap a different ID (CheckPlacement has already refused a swap above the limit)
            if (IsSwap(current, itemStack))
            {
                subInventory.SetItem(localSlot, itemStack);
                return current;
            }

            // スロット上限の空きまでだけ足し、入らなかった分とアイテム最大スタックのあふれを合わせて返す
            // Add only up to the slot limit's room and return what did not fit together with any max-stack overflow
            var room = Math.Max(0, subInventory.GetSlotStackLimit() - current.Count);
            var overflowCount = Math.Max(0, itemStack.Count - room);
            var accepted = itemStack.SubItem(overflowCount);
            var overflow = itemStack.SubItem(itemStack.Count - overflowCount);
            var result = current.AddItem(accepted);
            subInventory.SetItem(localSlot, result.ProcessResultItemStack);
            return result.RemainderItemStack.AddItem(overflow).ProcessResultItemStack;
        }
    }
}
