using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Interface.ComponentAttribute;

namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     ベルトコンベアに乗っているアイテムを機械に入れたり、機械からベルトコンベアにアイテムを載せるなどの処理をするための共通インターフェース
    ///     ブロック同士でアイテムをやり取りしたいときに使う
    /// </summary>
    [DisallowMultiple]
    public interface IBlockInventory : IBlockComponent
    {
        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context);
        public bool InsertionCheck(List<IItemStack> itemStacks);

        public IItemStack GetItem(int slot);
        void SetItem(int slot, IItemStack itemStack);
        public int GetSlotSize();
    }
}