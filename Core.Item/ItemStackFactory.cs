using Core.Item.Implementation;

namespace Core.Item
{
    public static class ItemStackFactory
    {
        public static IItemStack Create(int id, int amount)
        {
            if (id < 0)
            {
                return CreatEmpty();
            }
            if (amount < 1)
            {
                return CreatEmpty();
            }

            return new ItemStack(id, amount);
        }
        public static IItemStack CreatEmpty()
        {
            return new NullItemStack();
        }
    }
}