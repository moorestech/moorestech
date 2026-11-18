using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Ui.State.BlockInventory.Family.Models;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface.State;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// 異常は同じ間 1 回、回復後に再び出る
    /// Anomaly once while it persists; again after recovery
    /// </summary>
    public class FamilyDetailAnomalyTest
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
        public void ポンプの未対応paramは状態変化で繰り返しても1回で_閉じて開き直すと再び出る()
        {
            var model = new PumpDetailModel(_harness.Tracker);
            model.Initialize();
            var notPumpMaster = MasterHolder.BlockMaster.Blocks.Data.First(b => b.BlockParam is not (ElectricPumpBlockParam or GearPumpBlockParam));
            var pump = (PumpBlockStateDetail.BlockStateDetailKey, (object)new PumpBlockStateDetail(new List<PumpingFluidMessagePack>()));
            var block = _harness.CreateBlock(Position, notPumpMaster, pump);

            LogAssert.Expect(LogType.Error, new Regex("unsupported pump param"));
            _harness.Track(block);
            _harness.NotifyStateChanged();
            _harness.NotifyStateChanged();

            // 閉じると旗が戻り、再度出る
            // Closing clears flag; reopening reports again
            _harness.Track(null);
            LogAssert.Expect(LogType.Error, new Regex("unsupported pump param"));
            _harness.Track(block);
        }

        [Test]
        public void 採掘機の歯車の不整合は状態変化で繰り返しても1回で_閉じて開き直すと再び出る()
        {
            var model = new MinerDetailModel(_harness.Tracker);
            model.Initialize();
            var minerMaster = MasterHolder.BlockMaster.Blocks.Data.First(b => b.BlockParam is IMinerParam and not IGearConsumptionParam);
            var miner = new CommonMinerBlockStateDetail(new List<Core.Item.Interface.IItemStack>(), 2);
            var common = new CommonMachineBlockStateDetail(40, 100, 0.25f, VanillaMachineBlockStateConst.ProcessingState, VanillaMachineBlockStateConst.IdleState);
            var gear = new GearStateDetail(true, 5f, 2.4f, GearRole.Consumer);
            var block = _harness.CreateBlock(Position, minerMaster, (CommonMinerBlockStateDetail.BlockStateDetailKey, miner), (CommonMachineBlockStateDetail.BlockStateDetailKey, common), (GearStateDetail.BlockStateDetailKey, gear));

            LogAssert.Expect(LogType.Error, new Regex("has no IGearConsumptionParam"));
            _harness.Track(block);
            _harness.NotifyStateChanged();
            _harness.NotifyStateChanged();

            _harness.Track(null);
            LogAssert.Expect(LogType.Error, new Regex("has no IGearConsumptionParam"));
            _harness.Track(block);
        }
    }
}
