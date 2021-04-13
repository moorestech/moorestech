using System.Linq;

namespace industrialization.Item
{
    public class ItemStackFactory
    {
        public static IItemStack[] CreateEmptyItemStacksArray(int amount)
        {
            var a = new IItemStack[amount];
            for (var i = 0; i < a.Length; i++)
            {
                a[i] = new NullItemStack();
            }
            return a;
        }

        public static IItemStack NewItemStack(int id, int amount)
        {
            if (id < 0)
            {
                return new NullItemStack();
            }
            if (amount < 1)
            {
                return new NullItemStack();
            }

            return new ItemStack(id, amount);
        }
    }
}