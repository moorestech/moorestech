using System;
using System.Linq;
using System.Collections.Generic;
using Game.Block.Blocks.Machine;
using Tests.Util.EnergySystem;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.GearToElectric;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Core.Gear
{
    public class GearToElectricGenerationStateTest
    {
        // 孤立網でも需要0のtickが届き、前回供給を消す
        // Even an isolated network gets zero-demand ticks and clears prior supply
        [Test]
        public void UnwiredGeneratorReportsIdleGeneratingAndStopped()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestGearToElectricGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out var driveBlock);
            var generator = generatorBlock.GetComponent<GearToElectricGeneratorComponent>();
            var drive = driveBlock.GetComponent<SimpleGearGeneratorComponent>();
            var param = (GearToElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearToElectricGenerator).BlockParam;

            // ブロックシステムと同じインターフェースから購読する
            // Subscribe through the same interface as the block system
            var changeCount = 0;
            generatorBlock.GetComponent<IBlockStateObservable>().OnChangeBlockState.Subscribe(_ => changeCount++);
            drive.SetGenerateRpm(param.GearConsumption.BaseRpm);
            drive.SetGenerateTorque(param.GearConsumption.BaseTorque);
            AdvanceTicks(30);
            Assert.AreEqual(GearToElectricGenerationState.Idle, ReadDetail().GenerationState);
            Assert.AreEqual(0f, ReadDetail().GeneratedElectricPower, 0.001f);

            // 人工需要後、実tickで供給をゼロへ
            // Clear supply on the real tick after injected demand
            var supply = generator.OutputEnergy().AsPrimitive();
            Assert.Greater(supply, 0f);
            var beforeDischarge = changeCount;
            generator.OnElectricTickPostProcess(new ElectricNetworkStatistics(supply, supply * 0.4f, 1f, 1));
            Assert.AreEqual(GearToElectricGenerationState.Generating, ReadDetail().GenerationState);
            Assert.AreEqual(supply * 0.4f, ReadDetail().GeneratedElectricPower, 0.01f);
            Assert.Greater(changeCount, beforeDischarge);
            var beforeTick = changeCount;
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(GearToElectricGenerationState.Idle, ReadDetail().GenerationState);
            Assert.AreEqual(0f, ReadDetail().GeneratedElectricPower, 0.001f);
            Assert.Greater(changeCount, beforeTick);

            // 回転を止めると状態が停止中へ変わる
            // Stopping rotation changes the state to stopped
            drive.SetGenerateRpm(0f);
            AdvanceTicks(30);
            Assert.AreEqual(GearToElectricGenerationState.Stopped, ReadDetail().GenerationState);

            #region Internal

            GearToElectricGeneratorBlockStateDetail ReadDetail()
            {
                var detail = generator.GetBlockStateDetails().First(d => d.Key == GearToElectricGeneratorBlockStateDetail.GearGeneratorBlockStateDetailKey);
                return MessagePackSerializer.Deserialize<GearToElectricGeneratorBlockStateDetail>(detail.Value);
            }

            void AdvanceTicks(int count)
            {
                for (var tick = 0; tick < count; tick++) GameUpdater.UpdateOneTick();
            }

            #endregion
        }

        // 電力確定後の回転停止を全通知で捕捉し、次の電力確定で停止へ移る
        // Capture every notification when rotation stops after power settlement, then verify the next settlement stops generation
        [Test]
        public void ElectricMeasurementPrecedesCurrentRpmAndSettlesByNextTick()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestGearToElectricGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out var driveBlock);
            var machinePosition = new Vector3Int(0, 0, 5);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, machinePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machineBlock);
            ElectricWireTestUtil.Connect(Vector3Int.zero, machinePosition);

            // 実需要・実供給を成立させてから駆動源を停止する
            // Establish actual demand and supply before stopping the drive
            var generator = generatorBlock.GetComponent<GearToElectricGeneratorComponent>();
            var drive = driveBlock.GetComponent<SimpleGearGeneratorComponent>();
            var param = (GearToElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearToElectricGenerator).BlockParam;
            drive.SetGenerateRpm(param.GearConsumption.BaseRpm);
            drive.SetGenerateTorque(param.GearConsumption.BaseTorque);
            for (var tick = 0; tick < 30; tick++) GameUpdater.UpdateOneTick();
            Assert.Greater(machineBlock.GetComponent<VanillaMachineProcessorComponent>().CurrentPower, 0f);
            Assert.AreEqual(GearToElectricGenerationState.Generating, ReadDetail().GenerationState);
            Assert.Greater(ReadDetail().GeneratedElectricPower, 0f);
            var snapshots = new List<GearToElectricGeneratorBlockStateDetail>();
            var stopAfterElectricMeasurement = true;
            using var subscription = generatorBlock.GetComponent<IBlockStateObservable>().OnChangeBlockState.Subscribe(_ =>
            {
                snapshots.Add(ReadDetail());
                if (!stopAfterElectricMeasurement) return;
                stopAfterElectricMeasurement = false;
                drive.SetGenerateRpm(0f);
            });

            // 需要の変化で電力通知を発生させ、その直後に回転を止める
            // Change actual demand to emit a power notification and stop rotation immediately afterward
            var extraMachinePosition = new Vector3Int(0, 0, 8);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, extraMachinePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(Vector3Int.zero, extraMachinePosition);
            GameUpdater.UpdateOneTick();

            // 現在RPMだけ先に止まり、直近計測の供給は失われない
            // Current RPM stops first while the latest measured supply remains intact
            foreach (var snapshot in snapshots)
                TestContext.WriteLine($"transition RPM={snapshot.CurrentRpm}, state={snapshot.GenerationState}, supply={snapshot.GeneratedElectricPower}");
            Assert.IsFalse(stopAfterElectricMeasurement);
            Assert.Greater(snapshots.First().CurrentRpm, 0f);
            var stoppedRotation = snapshots.Last();
            Assert.AreEqual(0f, stoppedRotation.CurrentRpm);
            Assert.AreEqual(GearToElectricGenerationState.Generating, stoppedRotation.GenerationState);
            Assert.Greater(stoppedRotation.GeneratedElectricPower, 0f);
            snapshots.Clear();
            GameUpdater.UpdateOneTick();
            Assert.IsNotEmpty(snapshots);
            Assert.AreEqual(GearToElectricGenerationState.Stopped, snapshots.Last().GenerationState);
            Assert.AreEqual(0f, snapshots.Last().CurrentRpm);

            #region Internal

            GearToElectricGeneratorBlockStateDetail ReadDetail()
            {
                var detail = generator.GetBlockStateDetails().First(d => d.Key == GearToElectricGeneratorBlockStateDetail.GearGeneratorBlockStateDetailKey);
                return MessagePackSerializer.Deserialize<GearToElectricGeneratorBlockStateDetail>(detail.Value);
            }

            #endregion
        }

    }
}
