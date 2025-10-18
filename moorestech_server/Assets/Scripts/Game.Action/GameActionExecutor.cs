using System;
using System.Collections.Generic;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Mooresmaster.Model.ChallengeActionModule;
using UnityEngine;

namespace Game.Action
{
    public class GameActionExecutor : IGameActionExecutor
    {
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        public GameActionExecutor(
            IGameUnlockStateDataController gameUnlockStateDataController,
            IPlayerInventoryDataStore playerInventoryDataStore)
        {
            _gameUnlockStateDataController = gameUnlockStateDataController;
            _playerInventoryDataStore = playerInventoryDataStore;
        }

        public void ExecuteUnlockActions(ChallengeActionElement[] actions, ActionExecutionContext context = default)
        {
            if (actions == null || actions.Length == 0) return;

            foreach (var action in actions)
            {
                switch (action.ChallengeActionType)
                {
                    case ChallengeActionElement.ChallengeActionTypeConst.unlockCraftRecipe:
                    case ChallengeActionElement.ChallengeActionTypeConst.unlockItemRecipeView:
                    case ChallengeActionElement.ChallengeActionTypeConst.unlockChallengeCategory:
                        ExecuteAction(action, context);
                        break;
                }
            }
        }

        public void ExecuteActions(ChallengeActionElement[] actions, ActionExecutionContext context = default)
        {
            if (actions == null || actions.Length == 0) return;
            
            foreach (var action in actions)
            {
                ExecuteAction(action, context);
            }
        }
        
        private void ExecuteAction(ChallengeActionElement action, ActionExecutionContext context)
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

                case ChallengeActionElement.ChallengeActionTypeConst.giveItem:
                    GiveItem();
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

            void GiveItem()
            {
                // アイテムを付与するプレイヤーIDのリスト取得
                // Get the list of player IDs to whom items will be granted
                var param = (GiveItemChallengeActionParam)action.ChallengeActionParam;
                var targetPlayerIds = ResolveTargetPlayers(param.DeliveryTarget);
                if (targetPlayerIds.Count == 0) return;

                // アイテム付与処理
                // Item granting process
                foreach (var playerId in targetPlayerIds)
                {
                    var inventoryData = _playerInventoryDataStore.GetInventoryData(playerId);
                    foreach (var reward in param.RewardItems)
                    {
                        var itemStack = ServerContext.ItemStackFactory.Create(reward.ItemGuid, reward.ItemCount);
                        inventoryData.MainOpenableInventory.InsertItem(itemStack);
                    }
                }
            }

            List<int> ResolveTargetPlayers(string deliveryTarget)
            {
                switch (deliveryTarget)
                {
                    case GiveItemChallengeActionParam.DeliveryTargetConst.allPlayers:
                        return _playerInventoryDataStore.GetAllPlayerId();
                    case GiveItemChallengeActionParam.DeliveryTargetConst.actionInvoker:
                        if (context.HasActionInvoker)
                        {
                            return new List<int> { context.ActionInvokerPlayerId!.Value };
                        }

                        Debug.LogError("[GameActionExecutor] giveItem actionInvoker deliveryTarget requires ActionInvokerPlayerId.");
                        return new List<int>();
                    default:
                        Debug.LogError($"[GameActionExecutor] Unknown deliveryTarget: {deliveryTarget}");
                        return new List<int>();
                }
            }
            
            #endregion
        }
    }
}
