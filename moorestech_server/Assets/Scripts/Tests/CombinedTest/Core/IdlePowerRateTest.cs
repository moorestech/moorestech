using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Miner;
using Game.Block.Blocks.Pump;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class IdlePowerRateTest
    {
        [Test]
        public void ElectricMachineUsesIdleRateUntilProcessingStartsTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 対象機械を配置し、明示倍率が読み込まれていることを確認する
            // Place the target machine and verify the explicit rate is loaded
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var param = (ElectricMachineBlockParam)block.BlockMasterElement.BlockParam;
            var electric = block.GetComponent<VanillaElectricMachineComponent>();
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            var idlePowerRate = ResolveIdlePowerRate(param.IdlePowerRate);
            Assert.AreEqual(0.25f, idlePowerRate, 0.0001f);

            // 入力が無いIdle状態では要求電力がidlePowerRate分だけ下がる
            // In Idle without inputs, requested power is reduced by idlePowerRate
            Assert.AreEqual(param.RequiredPower * idlePowerRate, electric.RequestEnergy.AsPrimitive(), 0.0001f);

            // レシピ入力を投入して加工が始まるとフル要求へ戻る
            // Once recipe inputs start processing, demand returns to full power
            InsertRecipeInputs(block, GetMachineRecipe());
            processor.SupplyPower(processor.RequestPower);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(param.RequiredPower, electric.RequestEnergy.AsPrimitive(), 0.0001f);
        }

        [Test]
        public void ElectricPumpUsesIdleRateWhenInnerTankIsFullTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 空きタンクのポンプは生成可能なのでフル要求になる
            // A pump with free tank space can generate, so it requests full power
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPump, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var param = (ElectricPumpBlockParam)block.BlockMasterElement.BlockParam;
            var electric = block.GetComponent<ElectricPumpComponent>();
            var output = block.GetComponent<PumpFluidOutputComponent>();
            var idlePowerRate = ResolveIdlePowerRate(param.IdlePowerRate);
            Assert.AreEqual(param.RequiredPower, electric.RequestEnergy.AsPrimitive(), 0.0001f);

            // 内部タンクを満杯にすると生成不可のIdle扱いになり要求電力が下がる
            // Filling the inner tank makes generation idle and reduces requested power
            var fluidId = MasterHolder.FluidMaster.GetFluidId(param.GenerateFluid.items[0].FluidGuid);
            output.EnqueueGeneratedFluid(new FluidStack(param.InnerTankCapacity, fluidId));
            Assert.AreEqual(param.RequiredPower * idlePowerRate, electric.RequestEnergy.AsPrimitive(), 0.0001f);
        }

        [Test]
        public void ElectricPumpUsesIdleRateWhenFluidCannotBeGeneratedTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 不一致鉱脈ではIdle要求
            // On a mismatched vein there are no generation entries, so demand stays idle
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPump, new Vector3Int(20, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var param = (ElectricPumpBlockParam)block.BlockMasterElement.BlockParam;
            var electric = block.GetComponent<ElectricPumpComponent>();
            var idlePowerRate = ResolveIdlePowerRate(param.IdlePowerRate);
            Assert.AreEqual(param.RequiredPower * idlePowerRate, electric.RequestEnergy.AsPrimitive(), 0.0001f);
        }

        [Test]
        public void ElectricMinerUsesIdleRateUntilMiningStartsTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 配置直後はIdle、更新後は採掘
            // Immediately after placement demand is idle, then becomes full once mining is possible
            var (_, pos) = MinerMiningTest.GetItemMapVein();
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricMinerId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var param = (ElectricMinerBlockParam)block.BlockMasterElement.BlockParam;
            var electric = block.GetComponent<VanillaElectricMinerComponent>();
            var processor = block.GetComponent<VanillaMinerProcessorComponent>();
            var idlePowerRate = ResolveIdlePowerRate(param.IdlePowerRate);
            Assert.AreEqual(param.RequiredPower * idlePowerRate, electric.RequestEnergy.AsPrimitive(), 0.0001f);

            processor.Update();
            Assert.IsTrue(processor.IsMining);
            Assert.AreEqual(param.RequiredPower, electric.RequestEnergy.AsPrimitive(), 0.0001f);
        }

        [Test]
        public void GearBeltConveyorUsesIdleRateOnlyWhenEmptyTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 空のベルトはIdle扱いで要求トルクが低減される
            // An empty belt is idle, so required torque is reduced
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGearBeltConveyor, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var param = (GearBeltConveyorBlockParam)block.BlockMasterElement.BlockParam;
            var gear = block.GetComponent<GearBeltConveyorComponent>();
            var belt = block.GetComponent<VanillaBeltConveyorComponent>();
            var baseRpm = new RPM((float)param.GearConsumption.BaseRpm);
            var fullTorque = GearConsumptionCalculator.CalcRequiredTorque(param.GearConsumption, baseRpm).AsPrimitive();
            var idlePowerRate = ResolveIdlePowerRate(param.GearConsumption.IdlePowerRate);
            Assert.AreEqual(fullTorque * idlePowerRate, gear.GetRequiredTorque(baseRpm, true).AsPrimitive(), 0.0001f);

            // アイテムが載ると搬送中としてフル要求に戻る
            // Once an item is on the belt, it is active and requests full torque
            var item = ServerContext.ItemStackFactory.Create(new ItemId(1), 1);
            belt.InsertItem(item, InsertItemContext.Empty);
            Assert.AreEqual(fullTorque, gear.GetRequiredTorque(baseRpm, true).AsPrimitive(), 0.0001f);
        }

        [Test]
        public void GearMachineUsesIdleRateUntilProcessingStartsTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 加工中だけフル要求
            // Gear machines also request full torque only while processing
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearMachine, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var param = (GearMachineBlockParam)block.BlockMasterElement.BlockParam;
            var gear = block.GetComponent<GearEnergyTransformer>();
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            var baseRpm = new RPM((float)param.GearConsumption.BaseRpm);
            var fullTorque = GearConsumptionCalculator.CalcRequiredTorque(param.GearConsumption, baseRpm).AsPrimitive();
            var idlePowerRate = ResolveIdlePowerRate(param.GearConsumption.IdlePowerRate);
            Assert.AreEqual(fullTorque * idlePowerRate, gear.GetRequiredTorque(baseRpm, true).AsPrimitive(), 0.0001f);

            InsertRecipeInputs(block, GetMachineRecipe(ForUnitTestModBlockId.GearMachine));
            processor.Update();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(fullTorque, gear.GetRequiredTorque(baseRpm, true).AsPrimitive(), 0.0001f);
        }

        [Test]
        public void GearPumpUsesIdleRateWhenFluidCannotBeGeneratedTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 不一致鉱脈の歯車ポンプはIdle
            // A gear pump on a mismatched vein has no target fluid, so demand stays idle
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearPump, new Vector3Int(20, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var param = (GearPumpBlockParam)block.BlockMasterElement.BlockParam;
            var gear = block.GetComponent<GearEnergyTransformer>();
            var baseRpm = new RPM((float)param.GearConsumption.BaseRpm);
            var fullTorque = GearConsumptionCalculator.CalcRequiredTorque(param.GearConsumption, baseRpm).AsPrimitive();
            var idlePowerRate = ResolveIdlePowerRate(param.GearConsumption.IdlePowerRate);
            Assert.AreEqual(fullTorque * idlePowerRate, gear.GetRequiredTorque(baseRpm, true).AsPrimitive(), 0.0001f);
        }

        private static MachineRecipeMasterElement GetMachineRecipe()
        {
            return GetMachineRecipe(ForUnitTestModBlockId.MachineId);
        }

        private static MachineRecipeMasterElement GetMachineRecipe(BlockId blockId)
        {
            var machineGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            return MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(recipe => recipe.BlockGuid == machineGuid);
        }

        private static float ResolveIdlePowerRate(float? idlePowerRate)
        {
            return idlePowerRate ?? BlockMaster.DefaultIdlePowerRate;
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
