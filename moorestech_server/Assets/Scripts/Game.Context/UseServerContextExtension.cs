using Core.Item.Interface;

namespace Game.Context
{
    public static class UseServerContextExtension
    {
        public static IItemStack ToItem(this ItemStackJsonObject itemStackJsonObject)
        {
            return ServerContext.ItemStackFactory.Create(itemStackJsonObject.ItemHash, itemStackJsonObject.Count);
        }
    }
}