namespace industrialization.Item
{
    public class ItemStackFactory
    {
        public static IItemStack[] CreateEmptyItemStacksArray(int amount)
        {
            var itemArray = new NullItemStack[amount];
            return itemArray;
        }
    }
}