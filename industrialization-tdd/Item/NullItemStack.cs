namespace industrialization.Item
{
    public class NullItemStack : IItemStack
    {
        public int ID => -1;
        public int Amount => 0;

        public ItemProcessResult addItem(IItemStack receiveItemStack)
        {
            return new ItemProcessResult(receiveItemStack,new NullItemStack());
        }

        public IItemStack subItem(int subAmount)
        {
            return this;
        }
    }
}