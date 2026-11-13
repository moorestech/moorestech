using System.Linq;
using Client.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockInventory.Family.Models;
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
    /// 族ごとの詳細 Model の揃い方
    /// How the per-family detail models assemble; rebuilt on tracking and state changes
    /// </summary>
    public class FamilyDetailModelTest
    {
        private static readonly Vector3Int Position = new(1, 0, 1);
        private TrackedBlockHarness _harness;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _harness = new TrackedBlockHarness();
        }

        [TearDown]
        public void TearDown() => _harness.Dispose();

        [Test]
        public void 採掘機は詳細_common_IMinerParamの3つが揃わないとAbsent()
        {
            var minerMaster = MasterHolder.BlockMaster.Blocks.Data.First(b => b.BlockParam is IMinerParam);
            var model = new MinerDetailModel(_harness.Tracker);
            model.Initialize();
            var miner = new CommonMinerBlockStateDetail(new System.Collections.Generic.List<Core.Item.Interface.IItemStack>(), 2);
            var common = Common();

            _harness.Track(_harness.CreateBlock(Position, minerMaster, (CommonMinerBlockStateDetail.BlockStateDetailKey, miner)));
            Assert.IsFalse(model.Current.Value.IsPresent, "common が無い");

            _harness.Track(_harness.CreateBlock(Position, minerMaster, (CommonMachineBlockStateDetail.BlockStateDetailKey, common)));
            Assert.IsFalse(model.Current.Value.IsPresent, "採掘機の詳細が無い");

            var notMinerMaster = MasterHolder.BlockMaster.Blocks.Data.First(b => b.BlockParam is not IMinerParam);
            _harness.Track(_harness.CreateBlock(Position, notMinerMaster, (CommonMinerBlockStateDetail.BlockStateDetailKey, miner), (CommonMachineBlockStateDetail.BlockStateDetailKey, common)));
            Assert.IsFalse(model.Current.Value.IsPresent, "IMinerParam でない");

            _harness.Track(_harness.CreateBlock(Position, minerMaster, (CommonMinerBlockStateDetail.BlockStateDetailKey, miner), (CommonMachineBlockStateDetail.BlockStateDetailKey, common)));
            Assert.IsTrue(model.Current.Value.TryGetPresent(out var minerDetail));
            Assert.IsFalse(minerDetail.GearDriven, "歯車の詳細が無ければ電力駆動");

            _harness.Track(null);
            Assert.IsFalse(model.Current.Value.IsPresent, "閉");
        }

        [Test]
        public void 発電機は状態変化で値が替わり_ポンプは歯車ポンプのparamで歯車の詳細()
        {
            var generator = new GeneratorDetailModel(_harness.Tracker);
            var pump = new PumpDetailModel(_harness.Tracker);
            generator.Initialize();
            pump.Initialize();
            var gearPumpMaster = MasterHolder.BlockMaster.Blocks.Data.First(b => b.BlockParam is GearPumpBlockParam);
            var block = _harness.CreateBlock(Position, gearPumpMaster, (PowerGeneratorStateDetail.StateDetailKey, new PowerGeneratorStateDetail(10, 20, 0.5f)), (PumpBlockStateDetail.BlockStateDetailKey, new PumpBlockStateDetail(new System.Collections.Generic.List<PumpingFluidMessagePack>())));

            _harness.Track(block);
            Assert.IsTrue(generator.Current.Value.TryGetPresent(out var fueled));
            Assert.AreEqual(10, fueled.RemainingFuelTime, 1e-9);
            Assert.IsTrue(pump.Current.Value.TryGetPresent(out var pumpDetail));
            Assert.IsInstanceOf<GearPumpDetail>(pumpDetail);

            _harness.ReplaceStates(block, (PowerGeneratorStateDetail.StateDetailKey, new PowerGeneratorStateDetail(7, 20, 0.5f)));
            _harness.NotifyStateChanged();
            Assert.IsTrue(generator.Current.Value.TryGetPresent(out var lowFuel));
            Assert.AreEqual(7, lowFuel.RemainingFuelTime, 1e-9);
            Assert.IsFalse(pump.Current.Value.IsPresent, "ポンプの詳細が消えた");
        }

        private static CommonMachineBlockStateDetail Common()
        {
            return new CommonMachineBlockStateDetail(40, 100, 0.25f, VanillaMachineBlockStateConst.ProcessingState, VanillaMachineBlockStateConst.IdleState);
        }
    }
}
