using Core.Master;
using Game.UnlockState;
using Mooresmaster.Model.ChallengeActionModule;

namespace Game.Action
{
    public class GameActionExecutor : IGameActionExecutor
    {
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;

        public GameActionExecutor(IGameUnlockStateDataController gameUnlockStateDataController)
        {
            _gameUnlockStateDataController = gameUnlockStateDataController;
        }

        public void ExecuteAction(ChallengeActionElement action)
        {
            if (action == null) return;

            switch (action.ChallengeActionType)
            {
                case ChallengeActionElement.ChallengeActionTypeConst.unlockCraftRecipe:
                    var unlockRecipeGuids = ((UnlockCraftRecipeChallengeActionParam)action.ChallengeActionParam).UnlockRecipeGuids;
                    foreach (var guid in unlockRecipeGuids)
                    {
                        _gameUnlockStateDataController.UnlockCraftRecipe(guid);
                    }
                    break;

                case ChallengeActionElement.ChallengeActionTypeConst.unlockItemRecipeView:
                    var itemGuids = ((UnlockItemRecipeViewChallengeActionParam)action.ChallengeActionParam).UnlockItemGuids;
                    foreach (var itemGuid in itemGuids)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
                        _gameUnlockStateDataController.UnlockItem(itemId);
                    }
                    break;

                case ChallengeActionElement.ChallengeActionTypeConst.unlockChallengeCategory:
                    var challenges = ((UnlockChallengeCategoryChallengeActionParam)action.ChallengeActionParam).UnlockChallengeCategoryGuids;
                    foreach (var guid in challenges)
                    {
                        _gameUnlockStateDataController.UnlockChallenge(guid);
                    }
                    break;
            }
        }
    }
}