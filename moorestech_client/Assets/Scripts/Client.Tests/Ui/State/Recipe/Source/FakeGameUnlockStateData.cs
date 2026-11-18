using System;
using System.Collections.Generic;
using Core.Master;
using Game.UnlockState;
using Game.UnlockState.States;

namespace Client.Tests.Ui.State.Recipe.Source
{
    /// <summary>
    /// 機械レシピの解放表だけを差し替えられる解放状態の偽物
    /// Unlock-state fake whose machine-recipe table alone can be replaced
    /// </summary>
    public sealed class FakeGameUnlockStateData : IGameUnlockStateData
    {
        private readonly Dictionary<Guid, MachineRecipeUnlockStateInfo> _machineRecipes = new();

        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos { get; } = new Dictionary<Guid, CraftRecipeUnlockStateInfo>();
        public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos { get; } = new Dictionary<ItemId, ItemUnlockStateInfo>();
        public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos { get; } = new Dictionary<Guid, ChallengeCategoryUnlockStateInfo>();
        public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos => _machineRecipes;
        public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos { get; } = new Dictionary<Guid, BlockUnlockStateInfo>();
        public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos { get; } = new Dictionary<Guid, TrainCarUnlockStateInfo>();
        public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> ConnectToolUnlockStateInfos { get; } = new Dictionary<Guid, ConnectToolUnlockStateInfo>();
        public bool IsBlueprintUnlocked => false;

        public void SetMachineRecipe(Guid recipeGuid, bool unlocked)
        {
            _machineRecipes[recipeGuid] = new MachineRecipeUnlockStateInfo(recipeGuid, unlocked);
        }
    }
}
