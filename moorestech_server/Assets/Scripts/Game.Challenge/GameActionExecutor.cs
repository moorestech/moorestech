using Core.Master;
using Game.UnlockState;
using Mooresmaster.Model.ChallengeActionModule;

namespace Game.Challenge
{
    public class GameActionExecutor : IGameActionExecutor
    {
        private readonly IGameUnlockStateDataController _unlock;

        public GameActionExecutor(IGameUnlockStateDataController unlock)
        {
            _unlock = unlock;
        }

        public void ExecuteAction(ChallengeActionElement action)
        {
            switch (action.ChallengeActionType)
            {
                case ChallengeActionElement.ChallengeActionTypeConst.unlockCraftRecipe:
                {
                    var unlockRecipeGuids = ((UnlockCraftRecipeChallengeActionParam)action.ChallengeActionParam).UnlockRecipeGuids;
                    foreach (var guid in unlockRecipeGuids)
                    {
                        _unlock.UnlockCraftRecipe(guid);
                    }
                    break;
                }
                case ChallengeActionElement.ChallengeActionTypeConst.unlockItemRecipeView:
                {
                    var itemGuids = ((UnlockItemRecipeViewChallengeActionParam)action.ChallengeActionParam).UnlockItemGuids;
                    foreach (var itemGuid in itemGuids)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
                        _unlock.UnlockItem(itemId);
                    }
                    break;
                }
                case ChallengeActionElement.ChallengeActionTypeConst.unlockChallengeCategory:
                {
                    var challenges = ((UnlockChallengeCategoryChallengeActionParam)action.ChallengeActionParam).UnlockChallengeCategoryGuids;
                    foreach (var guid in challenges)
                    {
                        _unlock.UnlockChallenge(guid);
                    }
                    break;
                }
            }
        }
    }
}
