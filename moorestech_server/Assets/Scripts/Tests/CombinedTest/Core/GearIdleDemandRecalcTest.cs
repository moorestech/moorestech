using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class GearIdleDemandRecalcTest
    {
        [Test]
        public void ProcessingDemandBlacksOutNetworkThatCanSupplyOnlyIdleDemandTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 同じ歯数で直結し、機械を基準RPMで駆動する
            // Connect equal-tooth gears directly so the machine runs at its base RPM
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.GearMachine, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machineBlock);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            var machine = machineBlock.GetComponent<GearEnergyTransformer>();
            var processor = machineBlock.GetComponent<VanillaMachineProcessorComponent>();
            var generator = generatorBlock.GetComponent<SimpleGearGeneratorComponent>();
            var param = (GearMachineBlockParam)machineBlock.BlockMasterElement.BlockParam;
            var baseRpm = (float)param.GearConsumption.BaseRpm;
            var fullTorque = GearConsumptionCalculator.CalcRequiredTorque(param.GearConsumption, new RPM(baseRpm)).AsPrimitive();
            var idleTorque = fullTorque * (float)param.GearConsumption.IdlePowerRate;
            var supplyTorque = (fullTorque + idleTorque) / 2f;
            generator.SetGenerateRpm(baseRpm);
            generator.SetGenerateTorque(supplyTorque);

            // 供給0.06はアイドル需要0.02を満たすが、フル需要0.1には不足する
            // Supply torque 0.06 covers idle demand 0.02 but cannot cover full demand 0.1
            GameUpdater.UpdateOneTick();
            var network = GearNetworkDatastoreReflectionTestUtil.GetAppliedNetwork(machineBlock.BlockInstanceId);
            Assert.Greater(machine.CurrentRpm.AsPrimitive(), 0f);
            Assert.AreEqual(GearNetworkStopReason.None, network.CurrentGearNetworkInfo.StopReason);

            // 加工開始で要求倍率を1へ戻し、次tickの再計算でblackoutする
            // Starting processing restores request rate 1, and the next tick recalculation blacks out the network
            InsertRecipeInputs(machineBlock, GetMachineRecipe());
            processor.Update();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(0f, machine.CurrentRpm.AsPrimitive());
            Assert.AreEqual(GearNetworkStopReason.OverRequirePower, network.CurrentGearNetworkInfo.StopReason);
        }

        private static MachineRecipeMasterElement GetMachineRecipe()
        {
            var machineGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearMachine).BlockGuid;
            return MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(recipe => recipe.BlockGuid == machineGuid);
        }

        private static void InsertRecipeInputs(IBlock block, MachineRecipeMasterElement recipe)
        {
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var inputItem in recipe.InputItems)
            {
                inventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }
        }
    }
}
