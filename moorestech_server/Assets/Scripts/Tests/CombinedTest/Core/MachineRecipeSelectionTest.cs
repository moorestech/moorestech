using System;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Core.Update;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    // レシピ選択必須化の基本挙動を検証する
    // Verifies the core behavior of mandatory recipe selection
    public class MachineRecipeSelectionTest
    {
        [Test]
        public void NoSelectionNeverProcessesTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // レシピ材料のみ投入(選択せず)
            // Pick an electric machine recipe and insert only its inputs without selecting it
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }

            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            for (var i = 0; i < 10; i++) GameUpdater.UpdateOneTick();

            // 未選択のため加工は始まらない
            // Processing must not start without a selection
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
        }

        [Test]
        public void SelectedRecipeProcessesTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            // レシピを選択してから材料を投入
            // Select the recipe, then insert its inputs
            Assert.IsTrue(block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector));
            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(recipe, overflow));
            Assert.AreEqual(recipe.MachineRecipeGuid, selector.SelectedRecipeGuid);

            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }

            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
        }

        [Test]
        public void WrongBlockRecipeIsRejectedTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 他ブロック用レシピを設定試行
            // Place block A and try to set a recipe that belongs to block B
            var recipeA = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            MachineRecipeMasterElement recipeB = null;
            foreach (var r in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                if (r.BlockGuid != recipeA.BlockGuid) { recipeB = r; break; }
            }
            Assert.IsNotNull(recipeB, "テストモッドに2種類以上の機械ブロックのレシピが必要");

            var blockId = MasterHolder.BlockMaster.GetBlockId(recipeA.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);

            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
            Assert.AreEqual(MachineRecipeSelectionResult.RecipeBlockMismatch, selector.SetSelectedRecipe(recipeB, overflow));
            Assert.AreEqual(Guid.Empty, selector.SelectedRecipeGuid);
        }

        [Test]
        public void LockedRecipeIsRejectedTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 未アンロックのレシピを対象にする
            // Target the initialUnlocked:false recipe added to the test mod in Task 1
            MachineRecipeMasterElement lockedRecipe = null;
            foreach (var r in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                if (!r.InitialUnlocked) { lockedRecipe = r; break; }
            }
            Assert.IsNotNull(lockedRecipe, "テストモッドにinitialUnlocked:falseのレシピが必要（Task 1）");

            var blockId = MasterHolder.BlockMaster.GetBlockId(lockedRecipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);

            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
            Assert.AreEqual(MachineRecipeSelectionResult.RecipeLocked, selector.SetSelectedRecipe(lockedRecipe, overflow));
            Assert.AreEqual(Guid.Empty, selector.SelectedRecipeGuid);
        }

        [Test]
        public void SameInputsDifferentRecipeSelectionTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 同一入力・別出力ペアで選択優先を確認
            // Find the duplicate-input pair added in Task 1 and verify the selected one is used
            MachineRecipeMasterElement first = null, second = null;
            var data = MasterHolder.MachineRecipesMaster.MachineRecipes.Data;
            for (var i = 0; i < data.Length && second == null; i++)
            for (var j = i + 1; j < data.Length; j++)
            {
                if (data[i].BlockGuid != data[j].BlockGuid) continue;
                if (!SameInputs(data[i], data[j])) continue;
                first = data[i]; second = data[j];
                break;
            }
            Assert.IsNotNull(second, "テストモッドに同一入力レシピペアが必要（Task 1）");

            var blockId = MasterHolder.BlockMaster.GetBlockId(second.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
            selector.SetSelectedRecipe(second, overflow);

            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var inputItem in second.InputItems)
            {
                blockInventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }

            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(second.MachineRecipeGuid, processor.RecipeGuid);

            #region Internal

            static bool SameInputs(MachineRecipeMasterElement a, MachineRecipeMasterElement b)
            {
                if (a.InputItems.Length != b.InputItems.Length) return false;
                foreach (var ia in a.InputItems)
                {
                    var found = false;
                    foreach (var ib in b.InputItems)
                    {
                        if (ia.ItemGuid == ib.ItemGuid && ia.Count == ib.Count) { found = true; break; }
                    }
                    if (!found) return false;
                }
                return true;
            }

            #endregion
        }
    }
}
