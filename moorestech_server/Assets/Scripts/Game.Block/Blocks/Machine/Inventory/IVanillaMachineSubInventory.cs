using System.Collections.Generic;
using Core.Item.Interface;

namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     統合インベントリが各スロットレンジへ委譲するためのインターフェース
    ///     Interface the unified inventory uses to delegate to each slot range
    /// </summary>
    public interface IVanillaMachineSubInventory
    {
        IReadOnlyList<IItemStack> Items { get; }
        void SetItem(int slot, IItemStack itemStack);
    }
}
