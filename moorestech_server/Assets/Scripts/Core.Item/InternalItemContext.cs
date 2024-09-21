namespace Core.Item
{
    internal class InternalItemContext
    {
        public static ItemStackFactory ItemStackFactory { get; private set; }
        
        public InternalItemContext(ItemStackFactory itemStackFactory)
        {
            ItemStackFactory = itemStackFactory;
        }
    }
}