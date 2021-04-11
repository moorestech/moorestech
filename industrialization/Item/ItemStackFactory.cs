namespace industrialization.Item
{
    public class ItemStackFactory
    {
        public static IItemStack[] CreateEmptyItemStacksArray(int amount)
        {
            return new NullItemStack[amount];
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