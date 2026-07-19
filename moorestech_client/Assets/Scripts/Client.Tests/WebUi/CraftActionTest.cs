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
    /// craft.execute の recipeGuid 解決ゲート（不正/未存在/ロック/成功）の純関数テスト
    /// Pure-function tests for craft.execute's recipeGuid resolution gate (malformed/unknown/locked/success)
    /// </summary>
    public class CraftActionTest
    {
        // 不正な形式の guid は invalid_recipe
        // A malformed guid resolves to invalid_recipe
        [Test]
        public void ResolveCraftRecipeMalformedGuidReturnsInvalidRecipe()
        {
            var unlock = new StubUnlockStateData(new Dictionary<Guid, CraftRecipeUnlockStateInfo>());
            var result = CraftExecuteActionHandler.ResolveCraftRecipe(new JValue("not-a-guid"), unlock, out _);
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("invalid_recipe", result.Error);
        }

        // 実在しない guid は invalid_recipe
        // An unknown guid resolves to invalid_recipe
        [Test]
        public void ResolveCraftRecipeUnknownGuidReturnsInvalidRecipe()
        {
            var unlock = new StubUnlockStateData(new Dictionary<Guid, CraftRecipeUnlockStateInfo>());
            var guid = Guid.NewGuid();
            var result = CraftExecuteActionHandler.ResolveCraftRecipe(new JValue(guid.ToString()), unlock, out _);
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("invalid_recipe", result.Error);
        }

        // ロック中の guid は recipe_locked（guid はパース成功する）
        // A locked guid resolves to recipe_locked (the guid still parses)
        [Test]
        public void ResolveCraftRecipeLockedReturnsRecipeLocked()
        {
            var guid = Guid.NewGuid();
            var infos = new Dictionary<Guid, CraftRecipeUnlockStateInfo> { { guid, new CraftRecipeUnlockStateInfo(guid, false) } };
            var unlock = new StubUnlockStateData(infos);
            var result = CraftExecuteActionHandler.ResolveCraftRecipe(new JValue(guid.ToString()), unlock, out var parsed);
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("recipe_locked", result.Error);
            Assert.AreEqual(guid, parsed);
        }

        // 解放済みの guid は成功し、パース済み guid を返す
        // An unlocked guid succeeds and returns the parsed guid
        [Test]
        public void ResolveCraftRecipeUnlockedReturnsSuccess()
        {
            var guid = Guid.NewGuid();
            var infos = new Dictionary<Guid, CraftRecipeUnlockStateInfo> { { guid, new CraftRecipeUnlockStateInfo(guid, true) } };
            var unlock = new StubUnlockStateData(infos);
            var result = CraftExecuteActionHandler.ResolveCraftRecipe(new JValue(guid.ToString()), unlock, out var parsed);
            Assert.IsTrue(result.Ok);
            Assert.IsNull(result.Error);
            Assert.AreEqual(guid, parsed);
        }

        /// <summary>
        /// クラフトレシピの解放状態だけを差し込むテスト用スタブ
        /// Test stub that injects only craft-recipe unlock state
        /// </summary>
        private class StubUnlockStateData : IGameUnlockStateData
        {
            public StubUnlockStateData(IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> craftRecipeUnlockStateInfos)
            {
                CraftRecipeUnlockStateInfos = craftRecipeUnlockStateInfos;
            }

            public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos { get; }
            public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos { get; } = new Dictionary<ItemId, ItemUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos { get; } = new Dictionary<Guid, ChallengeCategoryUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos { get; } = new Dictionary<Guid, MachineRecipeUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos { get; } = new Dictionary<Guid, BlockUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos { get; } = new Dictionary<Guid, TrainCarUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> ConnectToolUnlockStateInfos { get; } = new Dictionary<Guid, ConnectToolUnlockStateInfo>();
        }
    }
}
