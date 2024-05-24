using Core.Item.Interface.Config;

namespace Game.Challenge
{
    public class CreateItemTaskParam : IChallengeTaskParam
    {
        public const string TaskCompletionType = "createItem";

        public readonly int ItemId;

        public CreateItemTaskParam(int itemId)
        {
            ItemId = itemId;
        }

        public static IChallengeTaskParam Create(dynamic param, IItemConfig itemConfig)
        {
            string itemModId = param.itemModId;
            string itemName = param.itemName;

            var item = itemConfig.GetItemId(itemModId, itemName);

            return new CreateItemTaskParam(item);
        }
    }
}