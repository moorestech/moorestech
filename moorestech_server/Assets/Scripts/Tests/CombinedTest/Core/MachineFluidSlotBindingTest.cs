using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using Game.UnlockState;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    // 液体タンクもレシピ順に束縛される（ADR 0042 R5）
    // Fluid tanks are bound to the recipe order as well (ADR 0042 R5)
    public class MachineFluidSlotBindingTest
    {
        [Test]
        public void InputTankAcceptsOnlyItsBoundFluid()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            // このレシピは既定でロックされているため明示アンロックする
            // This recipe is locked by default, so explicitly unlock it
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockMachineRecipe(ForUnitTestMachineRecipeId.LockedMachineRecipe);
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.LockedMachineRecipe);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var fluidInventory = block.GetComponent<VanillaMachineFluidInventoryComponent>();
            var fluid0 = MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[0].FluidGuid);
            var fluid1 = MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[1].FluidGuid);

            // タンク指定無しの流入は束縛タンクへ入る（fluid1はタンク1へ）
            // Undesignated inflow lands in the bound tank (fluid 1 goes to tank 1)
            var remainder = fluidInventory.AddLiquid(new FluidStack(2, fluid1), default);
            // GetFluidInventory()はAmount>0のタンクだけを返しタンク番号順ではないため、生のタンク列(index=タンク番号)を直接読む
            // GetFluidInventory() returns only non-empty tanks and is not in tank order, so read the raw per-index tank list directly
            var tanks = GetInputFluidContainers(blockInventory);

            Assert.AreEqual(0, remainder.Amount);
            Assert.AreEqual(fluid1, tanks[1].FluidId);
            Assert.AreNotEqual(fluid1, tanks[0].FluidId);

            // タンク0へ束縛外の液体を指定しても拒否される
            // Fluid 1 designated to tank 0 is refused
            var designatedRemainder = fluidInventory.AddLiquid(new FluidStack(1, fluid1), MachineFluidTestUtil.ConnectedToTank(0));
            Assert.AreEqual(1, designatedRemainder.Amount);

            // 挿入試行後(AddLiquid後)に再取得した生タンク列で、束縛外タンク0が空のままであることを確認する
            // Re-fetch the raw tank list after the AddLiquid attempt and verify the unbound tank 0 is still empty
            var tanksAfter = GetInputFluidContainers(blockInventory);
            Assert.AreNotEqual(fluid0, tanksAfter[0].FluidId);
            Assert.AreEqual(0, tanksAfter[0].Amount, 0.0001);
            Assert.AreEqual(2, tanksAfter[1].Amount, 0.0001);
        }

        [Test]
        public void UnselectedMachineRejectsFluid()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var fluidInventory = block.GetComponent<VanillaMachineFluidInventoryComponent>();

            var remainder = fluidInventory.AddLiquid(new FluidStack(5, new FluidId(1)), default);

            Assert.AreEqual(5, remainder.Amount);
        }

        // 入力タンクの生コンテナ列(index=タンク番号)をリフレクション経由で取得する
        // Fetch the raw input tank container list (index = tank number) via reflection
        private static IReadOnlyList<FluidContainer> GetInputFluidContainers(VanillaMachineBlockInventoryComponent blockInventory)
        {
            var inputInventory = (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(blockInventory);
            return inputInventory.FluidInputSlot;
        }
    }
}
