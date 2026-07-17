using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.GearToElectric;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class GearToElectricGeneratorTest
    {
        [Test]
        public void BatteryChargesFromGearAndNeverExceedsMaxGeneratedPower()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestGearToElectricGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out var driveBlock);

            var generatorComponent = generatorBlock.GetComponent<GearToElectricGeneratorComponent>();
            var driveComponent = driveBlock.GetComponent<SimpleGearGeneratorComponent>();
            var param = (GearToElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearToElectricGenerator).BlockParam;

            // フル回転で1tick分のバッテリーが満充電され、申告供給はmaxGeneratedPowerに一致する
            // Full rotation fully charges the one-tick battery; the declared supply equals maxGeneratedPower
            driveComponent.SetGenerateRpm(param.GearConsumption.BaseRpm);
            driveComponent.SetGenerateTorque(param.GearConsumption.BaseTorque);
            AdvanceTime(0.5f);
            Assert.AreEqual(param.MaxGeneratedPower, generatorComponent.OutputEnergy().AsPrimitive(), 0.1f);

            // オーバードライブ廃止: 定格の2倍で回してもmaxGeneratedPowerを超えて発電しない
            // Overdrive abolished: spinning at twice the rated RPM never generates beyond maxGeneratedPower
            DischargeAll();
            driveComponent.SetGenerateRpm(param.GearConsumption.BaseRpm * 2f);
            // 2倍RPMでは消費トルクが指数的に増えるため、網が停止しないようトルク供給も引き上げる
            // Torque demand grows exponentially at double RPM, so raise the torque supply to keep the network running
            driveComponent.SetGenerateTorque(param.GearConsumption.BaseTorque * 3.2f);
            AdvanceTime(0.5f);
            Assert.LessOrEqual(generatorComponent.OutputEnergy().AsPrimitive(), param.MaxGeneratedPower + 0.01f);
            Assert.AreEqual(param.MaxGeneratedPower, generatorComponent.OutputEnergy().AsPrimitive(), 0.1f);

            // 統計確定後の放電は利用率に応じる: 需要が供給の40%なら残量100%→60%
            // Post-settlement discharge follows utilization: demand at 40% of supply drops the battery from 100% to 60%
            var beforeDischarge = generatorComponent.OutputEnergy().AsPrimitive();
            generatorComponent.OnElectricTickPostProcess(new ElectricNetworkStatistics(beforeDischarge, beforeDischarge * 0.4f, 1f, 1));
            Assert.AreEqual(beforeDischarge * 0.6f, generatorComponent.OutputEnergy().AsPrimitive(), 0.01f);

            // RPM 0では充電しない（残量は維持される）
            // No charging at RPM 0; the remainder is kept as-is
            var keptRemaining = generatorComponent.OutputEnergy().AsPrimitive();
            driveComponent.SetGenerateRpm(0f);
            AdvanceTime(0.5f);
            Assert.AreEqual(keptRemaining, generatorComponent.OutputEnergy().AsPrimitive(), 0.01f);

            #region Internal

            // 需要=供給の統計を渡し利用率100%で全放電させる
            // Pass demand-equals-supply statistics to discharge fully at 100% utilization
            void DischargeAll()
            {
                var remaining = generatorComponent.OutputEnergy().AsPrimitive();
                if (remaining <= 0f) return;
                generatorComponent.OnElectricTickPostProcess(new ElectricNetworkStatistics(remaining, remaining, 1f, 1));
            }

            #endregion
        }

        [Test]
        public void TorqueShortageStopsNetworkAndChargesNothing()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestGearToElectricGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out var driveBlock);

            var generatorComponent = generatorBlock.GetComponent<GearToElectricGeneratorComponent>();
            var driveComponent = driveBlock.GetComponent<SimpleGearGeneratorComponent>();
            var param = (GearToElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearToElectricGenerator).BlockParam;

            // トルク不足でネットワークが停止し、バッテリーは空のまま
            // Torque shortage stops the network, leaving the battery empty
            driveComponent.SetGenerateRpm(param.GearConsumption.BaseRpm);
            driveComponent.SetGenerateTorque(param.GearConsumption.BaseTorque * 0.5f);
            AdvanceTime(0.5f);
            var gearNetwork = GearNetworkDatastoreReflectionTestUtil.GetAppliedNetwork(generatorComponent.BlockInstanceId);
            Assert.AreEqual(GearNetworkStopReason.OverRequirePower, gearNetwork.CurrentGearNetworkInfo.StopReason);
            Assert.AreEqual(0f, generatorComponent.OutputEnergy().AsPrimitive(), 0.01f);
        }

        private static void AdvanceTime(double seconds)
        {
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < seconds)
            {
                GameUpdater.UpdateOneTick();
            }
        }

    }
}
