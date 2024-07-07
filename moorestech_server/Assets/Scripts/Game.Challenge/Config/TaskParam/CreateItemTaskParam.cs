using Game.Context;

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
        
        public static IChallengeTaskParam Create(dynamic param)
        {
            string itemModId = param.itemModId;
            string itemName = param.itemName;
            
            var item = ServerContext.ItemConfig.GetItemId(itemModId, itemName);
            
            return new CreateItemTaskParam(item);
        }
    }
}