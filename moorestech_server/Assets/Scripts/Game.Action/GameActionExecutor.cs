using System.Collections.Generic;
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
        public void ExecuteUnlockActions(ChallengeActionElement[] actions)
        {
            if (actions == null || actions.Length == 0) return;

            foreach (var action in actions)
            {
                switch (action.ChallengeActionType)
                {
                    case ChallengeActionElement.ChallengeActionTypeConst.unlockCraftRecipe:
                    case ChallengeActionElement.ChallengeActionTypeConst.unlockItemRecipeView:
                    case ChallengeActionElement.ChallengeActionTypeConst.unlockChallengeCategory:
                        ExecuteAction(action);
                        break;
                }
            }
        }
        public void ExecuteActions(ChallengeActionElement[] actions)
        {
            if (actions == null || actions.Length == 0) return;
            
            foreach (var action in actions)
            {
                ExecuteAction(action);
            }
        }
        
        private void ExecuteAction(ChallengeActionElement action)
        {
            if (action == null) return;
            switch (action.ChallengeActionType)
            {
                case ChallengeActionElement.ChallengeActionTypeConst.unlockCraftRecipe:
                    UnlockCraftRecipe();
                    break;

                case ChallengeActionElement.ChallengeActionTypeConst.unlockItemRecipeView:
                    UnlockItemRecipeView();
                    break;

                case ChallengeActionElement.ChallengeActionTypeConst.unlockChallengeCategory:
                    UnlockChallengeCategory();
                    break;
            }
            
            #region Internal
            
            void UnlockCraftRecipe()
            {
                var unlockRecipeGuids = ((UnlockCraftRecipeChallengeActionParam)action.ChallengeActionParam).UnlockRecipeGuids;
                foreach (var guid in unlockRecipeGuids)
                {
                    _gameUnlockStateDataController.UnlockCraftRecipe(guid);
                }
            }
            
            void UnlockItemRecipeView()
            {
                var itemGuids = ((UnlockItemRecipeViewChallengeActionParam)action.ChallengeActionParam).UnlockItemGuids;
                foreach (var itemGuid in itemGuids)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
                    _gameUnlockStateDataController.UnlockItem(itemId);
                }
            }
            
            void UnlockChallengeCategory()
            {
                var challenges = ((UnlockChallengeCategoryChallengeActionParam)action.ChallengeActionParam).UnlockChallengeCategoryGuids;
                foreach (var guid in challenges)
                {
                    _gameUnlockStateDataController.UnlockChallenge(guid);
                }
            }
            
            #endregion
        }
    }
}
