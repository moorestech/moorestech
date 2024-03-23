using Core.Item;

namespace Game.Block.BlockInventory
{
    /// <summary>
    ///     ベルトコンベアに乗っているアイテムを機械に入れたり、機械からベルトコンベアにアイテムを載せるなどの処理をするための共通インターフェース
    ///     ブロック同士でアイテムをやり取りしたいときに使う
    ///     TODO そのうちコンポーネント化する
    /// </summary>
    public interface IBlockInventory
    {
        public IItemStack InsertItem(IItemStack itemStack);
        public IItemStack GetItem(int slot);
        void SetItem(int slot, IItemStack itemStack);
        public int GetSlotSize();
    }
}