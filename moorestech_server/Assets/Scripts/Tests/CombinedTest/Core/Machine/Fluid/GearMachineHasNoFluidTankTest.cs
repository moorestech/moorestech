using System;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.Machine
{
    public class GearMachineHasNoFluidTankTest
    {
        // 歯車機械は液体タンクの口を持たず、置いてもタンク0・配管接続なしで組まれることを確かめる
        // Verifies gear machines expose no fluid-tank params and are built with zero tanks and no pipe connector
        [Test]
        public void GearMachineIsBuiltWithoutFluidTanks()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // スキーマ上、歯車機械はタンク項目を持たず、液体機械は持つ
            // In the schema, gear machines lack tank params while the fluid machine has them
            var gearMachineParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearMachine).BlockParam;
            var fluidMachineParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.FluidMachineId).BlockParam;
            Assert.IsInstanceOf<IMachineParam>(gearMachineParam);
            Assert.IsNotInstanceOf<IMachineFluidTankParam>(gearMachineParam);
            Assert.IsInstanceOf<IMachineFluidTankParam>(fluidMachineParam);

            // 置いた歯車機械の入出力タンクは0本で、液体インベントリ部品も付かない
            // A placed gear machine has zero input/output tanks and no fluid inventory component
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearMachine, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(0, MachineInventoryTestHelper.GetInputFluidContainers(blockInventory).Count);
            Assert.AreEqual(0, MachineFluidTestUtil.GetOutputFluidContainers(blockInventory).Count);
            Assert.IsFalse(block.ExistsComponent<VanillaMachineFluidInventoryComponent>());
        }
    }
}
