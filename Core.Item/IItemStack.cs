using Core.Item.Util;

namespace Core.Item
{
    public interface IItemStack
    {
        int Id { get; }
        int Amount { get; }
        ItemProcessResult AddItem(IItemStack receiveItemStack);
        IItemStack SubItem(int subAmount);
        bool IsAllowedToAdd(IItemStack item);
        IItemStack Clone();
    }
}