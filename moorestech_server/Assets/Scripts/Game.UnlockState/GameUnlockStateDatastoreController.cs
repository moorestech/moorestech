using System;
using System.Collections.Generic;
using Core.Master;
using Game.UnlockState.Holders;
using Game.UnlockState.States;
using Newtonsoft.Json;

namespace Game.UnlockState
{
    public class GameUnlockStateDataController : IGameUnlockStateDataController
    {
        // ドメイン別ホルダーに解放状態の管理を委譲する
        // Delegate unlock state management to per-domain holders
        private readonly CraftRecipeUnlockStateHolder _craftRecipe = new();
        private readonly ItemUnlockStateHolder _item = new();
        private readonly ChallengeCategoryUnlockStateHolder _challengeCategory = new();
        private readonly MachineRecipeUnlockStateHolder _machineRecipe = new();
        private readonly BlockUnlockStateHolder _block = new();
        private readonly TrainCarUnlockStateHolder _trainCar = new();
        private readonly ConnectToolUnlockStateHolder _connectTool = new();

        public IObservable<Guid> OnUnlockCraftRecipe => _craftRecipe.OnUnlock;
        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos => _craftRecipe.Infos;
        public void UnlockCraftRecipe(Guid recipeGuid) => _craftRecipe.Unlock(recipeGuid);

        public IObservable<ItemId> OnUnlockItem => _item.OnUnlock;
        public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos => _item.Infos;
        public void UnlockItem(ItemId itemId) => _item.Unlock(itemId);

        public IObservable<Guid> OnUnlockChallengeCategory => _challengeCategory.OnUnlock;
        public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos => _challengeCategory.Infos;
        public void UnlockChallenge(Guid categoryGuid) => _challengeCategory.Unlock(categoryGuid);

        public IObservable<Guid> OnUnlockMachineRecipe => _machineRecipe.OnUnlock;
        public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos => _machineRecipe.Infos;
        public void UnlockMachineRecipe(Guid machineRecipeGuid) => _machineRecipe.Unlock(machineRecipeGuid);

        public IObservable<Guid> OnUnlockBlock => _block.OnUnlock;
        public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos => _block.Infos;
        public void UnlockBlock(Guid blockGuid) => _block.Unlock(blockGuid);

        public IObservable<Guid> OnUnlockTrainCar => _trainCar.OnUnlock;
        public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos => _trainCar.Infos;
        public void UnlockTrainCar(Guid trainCarGuid) => _trainCar.Unlock(trainCarGuid);

        public IObservable<Guid> OnUnlockConnectTool => _connectTool.OnUnlock;
        public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> ConnectToolUnlockStateInfos => _connectTool.Infos;
        public void UnlockConnectTool(Guid connectToolGuid) => _connectTool.Unlock(connectToolGuid);

        #region SaveLoad

        public void LoadUnlockState(GameUnlockStateJsonObject stateJsonObject)
        {
            _craftRecipe.Load(stateJsonObject.CraftRecipeUnlockStateInfos);
            _item.Load(stateJsonObject.ItemUnlockStateInfos);
            _challengeCategory.Load(stateJsonObject.ChallengeCategoryUnlockStateInfos);
            _machineRecipe.Load(stateJsonObject.MachineRecipeUnlockStateInfos);
            _block.Load(stateJsonObject.BlockUnlockStateInfos);
            _trainCar.Load(stateJsonObject.TrainCarUnlockStateInfos);
            _connectTool.Load(stateJsonObject.ConnectToolUnlockStateInfos);
        }

        public GameUnlockStateJsonObject GetSaveJsonObject()
        {
            return new GameUnlockStateJsonObject
            {
                CraftRecipeUnlockStateInfos = _craftRecipe.GetSaveJsonObject(),
                ItemUnlockStateInfos = _item.GetSaveJsonObject(),
                ChallengeCategoryUnlockStateInfos = _challengeCategory.GetSaveJsonObject(),
                MachineRecipeUnlockStateInfos = _machineRecipe.GetSaveJsonObject(),
                BlockUnlockStateInfos = _block.GetSaveJsonObject(),
                TrainCarUnlockStateInfos = _trainCar.GetSaveJsonObject(),
                ConnectToolUnlockStateInfos = _connectTool.GetSaveJsonObject(),
            };
        }

        #endregion
    }

    public class GameUnlockStateJsonObject
    {
        [JsonProperty("craftRecipeUnlockStateInfos")] public List<CraftRecipeUnlockStateInfoJsonObject> CraftRecipeUnlockStateInfos;
        [JsonProperty("itemUnlockStateInfos")] public List<ItemUnlockStateInfoJsonObject> ItemUnlockStateInfos;
        [JsonProperty("challengeCategoryUnlockStateInfos")] public List<ChallengeUnlockStateInfoJsonObject> ChallengeCategoryUnlockStateInfos;
        [JsonProperty("machineRecipeUnlockStateInfos")] public List<MachineRecipeUnlockStateInfoJsonObject> MachineRecipeUnlockStateInfos;
        [JsonProperty("blockUnlockStateInfos")] public List<BlockUnlockStateInfoJsonObject> BlockUnlockStateInfos;
        [JsonProperty("trainCarUnlockStateInfos")] public List<TrainCarUnlockStateInfoJsonObject> TrainCarUnlockStateInfos;
        [JsonProperty("connectToolUnlockStateInfos")] public List<ConnectToolUnlockStateInfoJsonObject> ConnectToolUnlockStateInfos;
    }
}
