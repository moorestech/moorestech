using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.GearElectric;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class GearElectricGeneratorTest
    {
        [Test]
        public void OutputEnergyScalesWithGearSupply()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestGearElectricGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out var driveBlock);

            var generatorComponent = generatorBlock.GetComponent<GearElectricGeneratorComponent>();
            var driveComponent = driveBlock.GetComponent<SimpleGearGeneratorComponent>();
            var param = (GearElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearElectricGenerator).BlockParam;

            // フル出力
            // Full output
            driveComponent.SetGenerateRpm(param.RequiredRpm);
            driveComponent.SetGenerateTorque(param.RequiredTorque);
            AdvanceTime(0.5f);
            Assert.AreEqual(param.MaxGeneratedPower, generatorComponent.OutputEnergy().AsPrimitive(),  0.1f);
            Assert.AreEqual(1f, generatorComponent.EnergyFulfillmentRate, 0.05f);

            // 半分のRPM -> 半分の出力
            // Half RPM -> Half output
            driveComponent.SetGenerateRpm(param.RequiredRpm * 0.5f);
            driveComponent.SetGenerateTorque(param.RequiredTorque);
            AdvanceTime(0.5f);
            Assert.AreEqual(param.MaxGeneratedPower * 0.5f, generatorComponent.OutputEnergy().AsPrimitive(), 0.1f);
            Assert.AreEqual(0.5f, generatorComponent.EnergyFulfillmentRate, 0.05f);

            // 半分のトルク -> ネットワークが停止し出力は0
            // Half torque -> Network stops due to power shortage, output becomes 0
            driveComponent.SetGenerateRpm(param.RequiredRpm);
            driveComponent.SetGenerateTorque(param.RequiredTorque * 0.5f);
            AdvanceTime(0.5f);
            var gearNetwork = GearNetworkDatastore.GetGearNetwork(generatorComponent.BlockInstanceId);
            Assert.AreEqual(GearNetworkStopReason.OverRequirePower, gearNetwork.CurrentGearNetworkInfo.StopReason);
            Assert.AreEqual(0f, generatorComponent.OutputEnergy().AsPrimitive(), 0.01f);
            Assert.AreEqual(0f, generatorComponent.EnergyFulfillmentRate, 0.05f);

            // RPM 0 -> 出力 0
            // RPM 0 -> Output 0
            driveComponent.SetGenerateRpm(0f);
            AdvanceTime(0.5f);
            Assert.AreEqual(0f, generatorComponent.OutputEnergy().AsPrimitive(), 0.01f);
            Assert.AreEqual(0f, generatorComponent.EnergyFulfillmentRate, 0.05f);
        }

        #region Internal

        private static void AdvanceTime(double seconds)
        {
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < seconds)
            {
                GameUpdater.UpdateOneTick();
            }
        }

        #endregion
    }
}
