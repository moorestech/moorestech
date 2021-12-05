using Core.Item.Implementation;
using Core.Item.Util;

namespace Core.Item
{
    public static class ItemStackFactory
    {
        public static IItemStack Create(int id, int amount)
        {
            if (id == ItemConst.NullItemId)
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