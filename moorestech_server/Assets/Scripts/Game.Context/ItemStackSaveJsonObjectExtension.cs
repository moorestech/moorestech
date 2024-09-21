using Core.Item.Interface;

namespace Game.Context
{
    public static class ItemStackSaveJsonObjectExtension
    {
        public static IItemStack ToItemStack(this ItemStackSaveJsonObject itemStackSaveJsonObject)
        {
            return ServerContext.ItemStackFactory.Create(itemStackSaveJsonObject.ItemGuid, itemStackSaveJsonObject.Count);
        }
    }
}