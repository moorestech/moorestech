using Core.Item.Util;

namespace Core.Item.Implementation
{
    public class NullItemStack : IItemStack
    {
        public NullItemStack()
        {
        }

        public int Id => ItemConst.NullItemId;
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