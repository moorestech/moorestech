using System;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Core.Fluid;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.MachineFluidIOTest;

namespace Tests.CombinedTest.Core.Machine
{
    public class GearMachineFluidConnectionTest
    {
        // 流体の速度モデルが落ち着くのに十分な tick 数(ゲーム内 60 秒)
        // Enough ticks for the fluid velocity model to settle (60 in-game seconds)
        private static readonly uint FluidSettleTicks = GameUpdater.SecondsToTicks(60);

        // タンクを持つ歯車機械は電気機械と同じく配管に繋がり、出力タンクの液体がパイプへ流れる
        // A gear machine with tanks connects to pipes like electric machines, and its output tank drains into the pipe
        [Test]
        public void GearMachineWithTanksDrainsOutputToPipe()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            // 歯車機械と、その出力口(-X)に隣接するパイプを置く
            // Place the gear machine and a pipe next to its outflow port (-X)
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidGearMachine, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gearMachineBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pipeBlock);

            // タンクは入出力1本ずつで、配管コネクタはパイプ1本と繋がる
            // One input and one output tank; the pipe connector links to the single pipe
            var blockInventory = gearMachineBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(1, MachineInventoryTestHelper.GetInputFluidContainers(blockInventory).Count);
            var outputTanks = MachineFluidTestUtil.GetOutputFluidContainers(blockInventory);
            Assert.AreEqual(1, outputTanks.Count);
            var fluidConnector = gearMachineBlock.GetComponent<BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>>>();
            Assert.AreEqual(1, fluidConnector.ConnectedTargets.Count);

            // 出力タンクに入れた液体が、tick を進めるとパイプへ移る
            // Fluid placed in the output tank moves into the pipe as ticks advance
            const double fluidAmount = 40d;
            outputTanks[0].AddLiquid(new FluidStack(fluidAmount, FluidId1));
            GameUpdater.RunFrames(FluidSettleTicks);
            var pipe = pipeBlock.GetComponent<FluidPipeComponent>();
            Assert.AreEqual(FluidId1, pipe.GetFluidId());
            Assert.AreEqual(fluidAmount, pipe.GetAmount(), 0.01f);
            Assert.AreEqual(0, outputTanks[0].Amount, 0.01f);
        }

        // タンク0本の歯車機械には配管部品が付かない
        // A gear machine with zero tanks gets no pipe components
        [Test]
        public void GearMachineWithoutTanksHasNoFluidComponents()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearMachine, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(0, MachineInventoryTestHelper.GetInputFluidContainers(blockInventory).Count);
            Assert.AreEqual(0, MachineFluidTestUtil.GetOutputFluidContainers(blockInventory).Count);
            Assert.IsFalse(block.ExistsComponent<VanillaMachineFluidInventoryComponent>());
        }
    }
}
