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
            world.TryAddBlock(ForUnitTestModBlockId.TestGearElectricGenerator, Vector3Int.zero, BlockDirection.North, out var generatorBlock);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, out var driveBlock);

            var generatorComponent = generatorBlock.GetComponent<GearElectricGeneratorComponent>();
            var driveComponent = driveBlock.GetComponent<SimpleGearGeneratorComponent>();
            var param = (GearElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearElectricGenerator).BlockParam;

            // フルフィルメント
            driveComponent.SetGenerateRpm(param.RequiredRpm);
            driveComponent.SetGenerateTorque(param.RequiredTorque);
            AdvanceTime(1.0f);
            Assert.That(generatorComponent.OutputEnergy().AsPrimitive(), Is.EqualTo(param.MaxGeneratedPower).Within(param.MaxGeneratedPower * 0.05f));
            Assert.That(generatorComponent.EnergyFulfillmentRate, Is.EqualTo(1f).Within(0.05f));

            // 半分のRPM -> 半分の出力
            driveComponent.SetGenerateRpm(param.RequiredRpm * 0.5f);
            driveComponent.SetGenerateTorque(param.RequiredTorque);
            AdvanceTime(1.0f);
            Assert.That(generatorComponent.OutputEnergy().AsPrimitive(), Is.EqualTo(param.MaxGeneratedPower * 0.5f).Within(param.MaxGeneratedPower * 0.1f));

            // トルク0 -> 出力0
            driveComponent.SetGenerateTorque(0f);
            AdvanceTime(0.5f);
            Assert.That(generatorComponent.OutputEnergy().AsPrimitive(), Is.EqualTo(0f).Within(0.01f));
        }

        #region Internal

        private static void AdvanceTime(double seconds)
        {
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < seconds)
            {
                GameUpdater.UpdateWithWait();
            }
        }

        #endregion
    }
}
