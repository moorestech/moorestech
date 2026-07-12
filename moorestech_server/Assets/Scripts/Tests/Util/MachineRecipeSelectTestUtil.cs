using Core.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;

namespace Tests.Util
{
    // 自動判定前提だった既存テストを最小変更で移行するためのレシピ選択ヘルパー
    // Helper to migrate auto-detection-era tests to explicit recipe selection with minimal edits
    public static class MachineRecipeSelectTestUtil
    {
        public static void SelectRecipe(IBlock machineBlock, MachineRecipeMasterElement recipe)
        {
            Assert.IsTrue(machineBlock.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector), "機械ブロックではありません");
            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 100);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(recipe, overflow), "テスト用レシピ選択に失敗");
        }
    }
}
