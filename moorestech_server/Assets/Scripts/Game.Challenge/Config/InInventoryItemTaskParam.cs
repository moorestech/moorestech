using Core.Item.Interface.Config;
using Game.Context;

namespace Game.Challenge
{
    public class InInventoryItemTaskParam : IChallengeTaskParam
    {
        public const string TaskCompletionType = "inInventoryItem";

        public readonly int ItemId;
        public readonly int Count;

        public InInventoryItemTaskParam(int itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        public static IChallengeTaskParam Create(dynamic param)
        {
            string itemModId = param.itemModId;
            string itemName = param.itemName;

            var item = ServerContext.ItemConfig.GetItemId(itemModId, itemName);

            return new InInventoryItemTaskParam(item, param.count);
        }
    }
}