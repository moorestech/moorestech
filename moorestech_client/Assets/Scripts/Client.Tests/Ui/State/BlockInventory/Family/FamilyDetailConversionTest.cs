using System.Collections.Generic;
using System.Linq;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// 族の状態詳細から詳細を組む変換
    /// Conversions from miner, pump and generator state details; fail-closed on unreadable values
    /// </summary>
    public class FamilyDetailConversionTest
    {
        private IMinerParam _minerParam;
        private ItemId _firstMinedItem;
        private ItemId _secondMinedItem;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            // 2 品以上を採掘できる採掘機のマスタ
            // A miner master that can mine two or more items
            _minerParam = MasterHolder.BlockMaster.Blocks.Data.Select(b => b.BlockParam).OfType<IMinerParam>().First(p => 2 <= p.MineSettings.items.Length);
            _firstMinedItem = MasterHolder.ItemMaster.GetItemId(_minerParam.MineSettings.items[0].ItemGuid);
            _secondMinedItem = MasterHolder.ItemMaster.GetItemId(_minerParam.MineSettings.items[1].ItemGuid);
        }

        [Test]
        public void 採掘秒2で採掘中の1品は毎分30で_他の品は出ない()
        {
            var detail = FamilyDetailConversion.Miner(MinerState(2, _firstMinedItem), Common(), _minerParam, false);

            Assert.AreEqual(1, detail.MiningItems.Count);
            Assert.AreEqual(_firstMinedItem, detail.MiningItems[0].ItemId);
            Assert.AreEqual(30, detail.MiningItems[0].ItemsPerMinute, 1e-9);
            Assert.AreNotEqual(_secondMinedItem, detail.MiningItems[0].ItemId);
        }

        [Test]
        public void 採掘秒が0以下なら採掘中の品は0件()
        {
            Assert.AreEqual(0, FamilyDetailConversion.Miner(MinerState(0, _firstMinedItem), Common(), _minerParam, false).MiningItems.Count);
            Assert.AreEqual(0, FamilyDetailConversion.Miner(MinerState(-1, _firstMinedItem), Common(), _minerParam, false).MiningItems.Count);
        }

        [Test]
        public void 採掘機の電力と進捗と駆動が写る()
        {
            var detail = FamilyDetailConversion.Miner(MinerState(2, _firstMinedItem), Common(), _minerParam, true);

            Assert.AreEqual(40, detail.CurrentPower, 1e-6);
            Assert.AreEqual(100, detail.RequestPower, 1e-6);
            Assert.AreEqual(0.25f, detail.Progress, 1e-6);
            Assert.IsTrue(detail.GearDriven);
        }

        [Test]
        public void 電気ポンプは状態と電力つきで_秒量は毎分90になる()
        {
            var param = MasterHolder.BlockMaster.Blocks.Data.Select(b => b.BlockParam).OfType<ElectricPumpBlockParam>().First();

            var reading = PumpDetailConversion.Build(PumpState(1.5), param, Common());

            Assert.IsTrue(reading.TryGetDetail(out var detail));
            Assert.IsFalse(reading.TryGetAnomaly(out _));
            var electric = (ElectricPumpDetail)detail;
            Assert.AreEqual(MachineProcessDisplayState.Processing, electric.State);
            Assert.AreEqual(40, electric.CurrentPower, 1e-6);
            Assert.AreEqual(100, electric.RequestPower, 1e-6);
            Assert.AreEqual(90, electric.PumpingFluids[0].AmountPerMinute, 1e-9);
            Assert.AreEqual(new FluidId(PumpedFluidId), electric.PumpingFluids[0].FluidId);
        }

        [Test]
        public void 歯車ポンプは歯車の詳細になる()
        {
            var param = MasterHolder.BlockMaster.Blocks.Data.Select(b => b.BlockParam).OfType<GearPumpBlockParam>().First();

            var reading = PumpDetailConversion.Build(PumpState(1.5), param, null);

            Assert.IsFalse(reading.TryGetAnomaly(out _));
            Assert.IsTrue(reading.TryGetDetail(out var detail));
            Assert.IsInstanceOf<GearPumpDetail>(detail);
            Assert.AreEqual(90, detail.PumpingFluids[0].AmountPerMinute, 1e-9);
        }

        [Test]
        public void ポンプでないparamは偽で異常を返しログは出さない()
        {
            var reading = PumpDetailConversion.Build(PumpState(1.5), (IBlockParam)_minerParam, Common());

            Assert.IsFalse(reading.TryGetDetail(out _));
            Assert.IsTrue(reading.TryGetAnomaly(out var anomaly));
            StringAssert.Contains("unsupported pump param", anomaly);
        }

        [Test]
        public void 電気ポンプでcommonが無いと偽で黙る()
        {
            var param = MasterHolder.BlockMaster.Blocks.Data.Select(b => b.BlockParam).OfType<ElectricPumpBlockParam>().First();

            // silent-ok の検証。Error は落ちる
            // Verifies the silent-ok; Unity fails the test if an Error is logged
            var reading = PumpDetailConversion.Build(PumpState(1.5), param, null);
            Assert.IsFalse(reading.TryGetDetail(out _));
            Assert.IsFalse(reading.TryGetAnomaly(out _));
        }

        [Test]
        public void 電気ポンプの状態文字列が読めないと偽で異常を返しログは出さない()
        {
            var param = MasterHolder.BlockMaster.Blocks.Data.Select(b => b.BlockParam).OfType<ElectricPumpBlockParam>().First();

            var reading = PumpDetailConversion.Build(PumpState(1.5), param, MachineTestDetails.Common("Unknown", 1, 1));
            Assert.IsFalse(reading.TryGetDetail(out _));
            Assert.IsTrue(reading.TryGetAnomaly(out var anomaly));
            StringAssert.Contains("unknown CurrentStateType", anomaly);
        }

        [Test]
        public void 発電機の3つの値がそのまま写る()
        {
            var detail = FamilyDetailConversion.Generator(new PowerGeneratorStateDetail(12.5, 30, 0.5f));

            Assert.AreEqual(12.5, detail.RemainingFuelTime, 1e-9);
            Assert.AreEqual(30, detail.CurrentFuelTime, 1e-9);
            Assert.AreEqual(0.5, detail.OperatingRate, 1e-6);
        }

        private const int PumpedFluidId = 1;

        private static PumpBlockStateDetail PumpState(double amountPerSecond)
        {
            return new PumpBlockStateDetail(new List<PumpingFluidMessagePack> { new PumpingFluidMessagePack(new FluidId(PumpedFluidId), amountPerSecond) });
        }

        private static CommonMachineBlockStateDetail Common()
        {
            return MachineTestDetails.Common(VanillaMachineBlockStateConst.ProcessingState, 40, 100);
        }

        private static CommonMinerBlockStateDetail MinerState(double miningSeconds, ItemId mining)
        {
            var detail = new CommonMinerBlockStateDetail(new List<IItemStack>(), miningSeconds);
            detail.CurrentMiningItemIdInts = new[] { mining.AsPrimitive() };
            return detail;
        }
    }
}
