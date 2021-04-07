namespace industrialization.Item
{
    public class NullItemStack : IItemStack
    {
        public int ID => -1;
        public int Amount => 0;

        public ItemProcessResult AddItem(IItemStack receiveItemStack)
        {
            return new ItemProcessResult(receiveItemStack,new NullItemStack());
        }

        public IItemStack SubItem(int subAmount)
        {
            return this;
        }
    }
}