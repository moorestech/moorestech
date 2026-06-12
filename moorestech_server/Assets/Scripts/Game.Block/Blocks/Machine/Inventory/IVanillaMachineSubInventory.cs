using System.Collections.Generic;
using Core.Item.Interface;

namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     機械の統合インベントリ（VanillaMachineBlockInventoryComponent）が各スロットレンジへ委譲するためのインターフェース
    ///     Interface the machine's unified inventory (VanillaMachineBlockInventoryComponent) uses to delegate to each slot range
    /// </summary>
    public interface IVanillaMachineSubInventory
    {
        IReadOnlyList<IItemStack> Items { get; }
        void SetItem(int slot, IItemStack itemStack);
    }
}
