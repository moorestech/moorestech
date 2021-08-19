namespace industrialization.Core.Item
{
    public class NullItemStack : IItemStack
    {
        public const int NullItemId = -1;
        public int Id => -1;
        public int Amount => 0;

        public ItemProcessResult AddItem(IItemStack receiveItemStack)
        {
            return new ItemProcessResult(receiveItemStack,new NullItemStack());
        }

        public IItemStack SubItem(int subAmount)
        {
            return this;
        }

        public bool IsAllowedToAdd(IItemStack item)
        {
            return true;
        }
        public override bool Equals(object? obj)
        {
            if (typeof(NullItemStack) != obj.GetType()) return false;
            return ((NullItemStack) obj).Id == Id && ((NullItemStack) obj).Amount == Amount;
        }
        public override string ToString()
        {
            return $"ID:{Id} Amount:{Amount}";
        }
    }
}