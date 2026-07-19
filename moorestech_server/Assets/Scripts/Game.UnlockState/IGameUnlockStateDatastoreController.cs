using System;
using System.Collections.Generic;
using Core.Master;
using Game.UnlockState.States;

namespace Game.UnlockState
{
    /// <summary>
    /// アンロック状態のデータだけを見るためのインターフェース
    /// ItemRecipeViewerDataContainerで使う処理でインターフェースをクライアント側と共通化して処理したかったので定義した
    ///
    /// An interface for viewing only unlocked data.
    /// I wanted to define it because I wanted to process it in ItemRecipeViewerDataContainer using the same interface on the client side.
    /// </summary>
    public interface IGameUnlockStateData
    {
        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos { get; }
        public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos { get; }
        public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos { get; }
        public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos { get; }
        public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos { get; }
        public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos { get; }
        public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> ConnectToolUnlockStateInfos { get; }
    }
    
    /// <summary>
    /// アンロック状態のデータを操作するためのインターフェース
    /// サーバー限定のインスタンス
    ///
    /// An interface for manipulating unlock state data.
    /// Server-only instance.
    /// </summary>
    public interface IGameUnlockStateDataController : IGameUnlockStateData
    {
        public IObservable<Guid> OnUnlockCraftRecipe { get; }
        void UnlockCraftRecipe(Guid recipeGuid);
        
        public IObservable<ItemId> OnUnlockItem { get; }
        void UnlockItem(ItemId itemId);

        public IObservable<Guid> OnUnlockChallengeCategory { get; }
        void UnlockChallenge(Guid categoryGuid);

        public IObservable<Guid> OnUnlockMachineRecipe { get; }
        void UnlockMachineRecipe(Guid machineRecipeGuid);

        public IObservable<Guid> OnUnlockBlock { get; }
        void UnlockBlock(Guid blockGuid);

        public IObservable<Guid> OnUnlockTrainCar { get; }
        void UnlockTrainCar(Guid trainCarGuid);

        public IObservable<Guid> OnUnlockConnectTool { get; }
        void UnlockConnectTool(Guid connectToolGuid);

        void LoadUnlockState(GameUnlockStateJsonObject stateJsonObject);
        GameUnlockStateJsonObject GetSaveJsonObject();
    }
}
