using Core.Item.Interface;

namespace Game.Context
{
    public static class ItemStackSaveJsonObjectExtension
    {
        public static IItemStack ToItem(this ItemStackSaveJsonObject itemStackSaveJsonObject)
        {
            return ServerContext.ItemStackFactory.Create(itemStackSaveJsonObject.ItemGuid, itemStackSaveJsonObject.Count);
        }
    }
}