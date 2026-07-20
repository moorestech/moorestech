using System;
using System.Collections.Generic;
using Client.WebUiHost.Game.Actions;
using Core.Master;
using Game.UnlockState;
using Game.UnlockState.States;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// machine_recipe.select のレシピ解決ゲートを検証する
    /// Verifies the recipe resolution gate for machine_recipe.select
    /// </summary>
    public class MachineRecipeSelectActionTest
    {
        // 不正な形式の guid は invalid_recipe
        // A malformed guid resolves to invalid_recipe
        [Test]
        public void ResolveSelectableRecipeMalformedGuidReturnsInvalidRecipe()
        {
            var unlock = new StubUnlockStateData(new Dictionary<Guid, MachineRecipeUnlockStateInfo>());
            var result = MachineRecipeSelectActionHandler.ResolveSelectableRecipe(new JValue("not-a-guid"), unlock, out _);
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("invalid_recipe", result.Error);
        }

        // 実在しない guid は invalid_recipe
        // An unknown guid resolves to invalid_recipe
        [Test]
        public void ResolveSelectableRecipeUnknownGuidReturnsInvalidRecipe()
        {
            var unlock = new StubUnlockStateData(new Dictionary<Guid, MachineRecipeUnlockStateInfo>());
            var guid = Guid.NewGuid();
            var result = MachineRecipeSelectActionHandler.ResolveSelectableRecipe(new JValue(guid.ToString()), unlock, out _);
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("invalid_recipe", result.Error);
        }

        // ロック中の guid は recipe_locked
        // A locked guid resolves to recipe_locked
        [Test]
        public void ResolveSelectableRecipeLockedReturnsRecipeLocked()
        {
            var guid = Guid.NewGuid();
            var infos = new Dictionary<Guid, MachineRecipeUnlockStateInfo> { { guid, new MachineRecipeUnlockStateInfo(guid, false) } };
            var unlock = new StubUnlockStateData(infos);
            var result = MachineRecipeSelectActionHandler.ResolveSelectableRecipe(new JValue(guid.ToString()), unlock, out var parsed);
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("recipe_locked", result.Error);
            Assert.AreEqual(guid, parsed);
        }

        // 解放済みの guid は成功し、パース済み guid を返す
        // An unlocked guid succeeds and returns the parsed guid
        [Test]
        public void ResolveSelectableRecipeUnlockedReturnsSuccess()
        {
            var guid = Guid.NewGuid();
            var infos = new Dictionary<Guid, MachineRecipeUnlockStateInfo> { { guid, new MachineRecipeUnlockStateInfo(guid, true) } };
            var unlock = new StubUnlockStateData(infos);
            var result = MachineRecipeSelectActionHandler.ResolveSelectableRecipe(new JValue(guid.ToString()), unlock, out var parsed);
            Assert.IsTrue(result.Ok);
            Assert.IsNull(result.Error);
            Assert.AreEqual(guid, parsed);
        }

        /// <summary>
        /// 機械レシピの解放状態だけを差し込むテスト用スタブ
        /// Test stub that injects only machine-recipe unlock state
        /// </summary>
        private class StubUnlockStateData : IGameUnlockStateData
        {
            public StubUnlockStateData(IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> machineRecipeUnlockStateInfos)
            {
                MachineRecipeUnlockStateInfos = machineRecipeUnlockStateInfos;
            }

            public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos { get; } = new Dictionary<Guid, CraftRecipeUnlockStateInfo>();
            public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos { get; } = new Dictionary<ItemId, ItemUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos { get; } = new Dictionary<Guid, ChallengeCategoryUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos { get; }
            public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos { get; } = new Dictionary<Guid, BlockUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos { get; } = new Dictionary<Guid, TrainCarUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> ConnectToolUnlockStateInfos { get; } = new Dictionary<Guid, ConnectToolUnlockStateInfo>();
        }
    }
}
