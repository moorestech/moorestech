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
    }
    
    /// <summary>
    /// アンロック状態のデータを操作するためのインターフェース
    ///
    /// An interface for manipulating unlock state data.
    /// </summary>
    public interface IGameUnlockStateDataController : IGameUnlockStateData
    {
        public IObservable<Guid> OnUnlockCraftRecipe { get; }
        void UnlockCraftRecipe(Guid recipeGuid);
        
        public IObservable<ItemId> OnUnlockItem { get; }
        void UnlockItem(ItemId itemId);
        
        void LoadUnlockState(GameUnlockStateJsonObject stateJsonObject);
        GameUnlockStateJsonObject GetSaveJsonObject();
    }
}