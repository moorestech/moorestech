using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.TrainRail.ContainerComponents;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Fluid;
using MessagePack;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class TrainFluidPlatformStateDetailTest
    {
        private static readonly Guid WaterGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        // 空のPFは空の出力タンクを1本出す
        // An empty platform publishes one empty output tank
        [Test]
        public void EmptyPlatformPublishesOneEmptyOutputTank()
        {
            var component = PlacePlatform();
            var detail = ReadDetail(component);

            Assert.AreEqual(0, detail.InputTanks.Count);
            Assert.AreEqual(1, detail.OutputTanks.Count);
            Assert.AreEqual(FluidMaster.EmptyFluidId.AsPrimitive(), detail.OutputTanks[0].FluidId);
            Assert.AreEqual(0, detail.OutputTanks[0].Amount, 0.001);
            Assert.Greater(detail.OutputTanks[0].MaxCapacity, 0);
        }

        // 注入すると量が状態に載り、変化通知が1回出る
        // Injecting fluid shows up in the state and fires one change notification
        [Test]
        public void AddLiquidPublishesAmountAndNotifies()
        {
            var component = PlacePlatform();
            var notifyCount = 0;
            component.OnChangeBlockState.Subscribe(_ => notifyCount++);

            var waterId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            component.AddLiquid(new FluidStack(300.0, waterId), default);

            var tank = ReadDetail(component).OutputTanks[0];
            Assert.AreEqual(waterId.AsPrimitive(), tank.FluidId);
            Assert.AreEqual(300.0, tank.Amount, 0.001);
            Assert.AreEqual(1, notifyCount);
        }

        // 満杯で何も入らなかったときは通知しない
        // No notification when nothing is accepted because the tank is full
        [Test]
        public void RejectedAddLiquidDoesNotNotify()
        {
            var component = PlacePlatform();
            var waterId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            var capacity = ReadDetail(component).OutputTanks[0].MaxCapacity;
            component.AddLiquid(new FluidStack(capacity, waterId), default);

            var notifyCount = 0;
            component.OnChangeBlockState.Subscribe(_ => notifyCount++);
            component.AddLiquid(new FluidStack(10.0, waterId), default);

            Assert.AreEqual(0, notifyCount);
        }

        #region Helper

        private static TrainPlatformFluidContainerComponent PlacePlatform()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var (platformBlock, _) = TrainTestHelper.PlaceBlockWithRailComponents(env, ForUnitTestModBlockId.TestTrainFluidPlatform, Vector3Int.zero, BlockDirection.North);
            return platformBlock.GetComponent<TrainPlatformFluidContainerComponent>();
        }

        private static FluidMachineInventoryStateDetail ReadDetail(TrainPlatformFluidContainerComponent component)
        {
            var details = component.GetBlockStateDetails();
            Assert.AreEqual(1, details.Length);
            Assert.AreEqual(FluidMachineInventoryStateDetail.BlockStateDetailKey, details[0].Key);
            return MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(details[0].Value);
        }

        #endregion
    }
}
