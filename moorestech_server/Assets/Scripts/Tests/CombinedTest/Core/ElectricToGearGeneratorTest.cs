using System;
using System.IO;
using System.Reflection;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class ElectricToGearGeneratorTest
    {
        [Test]
        public void FullChargeOutputsRatedAndPartialChargePulses()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestElectricToGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var c = block.GetComponent<ElectricToGearGeneratorComponent>();
            var param = (ElectricToGearGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestElectricToGearGenerator).BlockParam;

            var mode0 = param.OutputModes[0]; // rpm 60, torque 50, power 30
            var mode1 = param.OutputModes[1]; // rpm 120, torque 100, power 100

            // 一定電力を常時要求し、初期はバッテリー空で出力なし
            // The converter constantly demands a fixed power; the battery starts empty with no output
            Assert.AreEqual((float)mode0.RequiredPower, c.RequestEnergy.AsPrimitive(), 0.001f);
            Assert.AreEqual(0f, c.GenerateRpm.AsPrimitive(), 0.001f);

            // 供給率1の電力tickで満充電になり、定格RPM・定格トルクを出力する
            // A rate-1 electric tick fully charges the battery, outputting the rated RPM and torque
            c.OnElectricTickPostProcess(FullRateStats(mode0.RequiredPower));
            Assert.AreEqual((float)mode0.Rpm, c.GenerateRpm.AsPrimitive(), 0.001f);
            Assert.AreEqual((float)mode0.Torque, c.GenerateTorque.AsPrimitive(), 0.01f);

            // 出力tickで1tick分のバッテリーを全消費し残量0・出力停止
            // The output tick consumes the whole one-tick battery, stopping the output at zero remainder
            c.ConsumeGeneratorTick(1f);
            Assert.AreEqual(0f, c.GenerateRpm.AsPrimitive(), 0.001f);
            Assert.AreEqual(0f, c.GenerateTorque.AsPrimitive(), 0.01f);

            // 供給率0.5では1tick目は充電のみで出力なし、2tick目で満充電になり定格出力（脈動）
            // At rate 0.5 the first tick only charges with no output; the second tick reaches full and outputs the rated values (pulsing)
            c.OnElectricTickPostProcess(HalfRateStats(mode0.RequiredPower));
            Assert.AreEqual(0f, c.GenerateTorque.AsPrimitive(), 0.01f);
            c.OnElectricTickPostProcess(HalfRateStats(mode0.RequiredPower));
            Assert.AreEqual((float)mode0.Torque, c.GenerateTorque.AsPrimitive(), 0.01f);
            c.ConsumeGeneratorTick(1f);

            // 部分充電のままモードを切り替えると要求電力が新モードへ変わり、満充電まで出力しない
            // Switching modes while partially charged updates the demand and keeps the output off until fully charged
            c.OnElectricTickPostProcess(HalfRateStats(mode0.RequiredPower));
            c.SetSelectedMode(1);
            Assert.AreEqual((float)mode1.RequiredPower, c.RequestEnergy.AsPrimitive(), 0.001f);
            Assert.AreEqual(0f, c.GenerateTorque.AsPrimitive(), 0.01f);

            // 供給率1の電力tickで満充電になり、新モードの定格を出力（トルクドループなし）
            // A rate-1 electric tick fills the battery and outputs the new mode rated values (no torque droop)
            c.OnElectricTickPostProcess(FullRateStats(mode1.RequiredPower));
            Assert.AreEqual((float)mode1.Torque, c.GenerateTorque.AsPrimitive(), 0.01f);
            Assert.AreEqual((float)mode1.Rpm, c.GenerateRpm.AsPrimitive(), 0.001f);

            Assert.IsFalse(c.SetSelectedMode(99));
            Assert.AreEqual(1, c.SelectedIndex);
            Assert.IsFalse(c.SetSelectedMode(-1));
            Assert.AreEqual(1, c.SelectedIndex);

            // 出力後に供給率0が続くと充電されず出力は止まったまま
            // After the output tick, continued rate-0 ticks charge nothing and the output stays off
            c.ConsumeGeneratorTick(1f);
            c.OnElectricTickPostProcess(ZeroRateStats(mode1.RequiredPower));
            Assert.AreEqual(0f, c.GenerateTorque.AsPrimitive(), 0.01f);
            Assert.AreEqual(0f, c.GenerateRpm.AsPrimitive(), 0.001f);

            #region Internal

            // 供給率のみが充電量を決めるため、統計は率だけ変えた固定形で渡す
            // Only the supply rate drives charging, so the statistics are fixed shapes varying just the rate
            ElectricNetworkStatistics FullRateStats(double requiredPower) => new((float)requiredPower, (float)requiredPower, 1f, 1);
            ElectricNetworkStatistics HalfRateStats(double requiredPower) => new((float)requiredPower * 0.5f, (float)requiredPower, 0.5f, 1);
            ElectricNetworkStatistics ZeroRateStats(double requiredPower) => new(0f, (float)requiredPower, 0f, 1);

            #endregion
        }

        [Test]
        public void SelectedIndexSurvivesSaveLoad()
        {
            var (_, saveServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestElectricToGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            block.GetComponent<ElectricToGearGeneratorComponent>().SetSelectedMode(2);

            ChangeFilePath(saveServiceProvider.GetService<SaveJsonFilePath>(), "ElectricToGearSaveLoadTest.json");
            saveServiceProvider.GetService<IWorldSaveDataSaver>().Save();

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ChangeFilePath(loadServiceProvider.GetService<SaveJsonFilePath>(), "ElectricToGearSaveLoadTest.json");
            loadServiceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize();

            var reloaded = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero);
            File.Delete(saveServiceProvider.GetService<SaveJsonFilePath>().Path);

            Assert.AreEqual(2, reloaded.GetComponent<ElectricToGearGeneratorComponent>().SelectedIndex);
        }

        [Test]
        public void UnpoweredMotorDoesNotDominateGearNetwork()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.TestElectricToGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var motorBlock);
            var motor = motorBlock.GetComponent<ElectricToGearGeneratorComponent>();
            motor.SetSelectedMode(2); // rpm 240 but unpowered → fulfillment 0

            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out var driveBlock);

            GameUpdater.UpdateOneTick();

            Assert.AreNotEqual(240f, motor.CurrentRpm.AsPrimitive());
            Assert.AreEqual(0f, motor.GenerateRpm.AsPrimitive(), 0.001f);
        }

        [Test]
        public void TorqueZeroModeKeepsRpmZeroEvenWhenFullyPowered()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestElectricToGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var c = block.GetComponent<ElectricToGearGeneratorComponent>();
            var param = (ElectricToGearGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestElectricToGearGenerator).BlockParam;

            // index 3 = rpm 60, torque 0, requiredPower 10。トルク0モードを満充足にする。
            // index 3 is the torque-0 mode; supply it to full fulfillment.
            var mode3 = param.OutputModes[3];
            c.SetSelectedMode(3);
            c.OnElectricTickPostProcess(new ElectricNetworkStatistics((float)mode3.RequiredPower, (float)mode3.RequiredPower, 1f, 1));

            // 満充足でもトルク0なら GenerateRpm は0（実効トルクゲート）。網の最速起点を奪わない。
            // Even at full fulfillment, a torque-0 mode yields GenerateRpm 0 (effective-torque gate); never dominates the network.
            Assert.AreEqual(0f, c.GenerateTorque.AsPrimitive(), 0.001f);
            Assert.AreEqual(0f, c.GenerateRpm.AsPrimitive(), 0.001f);
        }

        [Test]
        public void ModeSwitchFiresOnChangeBlockState()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestElectricToGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var observable = block.GetComponent<IBlockStateObservable>();
            var fired = 0;
            using var _ = observable.OnChangeBlockState.Subscribe(__ => fired++);

            var before = fired;
            block.GetComponent<ElectricToGearGeneratorComponent>().SetSelectedMode(2);
            Assert.Greater(fired, before, "SetSelectedMode で OnChangeBlockState が発火していない");
        }

        private void ChangeFilePath(SaveJsonFilePath instance, string fileName)
        {
            var fieldInfo = typeof(SaveJsonFilePath).GetField("<Path>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var path = Path.Combine(Environment.CurrentDirectory, "../", "moorestech_server", fileName);
            fieldInfo.SetValue(instance, path);
        }
    }
}
