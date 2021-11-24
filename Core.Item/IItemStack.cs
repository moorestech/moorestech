namespace Core.Item
{
    public interface IItemStack
    {
        int Id { get; }
        int Amount { get; }
        ItemProcessResult AddItem(IItemStack receiveItemStack);
        //TODO SubItemの引数をItemProcessResultに変更する
        IItemStack SubItem(int subAmount);
        bool IsAllowedToAdd(IItemStack item);
    }
}