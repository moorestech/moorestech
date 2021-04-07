namespace industrialization.Item
{
    public interface IItemStack
    {
        int ID { get; }
        int Amount { get; }
        ItemProcessResult AddItem(IItemStack receiveItemStack);
        IItemStack SubItem(int subAmount);
    }
}