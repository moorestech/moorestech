using System;
using System.IO;
using System.Reflection;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Interface;
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
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class ElectricToGearGeneratorTest
    {
        [Test]
        public void FixedRpmTorqueDroopsAndModeSwitch()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestElectricToGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var c = block.GetComponent<ElectricToGearGeneratorComponent>();
            var param = (ElectricToGearGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestElectricToGearGenerator).BlockParam;

            var mode0 = param.OutputModes[0]; // rpm 60, torque 50, power 30
            var mode1 = param.OutputModes[1]; // rpm 120, torque 100, power 100

            Assert.AreEqual((float)mode0.RequiredPower, c.RequestEnergy.AsPrimitive(), 0.001f);
            Assert.AreEqual(0f, c.GenerateRpm.AsPrimitive(), 0.001f);

            c.SupplyEnergy(new ElectricPower((float)mode0.RequiredPower));
            Assert.AreEqual((float)mode0.Rpm, c.GenerateRpm.AsPrimitive(), 0.001f);
            Assert.AreEqual((float)mode0.Torque, c.GenerateTorque.AsPrimitive(), 0.01f);

            c.SupplyEnergy(new ElectricPower((float)mode0.RequiredPower * 0.5f));
            Assert.AreEqual((float)mode0.Rpm, c.GenerateRpm.AsPrimitive(), 0.001f);
            Assert.AreEqual((float)mode0.Torque * 0.5f, c.GenerateTorque.AsPrimitive(), 0.01f);

            c.SupplyEnergy(new ElectricPower(0));
            Assert.AreEqual(0f, c.GenerateTorque.AsPrimitive(), 0.01f);
            Assert.AreEqual(0f, c.GenerateRpm.AsPrimitive(), 0.001f);

            c.SupplyEnergy(new ElectricPower((float)mode0.RequiredPower));
            c.SetSelectedMode(1);

            Assert.AreEqual((float)mode1.RequiredPower, c.RequestEnergy.AsPrimitive(), 0.001f);

            var expectedFulfillment = (float)mode0.RequiredPower / (float)mode1.RequiredPower;
            Assert.AreEqual((float)mode1.Torque * expectedFulfillment, c.GenerateTorque.AsPrimitive(), 0.5f);
            Assert.AreEqual((float)mode1.Rpm, c.GenerateRpm.AsPrimitive(), 0.001f);

            Assert.IsFalse(c.SetSelectedMode(99));
            Assert.AreEqual(1, c.SelectedIndex);
            Assert.IsFalse(c.SetSelectedMode(-1));
            Assert.AreEqual(1, c.SelectedIndex);

            c.SupplyEnergy(new ElectricPower((float)mode1.RequiredPower));
            c.Update();
            c.Update();
            Assert.AreEqual(0f, c.GenerateTorque.AsPrimitive(), 0.01f);
            Assert.AreEqual(0f, c.GenerateRpm.AsPrimitive(), 0.001f);
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

        private void ChangeFilePath(SaveJsonFilePath instance, string fileName)
        {
            var fieldInfo = typeof(SaveJsonFilePath).GetField("<Path>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var path = Path.Combine(Environment.CurrentDirectory, "../", "moorestech_server", fileName);
            fieldInfo.SetValue(instance, path);
        }
    }
}
