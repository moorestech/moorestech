using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Fluid;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Core.Electric
{
    public class ElectricGeneratorStateDetailTest
    {
        [Test]
        public void 液体燃料で燃焼中は稼働率と燃料タンクを状態詳細に出す()
        {
            var (generator, fuel, fluidId) = Setup();
            var notified = 0;
            generator.OnChangeBlockState.Subscribe(_ => notified++);

            // 注ぐだけでタンクの変化を通知する
            // Pouring alone notifies the tank change
            generator.AddLiquid(new FluidStack(fuel.Amount * 3, fluidId), default);
            Assert.AreEqual(1, notified);

            // 1tick進めると1単位が点き、タンクに2単位残る
            // One tick ignites one unit and leaves two in the tank
            GameUpdater.UpdateOneTick();

            var (power, tank) = ReadDetails(generator);
            Assert.AreEqual(1f, power.OperatingRate);
            Assert.AreEqual(fuel.Time, power.CurrentFuelTime, 0.01);
            Assert.AreEqual(fuel.Amount * 2, tank.Amount, 0.001);
            Assert.AreEqual(2, notified);
        }

        [Test]
        public void 燃焼中は毎tick残燃料を通知する()
        {
            var (generator, fuel, fluidId) = Setup();
            generator.AddLiquid(new FluidStack(fuel.Amount, fluidId), default);
            GameUpdater.UpdateOneTick();
            var (before, _) = ReadDetails(generator);

            var notified = 0;
            generator.OnChangeBlockState.Subscribe(_ => notified++);
            GameUpdater.RunFrames(3);

            // 残燃料バーが進むよう、燃えている間は毎tick通知する
            // Notify every tick while burning so the remaining-fuel bar advances
            var (after, _) = ReadDetails(generator);
            Assert.AreEqual(3, notified);
            Assert.Less(after.RemainingFuelTime, before.RemainingFuelTime);
        }

        [Test]
        public void 燃え尽きたら稼働率0になり通知が止まる()
        {
            var (generator, fuel, fluidId) = Setup();
            generator.AddLiquid(new FluidStack(fuel.Amount, fluidId), default);
            GameUpdater.UpdateOneTick();

            var fuelTicks = GameUpdater.SecondsToTicks(fuel.Time);
            for (var i = 0; i < fuelTicks + 1; i++) GameUpdater.RunFrames(1);

            // 燃え尽きた後は通知が止まる
            // Notifications stop once the fuel has burned out
            var notified = 0;
            generator.OnChangeBlockState.Subscribe(_ => notified++);
            GameUpdater.RunFrames(3);

            var (power, _) = ReadDetails(generator);
            Assert.AreEqual(0f, power.OperatingRate);
            Assert.AreEqual(0, notified);
        }

        private static (VanillaElectricGeneratorComponent, FuelFluidsElement, FluidId) Setup()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GeneratorId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block));
            var param = (ElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GeneratorId).BlockParam;
            var fuel = param.FuelFluids[0];
            return (block.GetComponent<VanillaElectricGeneratorComponent>(), fuel, MasterHolder.FluidMaster.GetFluidId(fuel.FluidGuid));
        }

        private static (PowerGeneratorStateDetail, FluidMessagePack) ReadDetails(VanillaElectricGeneratorComponent generator)
        {
            var details = generator.GetBlockStateDetails();
            var power = MessagePackSerializer.Deserialize<PowerGeneratorStateDetail>(details.First(d => d.Key == PowerGeneratorStateDetail.StateDetailKey).Value);
            var fluid = MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(details.First(d => d.Key == FluidMachineInventoryStateDetail.BlockStateDetailKey).Value);
            return (power, fluid.InputTanks[0]);
        }
    }
}
