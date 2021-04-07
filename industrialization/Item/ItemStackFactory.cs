namespace industrialization.Item
{
    public class ItemStackFactory
    {
        public static IItemStack[] CreateEmptyItemStacksArray(int amount)
        {
            return new NullItemStack[amount];
        }
    }
}