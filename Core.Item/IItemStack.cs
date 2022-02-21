using Core.Item.Util;

namespace Core.Item
{
    public interface IItemStack
    {
        int Id { get; }
        int Count { get; }
        ItemProcessResult AddItem(IItemStack receiveItemStack);
        IItemStack SubItem(int subCount);
        bool IsAllowedToAdd(IItemStack item);
        /// <summary>
        /// アイテムを追加できるが、あまりが発生する場合trueを返します
        /// あまりが発生しない場合や、IDが違うことで追加ができない場合はfalseを返します
        /// </summary>
        /// <returns>追加はできるがあまりがある時にtrue</returns>
        bool IsAllowedToAddButRemain(IItemStack item);
        IItemStack Clone();
    }
}