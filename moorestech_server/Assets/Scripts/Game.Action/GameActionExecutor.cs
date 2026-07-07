using System;
using System.Collections.Generic;
using Core.Item.Interface;
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
        private readonly IItemStackLevelUnlocker _itemStackLevelUnlocker;
        private readonly IPlayerInventorySlotLevelDataStore _playerInventorySlotLevelDataStore;

        public GameActionExecutor(
            IGameUnlockStateDataController gameUnlockStateDataController,
            IPlayerInventoryDataStore playerInventoryDataStore,
            IItemStackLevelUnlocker itemStackLevelUnlocker,
            IPlayerInventorySlotLevelDataStore playerInventorySlotLevelDataStore)
        {
            _gameUnlockStateDataController = gameUnlockStateDataController;
            _playerInventoryDataStore = playerInventoryDataStore;
            _itemStackLevelUnlocker = itemStackLevelUnlocker;
            _playerInventorySlotLevelDataStore = playerInventorySlotLevelDataStore;
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
                    case GameActionElement.GameActionTypeConst.unlockMachineRecipe:
                    case GameActionElement.GameActionTypeConst.unlockItemStackLevel:
                    case GameActionElement.GameActionTypeConst.unlockBlock:
                    case GameActionElement.GameActionTypeConst.unlockTrainCar:
                    case GameActionElement.GameActionTypeConst.unlockPlayerInventorySlotLevel:
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

                case GameActionElement.GameActionTypeConst.unlockMachineRecipe:
                    UnlockMachineRecipe();
                    break;

                case GameActionElement.GameActionTypeConst.unlockBlock:
                    UnlockBlock();
                    break;

                case GameActionElement.GameActionTypeConst.unlockTrainCar:
                    UnlockTrainCar();
                    break;

                case GameActionElement.GameActionTypeConst.giveItem:
                    GiveItem();
                    break;

                case GameActionElement.GameActionTypeConst.unlockItemStackLevel:
                    _itemStackLevelUnlocker.ApplyUnlockItemStackLevelAction(action);
                    break;

                case GameActionElement.GameActionTypeConst.unlockPlayerInventorySlotLevel:
                    UnlockPlayerInventorySlotLevel();
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

            void UnlockMachineRecipe()
            {
                var machineRecipeGuids = ((UnlockMachineRecipeGameActionParam)action.GameActionParam).UnlockMachineRecipeGuids;
                foreach (var guid in machineRecipeGuids)
                {
                    _gameUnlockStateDataController.UnlockMachineRecipe(guid);
                }
            }

            void UnlockBlock()
            {
                var blockGuids = ((UnlockBlockGameActionParam)action.GameActionParam).UnlockBlockGuids;
                foreach (var guid in blockGuids)
                {
                    _gameUnlockStateDataController.UnlockBlock(guid);
                }
            }

            void UnlockTrainCar()
            {
                var trainCarGuids = ((UnlockTrainCarGameActionParam)action.GameActionParam).UnlockTrainCarGuids;
                foreach (var guid in trainCarGuids)
                {
                    _gameUnlockStateDataController.UnlockTrainCar(guid);
                }
            }

            void UnlockPlayerInventorySlotLevel()
            {
                var level = ((UnlockPlayerInventorySlotLevelGameActionParam)action.GameActionParam).Level;
                _playerInventorySlotLevelDataStore.UnlockLevel(level);
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
