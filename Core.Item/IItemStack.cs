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
        IItemStack Clone();
    }
}