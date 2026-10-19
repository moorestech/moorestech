using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Interface.ComponentAttribute;

namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     ベルトコンベアに乗っているアイテムを機械に入れたり、機械からベルトコンベアにアイテムを載せるなどの処理をするための共通インターフェース
    ///     ブロック同士でアイテムをやり取りしたいときに使う。搬入だけの契約で、スロットの読み書きは持たない(スロットはIOpenableInventory、撤去時の返却はIGetRefundItemsInfo)
    ///     The transfer contract between blocks: insertion only, with no slot access (slots live on IOpenableInventory, removal refunds on IGetRefundItemsInfo)
    /// </summary>
    [DisallowMultiple]
    public interface IBlockInventory : IBlockComponent
    {
        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context);
        public bool InsertionCheck(List<IItemStack> itemStacks);
    }
}