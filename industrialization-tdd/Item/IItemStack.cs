namespace industrialization.Item
{
    public interface IItemStack
    {
        int ID { get; }
        int Amount { get; }
        ItemProcessResult addItem(IItemStack receiveItemStack);
        IItemStack subItem(int subAmount);
    }
}