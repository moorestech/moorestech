using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class MachineFluidIOTest
    {
        [Test]
        public void FluidProcessingOutputTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[9];

            var blockPosition = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var blockEvent = new BlockOpenableInventoryUpdateEvent();
            var connector = new BlockConnectorComponent<IBlockInventory>(null, null, blockPosition);

            var inputInventory = new VanillaMachineInputInventory(
                MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid),
                0,
                recipe.InputFluids.Length,
                10f,
                blockEvent,
                new BlockInstanceId(1)
            );

            var outputInventory = new VanillaMachineOutputInventory(
                0,
                ServerContext.ItemStackFactory,
                blockEvent,
                new BlockInstanceId(1),
                0,
                connector
            );

            var processor = new VanillaMachineProcessorComponent(inputInventory, outputInventory, recipe, new ElectricPower(100));

            var inventoryComponent = new VanillaMachineBlockInventoryComponent(inputInventory, outputInventory);
            var machineComponent = new VanillaElectricMachineComponent(new BlockInstanceId(1), processor);

            var components = new List<IBlockComponent> { inventoryComponent, processor, machineComponent, connector };
            var block = new BlockSystem(new BlockInstanceId(1), recipe.BlockGuid, components, blockPosition);

            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                var fluid = recipe.InputFluids[i];
                var fluidId = MasterHolder.FluidMaster.GetFluidId(fluid.FluidGuid);
                inputInventory.FluidInputSlot[i].AddLiquid(new FluidStack(fluid.Amount, fluidId), FluidContainer.Empty, out var remain);
                Assert.IsNull(remain);
            }

            var craftTime = DateTime.Now.AddSeconds(recipe.Time);
            while (craftTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1)
            {
                processor.SupplyPower(new ElectricPower(10000));
                GameUpdater.UpdateWithWait();
            }

            foreach (var container in inputInventory.FluidInputSlot)
            {
                Assert.AreEqual(0f, container.Amount, 0.01f);
            }
        }
    }
}
