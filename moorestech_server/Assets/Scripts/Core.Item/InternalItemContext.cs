using Core.Item.Interface.Config;

namespace Core.Item
{
    internal class InternalItemContext
    {
        public InternalItemContext(ItemStackFactory itemStackFactory, IItemConfig itemConfig)
        {
            ItemStackFactory = itemStackFactory;
            ItemConfig = itemConfig;
        }
        
        public static ItemStackFactory ItemStackFactory { get; private set; }
        public static IItemConfig ItemConfig { get; private set; }
    }
}