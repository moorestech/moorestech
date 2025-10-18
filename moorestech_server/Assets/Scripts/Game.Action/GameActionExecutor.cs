using System;
using System.Collections.Generic;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Mooresmaster.Model.GameActionModule;
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

        public void ExecuteUnlockActions(GameActionElement[] actions, ActionExecutionContext context = default)
        {
            if (actions == null || actions.Length == 0) return;

            foreach (var action in actions)
            {
                switch (action.GameActionType)
                {
                    case GameActionElement.GameActionTypeConst.unlockCraftRecipe:
                    case GameActionElement.GameActionTypeConst.unlockItemRecipeView:
                    case GameActionElement.GameActionTypeConst.unlockChallengeCategory:
                        ExecuteAction(action, context);
                        break;
                }
            }
        }

        public void ExecuteActions(GameActionElement[] actions, ActionExecutionContext context = default)
        {
            if (actions == null || actions.Length == 0) return;
            
            foreach (var action in actions)
            {
                ExecuteAction(action, context);
            }
        }
        
        private void ExecuteAction(GameActionElement action, ActionExecutionContext context)
        {
            if (action == null) return;
            switch (action.GameActionType)
            {
                case GameActionElement.GameActionTypeConst.unlockCraftRecipe:
                    UnlockCraftRecipe();
                    break;

                case GameActionElement.GameActionTypeConst.unlockItemRecipeView:
                    UnlockItemRecipeView();
                    break;

                case GameActionElement.GameActionTypeConst.unlockChallengeCategory:
                    UnlockChallengeCategory();
                    break;

                case GameActionElement.GameActionTypeConst.giveItem:
                    GiveItem();
                    break;
            }
            
            #region Internal
            
            void UnlockCraftRecipe()
            {
                var unlockRecipeGuids = ((UnlockCraftRecipeGameActionParam)action.GameActionParam).UnlockRecipeGuids;
                foreach (var guid in unlockRecipeGuids)
                {
                    _gameUnlockStateDataController.UnlockCraftRecipe(guid);
                }
            }
            
            void UnlockItemRecipeView()
            {
                var itemGuids = ((UnlockItemRecipeViewGameActionParam)action.GameActionParam).UnlockItemGuids;
                foreach (var itemGuid in itemGuids)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
                    _gameUnlockStateDataController.UnlockItem(itemId);
                }
            }
            
            void UnlockChallengeCategory()
            {
                var challenges = ((UnlockChallengeCategoryGameActionParam)action.GameActionParam).UnlockChallengeCategoryGuids;
                foreach (var guid in challenges)
                {
                    _gameUnlockStateDataController.UnlockChallenge(guid);
                }
            }

            void GiveItem()
            {
                // アイテムを付与するプレイヤーIDのリスト取得
                // Get the list of player IDs to whom items will be granted
                var param = (GiveItemGameActionParam)action.GameActionParam;
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
                    case GiveItemGameActionParam.DeliveryTargetConst.allPlayers:
                        return _playerInventoryDataStore.GetAllPlayerId();
                    case GiveItemGameActionParam.DeliveryTargetConst.actionInvoker:
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
