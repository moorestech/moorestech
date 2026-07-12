using System;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Fluid;
using NUnit.Framework;
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
            var remainingBefore = MachineRecipeChangeRefundTestHelper.GetRemainingTicks(processor);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(recipe, overflow));
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(remainingBefore, MachineRecipeChangeRefundTestHelper.GetRemainingTicks(processor));
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
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                .First(r => r.InputFluids != null && r.InputFluids.Length > 0 && r.InitialUnlocked);
            var next = MachineRecipeChangeRefundTestHelper.FindAlternateRecipe(recipe);
            Assert.IsNotNull(next);
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            selector.SetSelectedRecipe(recipe, overflow);
            MachineRecipeChangeRefundTestHelper.InsertRecipeInputs(inventory, recipe);
            var tanks = MachineRecipeChangeRefundTestHelper.GetInputInventory(inventory).FluidInputSlot;
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[i].FluidGuid);
                tanks[i].AddLiquid(new FluidStack(recipe.InputFluids[i].Amount, fluidId), FluidContainer.Empty);
            }
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            MachineRecipeChangeRefundTestHelper.FillFluidTanksToCapacity(tanks, MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[0].FluidGuid));
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(next, overflow));
            Assert.AreEqual(next.MachineRecipeGuid, selector.SelectedRecipeGuid);
            foreach (var tank in tanks)
            {
                Assert.LessOrEqual(tank.Amount, tank.Capacity);
            }
        }
    }
}
