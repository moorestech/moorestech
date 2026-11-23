using System;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Tests.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Source;
using Core.Master;
using Game.Block.Blocks.GearToElectric;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.GearConsumptionModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Special.Source
{
    /// <summary>
    /// 歯車と回転発電機の詳細を実マスタとサーバーの状態から読む規則を確かめる（L0）
    /// Verifies reading gear and gear-to-electric details from the real master and the server state (L0)
    /// </summary>
    public class SpecialBlockDetailReaderGearTest
    {
        private TrackedBlockHarness _harness;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _harness = new TrackedBlockHarness();
        }

        [TearDown]
        public void TearDown()
        {
            _harness.Dispose();
        }

        [Test]
        public void 消費側の基準RPMは実マスタの値で役割はサーバーに従う()
        {
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearMachine);
            Assert.AreEqual(10f, (float)((IGearConsumptionParam)master.BlockParam).GearConsumption.BaseRpm, 0.001f, "テスト前提: 歯車機械の基準 RPM は 10");

            var consumer = Read(master, new GearStateDetail(true, 12.5f, 3f, GearRole.Consumer), out var consumerAnomaly);
            Assert.IsTrue(consumer.TryGetGear(out var consumerGear));
            Assert.AreEqual(10.0, ((ConsumerGearDetail)consumerGear).BaseRpm, 0.001);
            Assert.AreEqual(12.5, consumerGear.CurrentRpm, 0.001);
            Assert.IsNull(consumerAnomaly);

            // 消費パラメータを持つマスタでも、サーバーが発電機と言えば基準 RPM を持たない
            // Even with a consumption param, a server-declared generator carries no base RPM
            var generator = Read(master, new GearStateDetail(true, 20f, 5f, GearRole.Generator), out _);
            Assert.IsTrue(generator.TryGetGear(out var generatorGear));
            Assert.IsInstanceOf<GeneratorGearDetail>(generatorGear);
        }

        [Test]
        public void 消費パラメータの無いマスタで消費側と言われたら歯車行を出さず食い違いを返す()
        {
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.SimpleGearGenerator);

            var detail = Read(master, new GearStateDetail(true, 1f, 2f, GearRole.Consumer), out var anomaly);

            Assert.IsFalse(detail.TryGetGear(out _));
            StringAssert.Contains("GearRole.Consumer", anomaly);
        }

        [Test]
        public void 回転発電機の最大値は実マスタから引き供給と状態はサーバーの値()
        {
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearToElectricGenerator);
            Assert.AreEqual(100f, ((GearToElectricGeneratorBlockParam)master.BlockParam).MaxGeneratedPower, 0.001f, "テスト前提: 最大発電は 100");
            var state = new GearToElectricGeneratorBlockStateDetail(true, new RPM(120f), new Torque(100f), new ElectricPower(72f), GearToElectricGenerationState.Generating);
            var block = _harness.CreateBlock(Vector3Int.zero, master, (GearToElectricGeneratorBlockStateDetail.GearGeneratorBlockStateDetailKey, state));

            var detail = SpecialBlockDetailReader.Read(block, new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), Array.Empty<ItemId>(), out _);

            Assert.IsTrue(detail.TryGetGearToElectric(out var generator));
            Assert.AreEqual(100.0, generator.MaxGeneratedPower, 0.001);
            Assert.AreEqual(72.0, generator.SuppliedPower, 0.001);
            Assert.AreEqual(GearToElectricDisplayState.Generating, generator.State);
        }

        private SpecialBlockDetailViewModel Read(BlockMasterElement master, GearStateDetail gear, out string anomaly)
        {
            var block = _harness.CreateBlock(Vector3Int.zero, master, (GearStateDetail.BlockStateDetailKey, gear));
            return SpecialBlockDetailReader.Read(block, new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), Array.Empty<ItemId>(), out anomaly);
        }
    }
}
