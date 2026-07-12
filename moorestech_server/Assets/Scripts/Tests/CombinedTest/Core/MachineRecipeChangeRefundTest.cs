using System;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.UnlockState;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    // 加工中のレシピ変更・クリア時の材料返却フローを検証する
    // Verifies material refund flow when changing or clearing a recipe mid-processing
    public class MachineRecipeChangeRefundTest
    {
        [Test]
        public void RefundToInputInventoryTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var next = MachineRecipeChangeRefundTestHelper.FindAlternateRecipe(recipe);
            Assert.IsNotNull(next);
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            MachineRecipeChangeRefundTestHelper.StartProcessing(selector, processor, inventory, recipe, overflow);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(next, overflow));
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.AreEqual(next.MachineRecipeGuid, selector.SelectedRecipeGuid);

            var (input, output) = MachineRecipeChangeRefundTestHelper.GetNonEmptySlots(inventory);
            Assert.AreEqual(0, output.Count);
            foreach (var inputItem in recipe.InputItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid);
                Assert.AreEqual(inputItem.Count, MachineRecipeChangeRefundTestHelper.CountItem(input, itemId));
            }
        }

        [Test]
        public void RefundOverflowGoesToPlayerInventoryTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var next = MachineRecipeChangeRefundTestHelper.FindAlternateRecipe(recipe);
            Assert.IsNotNull(next);
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            MachineRecipeChangeRefundTestHelper.StartProcessing(selector, processor, inventory, recipe, overflow);
            MachineRecipeChangeRefundTestHelper.FillInputSlotsWithFiller(inventory, new Guid("00000000-0000-0000-1234-000000000003"));

            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(next, overflow));
            foreach (var inputItem in recipe.InputItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid);
                Assert.AreEqual(inputItem.Count, MachineRecipeChangeRefundTestHelper.CountOverflowItem(overflow, itemId));
            }
        }

        [Test]
        public void RefundImpossibleCancelsChangeTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var next = MachineRecipeChangeRefundTestHelper.FindAlternateRecipe(recipe);
            Assert.IsNotNull(next);
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            MachineRecipeChangeRefundTestHelper.StartProcessing(selector, processor, inventory, recipe, overflow);
            MachineRecipeChangeRefundTestHelper.FillInputSlotsWithFiller(inventory, new Guid("00000000-0000-0000-1234-000000000003"));
            var (inputBefore, _) = MachineRecipeChangeRefundTestHelper.GetNonEmptySlots(inventory);
            var beforeSnapshot = inputBefore.Select(i => (i.Id, i.Count)).ToList();

            var noOverflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(0);
            Assert.AreEqual(MachineRecipeSelectionResult.RefundFailed, selector.SetSelectedRecipe(next, noOverflow));
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(recipe.MachineRecipeGuid, selector.SelectedRecipeGuid);

            var (inputAfter, _) = MachineRecipeChangeRefundTestHelper.GetNonEmptySlots(inventory);
            Assert.AreEqual(beforeSnapshot.Count, inputAfter.Count);
            for (var i = 0; i < beforeSnapshot.Count; i++)
            {
                Assert.AreEqual(beforeSnapshot[i].Id, inputAfter[i].Id);
                Assert.AreEqual(beforeSnapshot[i].Count, inputAfter[i].Count);
            }
        }

        [Test]
        public void ClearDuringProcessingRefundsTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            MachineRecipeChangeRefundTestHelper.StartProcessing(selector, processor, inventory, recipe, overflow);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.ClearSelectedRecipe(overflow));
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.AreEqual(Guid.Empty, selector.SelectedRecipeGuid);

            var (input, _) = MachineRecipeChangeRefundTestHelper.GetNonEmptySlots(inventory);
            foreach (var inputItem in recipe.InputItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid);
                Assert.AreEqual(inputItem.Count, MachineRecipeChangeRefundTestHelper.CountItem(input, itemId));
            }
        }

        [Test]
        public void SameRecipeReSelectIsNoOpTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            MachineRecipeChangeRefundTestHelper.StartProcessing(selector, processor, inventory, recipe, overflow);
            var remainingBefore = processor.GetRemainingTicks();
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(recipe, overflow));
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(remainingBefore, processor.GetRemainingTicks());
            Assert.AreEqual(recipe.MachineRecipeGuid, processor.RecipeGuid);
        }

        [Test]
        public void IsRemainInputIsNotRefundedTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                .First(r => r.InputItems.Any(i => i.IsRemain.HasValue && i.IsRemain.Value));
            var next = MachineRecipeChangeRefundTestHelper.FindAlternateRecipe(recipe);
            Assert.IsNotNull(next);
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            MachineRecipeChangeRefundTestHelper.StartProcessing(selector, processor, inventory, recipe, overflow);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(next, overflow));

            var remain = recipe.InputItems.First(i => i.IsRemain.HasValue && i.IsRemain.Value);
            var remainId = MasterHolder.ItemMaster.GetItemId(remain.ItemGuid);
            var (input, _) = MachineRecipeChangeRefundTestHelper.GetNonEmptySlots(inventory);
            Assert.AreEqual(remain.Count, MachineRecipeChangeRefundTestHelper.CountItem(input, remainId));
        }

        [Test]
        public void FluidRefundBestEffortTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            // LockedMachineRecipeフィクスチャを明示アンロックしてから選択する
            // Explicitly unlock the LockedMachineRecipe fixture before selecting it
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockMachineRecipe(ForUnitTestMachineRecipeId.LockedMachineRecipe);

            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.LockedMachineRecipe);
            Assert.IsTrue(recipe.InputFluids.Length >= 2, "流体返却テストには2種類以上の入力液体が必要");
            var next = MachineRecipeChangeRefundTestHelper.FindAlternateRecipe(recipe);
            Assert.IsNotNull(next);
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(recipe, overflow));
            MachineRecipeChangeRefundTestHelper.InsertRecipeInputs(inventory, recipe);
            var tanks = MachineRecipeChangeRefundTestHelper.GetInputInventory(inventory).FluidInputSlot;
            MachineRecipeChangeRefundTestHelper.InsertRecipeFluids(tanks, recipe);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            Assert.IsTrue(tanks.Count >= 3);
            var (before0, before1, before2, expected0, expected1) = MachineRecipeChangeRefundTestHelper.PreparePartialFluidTanksForOverflowRefund(tanks, recipe, MachineFluidIOTest.FluidId3);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(next, overflow));
            Assert.AreEqual(next.MachineRecipeGuid, selector.SelectedRecipeGuid);
            Assert.AreEqual(expected0, tanks[0].Amount, 0.0001);
            Assert.AreEqual(expected1, tanks[1].Amount, 0.0001);
            Assert.AreEqual(before2, tanks[2].Amount, 0.0001);
            Assert.AreEqual(MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[0].FluidGuid), tanks[0].FluidId);
            Assert.AreEqual(MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[1].FluidGuid), tanks[1].FluidId);
            // 返却合計が容量に収まらず溢れた分は消失していること
            // Overflow beyond tank capacity is discarded and not preserved in total
            Assert.Less(expected0 + expected1, before0 + before1 + recipe.InputFluids[0].Amount + recipe.InputFluids[1].Amount);
        }
    }
}
