using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Mooresmaster.Model.GameActionModule;

namespace Game.Action
{
    public class GameActionExecutor : IGameActionExecutor
    {
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;
        private readonly GiveItemGameActionExecutor _giveItemGameActionExecutor;
        private readonly IItemStackLevelUnlocker _itemStackLevelUnlocker;
        private readonly IPlayerInventorySlotLevelDataStore _playerInventorySlotLevelDataStore;
        private readonly IModuleSlotStackLimitUnlocker _moduleSlotStackLimitUnlocker;

        public GameActionExecutor(
            IGameUnlockStateDataController gameUnlockStateDataController,
            IPlayerInventoryDataStore playerInventoryDataStore,
            IItemStackLevelUnlocker itemStackLevelUnlocker,
            IPlayerInventorySlotLevelDataStore playerInventorySlotLevelDataStore,
            IModuleSlotStackLimitUnlocker moduleSlotStackLimitUnlocker)
        {
            _gameUnlockStateDataController = gameUnlockStateDataController;
            _giveItemGameActionExecutor = new GiveItemGameActionExecutor(playerInventoryDataStore);
            _itemStackLevelUnlocker = itemStackLevelUnlocker;
            _playerInventorySlotLevelDataStore = playerInventorySlotLevelDataStore;
            _moduleSlotStackLimitUnlocker = moduleSlotStackLimitUnlocker;
        }

        public void ExecuteUnlockActions(GameActionElement[] actions, ActionExecutionContext context = default)
        {
            if (actions == null || actions.Length == 0) return;

            // 解放系だけを再実行する（付与・スキットは一度きり。集合は読み取り専用なので Enumerable.Contains で引く）
            // Re-run unlock kinds only; grants and skits happen once (the set is read-only, so look it up with Enumerable.Contains)
            foreach (var action in actions)
            {
                if (UnlockReplayGameActionTypes.Types.Contains(action.GameActionType)) ExecuteAction(action, context);
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

                case GameActionElement.GameActionTypeConst.unlockConnectTool:
                    UnlockConnectTool();
                    break;

                case GameActionElement.GameActionTypeConst.giveItem:
                    _giveItemGameActionExecutor.Execute(action, context);
                    break;

                case GameActionElement.GameActionTypeConst.unlockItemStackLevel:
                    _itemStackLevelUnlocker.ApplyUnlockItemStackLevelAction(action);
                    break;

                case GameActionElement.GameActionTypeConst.unlockPlayerInventorySlotLevel:
                    UnlockPlayerInventorySlotLevel();
                    break;

                case GameActionElement.GameActionTypeConst.unlockBlueprint:
                    _gameUnlockStateDataController.UnlockBlueprint();
                    break;

                case GameActionElement.GameActionTypeConst.unlockModuleSlotStackLevel:
                    _moduleSlotStackLimitUnlocker.ApplyUnlockModuleSlotStackLevelAction(action);
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

            void UnlockConnectTool()
            {
                var connectToolGuids = ((UnlockConnectToolGameActionParam)action.GameActionParam).UnlockConnectToolGuids;
                foreach (var guid in connectToolGuids)
                {
                    _gameUnlockStateDataController.UnlockConnectTool(guid);
                }
            }

            void UnlockPlayerInventorySlotLevel()
            {
                var level = ((UnlockPlayerInventorySlotLevelGameActionParam)action.GameActionParam).Level;
                _playerInventorySlotLevelDataStore.UnlockLevel(level);
            }
            
            #endregion
        }
    }
}
