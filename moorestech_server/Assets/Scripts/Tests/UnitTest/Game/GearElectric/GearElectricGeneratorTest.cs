using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.GearElectric;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Gear.Common;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.GearElectric
{
    /// <summary>
    /// 歯車発電機のテスト
    /// TDDに従って、最初に失敗するテストを書き、実装を進める
    /// </summary>
    public class GearElectricGeneratorTest
    {
        [Test]
        public void EnergyFulfillmentRate_100Percent_GeneratesMaxPower()
        {
            // Arrange
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            // 歯車発電機を設置
            worldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.TestGearElectricGenerator,
                Vector3Int.zero,
                BlockDirection.North,
                out var generatorBlock);

            var component = generatorBlock.GetComponent<GearElectricGeneratorComponent>();
            var gearTransformer = generatorBlock.GetComponent<IGearEnergyTransformer>();
            var electricGenerator = generatorBlock.GetComponent<IElectricGenerator>();

            // パラメータを取得
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearElectricGenerator);
            var param = blockMaster.BlockParam as GearElectricGeneratorBlockParam;

            // Act
            // 要求RPMと要求トルクと同じ値を供給
            gearTransformer.SupplyPower(new RPM(param.RequiredRpm), new Torque(param.RequiredTorque), true);

            // アップデート
            GameUpdater.UpdateWithWait();

            // Assert
            var generatedPower = electricGenerator.OutputEnergy();
            Assert.AreEqual(param.MaxGeneratedPower, generatedPower.AsPrimitive(), 0.01f,
                "エネルギー充足率100%で最大発電量が出力されません");
        }

        [Test]
        public void EnergyFulfillmentRate_50Percent_GeneratesHalfPower()
        {
            // Arrange
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.TestGearElectricGenerator,
                Vector3Int.zero,
                BlockDirection.North,
                out var generatorBlock);

            var component = generatorBlock.GetComponent<GearElectricGeneratorComponent>();
            var gearTransformer = generatorBlock.GetComponent<IGearEnergyTransformer>();
            var electricGenerator = generatorBlock.GetComponent<IElectricGenerator>();

            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearElectricGenerator);
            var param = blockMaster.BlockParam as GearElectricGeneratorBlockParam;

            // Act
            // RPMは半分、トルクは要求値と同じを供給（充足率50%）
            gearTransformer.SupplyPower(
                new RPM(param.RequiredRpm / 2),
                new Torque(param.RequiredTorque),
                true);

            GameUpdater.UpdateWithWait();

            // Assert
            var generatedPower = electricGenerator.OutputEnergy();
            var expectedPower = param.MaxGeneratedPower * 0.5f;
            Assert.AreEqual(expectedPower, generatedPower.AsPrimitive(), 0.01f,
                "エネルギー充足率50%で発電量が50%になりません");
        }

        [Test]
        public void NoEnergyInput_GeneratesZeroPower()
        {
            // Arrange
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.TestGearElectricGenerator,
                Vector3Int.zero,
                BlockDirection.North,
                out var generatorBlock);

            var electricGenerator = generatorBlock.GetComponent<IElectricGenerator>();

            // Act
            // 何も入力しない（初期状態）
            GameUpdater.UpdateWithWait();

            // Assert
            var generatedPower = electricGenerator.OutputEnergy();
            Assert.AreEqual(0f, generatedPower.AsPrimitive(),
                "入力がない場合、発電量は0になるべきです");
        }

        [Test]
        public void EnergyFulfillmentRate_Over100Percent_ClipsToMaxPower()
        {
            // Arrange
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.TestGearElectricGenerator,
                Vector3Int.zero,
                BlockDirection.North,
                out var generatorBlock);

            var gearTransformer = generatorBlock.GetComponent<IGearEnergyTransformer>();
            var electricGenerator = generatorBlock.GetComponent<IElectricGenerator>();

            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearElectricGenerator);
            var param = blockMaster.BlockParam as GearElectricGeneratorBlockParam;

            // Act
            // 要求値の2倍を供給（充足率200%）
            gearTransformer.SupplyPower(
                new RPM(param.RequiredRpm * 2),
                new Torque(param.RequiredTorque * 2),
                true);

            GameUpdater.UpdateWithWait();

            // Assert
            var generatedPower = electricGenerator.OutputEnergy();
            Assert.AreEqual(param.MaxGeneratedPower, generatedPower.AsPrimitive(), 0.01f,
                "エネルギー充足率が100%を超えても発電量は最大値でクリップされるべきです");
        }

        [Test]
        public void ZeroRpmInput_GeneratesZeroPower()
        {
            // Arrange
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.TestGearElectricGenerator,
                Vector3Int.zero,
                BlockDirection.North,
                out var generatorBlock);

            var gearTransformer = generatorBlock.GetComponent<IGearEnergyTransformer>();
            var electricGenerator = generatorBlock.GetComponent<IElectricGenerator>();

            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearElectricGenerator);
            var param = blockMaster.BlockParam as GearElectricGeneratorBlockParam;

            // Act
            // RPMは0、トルクは要求値を供給
            gearTransformer.SupplyPower(
                new RPM(0),
                new Torque(param.RequiredTorque),
                true);

            GameUpdater.UpdateWithWait();

            // Assert
            var generatedPower = electricGenerator.OutputEnergy();
            Assert.AreEqual(0f, generatedPower.AsPrimitive(),
                "RPMが0の場合、発電量は0になるべきです");
        }

        [Test]
        public void ZeroTorqueInput_GeneratesZeroPower()
        {
            // Arrange
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.TestGearElectricGenerator,
                Vector3Int.zero,
                BlockDirection.North,
                out var generatorBlock);

            var gearTransformer = generatorBlock.GetComponent<IGearEnergyTransformer>();
            var electricGenerator = generatorBlock.GetComponent<IElectricGenerator>();

            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearElectricGenerator);
            var param = blockMaster.BlockParam as GearElectricGeneratorBlockParam;

            // Act
            // RPMは要求値、トルクは0を供給
            gearTransformer.SupplyPower(
                new RPM(param.RequiredRpm),
                new Torque(0),
                true);

            GameUpdater.UpdateWithWait();

            // Assert
            var generatedPower = electricGenerator.OutputEnergy();
            Assert.AreEqual(0f, generatedPower.AsPrimitive(),
                "トルクが0の場合、発電量は0になるべきです");
        }

        [Test]
        public void DynamicInputChange_UpdatesGeneratedPower()
        {
            // Arrange
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.TestGearElectricGenerator,
                Vector3Int.zero,
                BlockDirection.North,
                out var generatorBlock);

            var gearTransformer = generatorBlock.GetComponent<IGearEnergyTransformer>();
            var electricGenerator = generatorBlock.GetComponent<IElectricGenerator>();

            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearElectricGenerator);
            var param = blockMaster.BlockParam as GearElectricGeneratorBlockParam;

            // Act & Assert
            // 初期状態：発電なし
            GameUpdater.UpdateWithWait();
            Assert.AreEqual(0f, electricGenerator.OutputEnergy().AsPrimitive());

            // 50%充足率で供給
            gearTransformer.SupplyPower(
                new RPM(param.RequiredRpm / 2),
                new Torque(param.RequiredTorque),
                true);
            GameUpdater.UpdateWithWait();

            var power50 = electricGenerator.OutputEnergy();
            Assert.AreEqual(param.MaxGeneratedPower * 0.5f, power50.AsPrimitive(), 0.01f,
                "50%充足率で適切に発電されません");

            // 100%充足率に変更
            gearTransformer.SupplyPower(
                new RPM(param.RequiredRpm),
                new Torque(param.RequiredTorque),
                true);
            GameUpdater.UpdateWithWait();

            var power100 = electricGenerator.OutputEnergy();
            Assert.AreEqual(param.MaxGeneratedPower, power100.AsPrimitive(), 0.01f,
                "100%充足率で適切に発電されません");

            // 再び0に戻す
            gearTransformer.SupplyPower(
                new RPM(0),
                new Torque(0),
                true);
            GameUpdater.UpdateWithWait();

            var power0 = electricGenerator.OutputEnergy();
            Assert.AreEqual(0f, power0.AsPrimitive(),
                "入力を0に戻した時、発電も0になるべきです");
        }
    }
}