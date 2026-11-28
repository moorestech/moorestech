using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Tests.Ui.State.BlockInventory.Family;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 在庫を開けなかったパネルでは状態の届いた特殊族の詳細も出さないことを確かめる（L0）
    /// Verifies special details stay hidden on a panel that failed to open even when state has arrived (L0)
    /// </summary>
    public class SpecialBlockDetailModelTest
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

        [TestCase(BlockInventoryPanelError.OpenFailed, false)]
        [TestCase(BlockInventoryPanelError.None, true)]
        public void 宇宙エレベーターの詳細は在庫を開けたパネルでだけ出る(BlockInventoryPanelError error, bool expected)
        {
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestSpaceElevatorBase);
            var block = _harness.CreateBlock(Vector3Int.zero, master, (SpaceElevatorBaseStateDetail.BlockStateDetailKey, new SpaceElevatorBaseStateDetail(true, 250, 0.5f, SpaceElevatorBaseStatus.Climbing, null, null)));
            // 追跡ブロックの個体 ID（既定値）とパネルの個体 ID をそろえ、違いを Error だけにする
            // Match the panel's instance ID to the tracked block's (default) so only Error differs
            var panel = BlockInventoryTestPanels.OpenBlock(master.BlockType, master.BlockScreen, master.BlockGuid, default, block.BlockInstanceId, Vector3Int.zero, Array.Empty<SlotItem>(), error);
            var model = new SpecialBlockDetailModel(new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(panel)), _harness.Tracker, new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), new FakeUnlockedOrbitalCargoSource());
            model.Initialize();

            _harness.Track(block);

            Assert.AreEqual(expected, model.Current.Value.TryGetSpaceElevator(out _));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 発射台画面の機能不足を状態の未着と区別する(bool hasLaunchPad)
        {
            var blockId = hasLaunchPad ? ForUnitTestModBlockId.RocketLaunchPadId : ForUnitTestModBlockId.ChestId;
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var block = _harness.CreateBlock(Vector3Int.zero, master);
            var screen = BlockMasterElement.BlockScreenConst.RocketLaunchPad;
            var panel = BlockInventoryTestPanels.OpenBlock(master.BlockType, screen, master.BlockGuid, blockId, block.BlockInstanceId, Vector3Int.zero, Array.Empty<SlotItem>(), BlockInventoryPanelError.None);
            var model = new SpecialBlockDetailModel(new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(panel)), _harness.Tracker, new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), new FakeUnlockedOrbitalCargoSource());
            model.Initialize();
            _harness.Track(block);

            // どちらにも状態は未着だが、発射台の機能が無い方にだけ理由を出す
            // Neither state has arrived; only the block without launch pad functionality gets a reason
            Assert.IsFalse(model.Current.Value.TryGetRocketLaunchPad(out _));
            Assert.AreEqual(!hasLaunchPad, model.Current.Value.Unavailable != null);
        }

        [Test]
        public void 積荷の解放が変わると発射台の一覧が組み直される()
        {
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.RocketLaunchPadId);
            var layout = new RocketLaunchPadSlotLayout((RocketLaunchPadBlockParam)master.BlockParam);
            var block = _harness.CreateBlock(Vector3Int.zero, master, (RocketLaunchPadStateDetail.BlockStateDetailKey, new RocketLaunchPadStateDetail(RocketLaunchPadPhase.Idle, RocketLaunchPadWaitReason.CargoNotSelected, 0f, null, null)));
            var panel = BlockInventoryTestPanels.OpenBlock(master.BlockType, master.BlockScreen, master.BlockGuid, default, block.BlockInstanceId, Vector3Int.zero, Array.Empty<SlotItem>(), BlockInventoryPanelError.None);
            var unlocked = new FakeUnlockedOrbitalCargoSource();
            var model = new SpecialBlockDetailModel(new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(panel)), _harness.Tracker, new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), unlocked);
            model.Initialize();
            _harness.Track(block);
            Assert.IsTrue(model.Current.Value.TryGetRocketLaunchPad(out var before));
            CollectionAssert.IsEmpty(before.Cargo.SelectableChoices);

            var payload = layout.RequirementAt(layout.PayloadSlots[0]).ItemId;
            unlocked.Unlocked.Value = new HashSet<ItemId> { payload };
            Assert.IsTrue(model.Current.Value.TryGetRocketLaunchPad(out var after));
            CollectionAssert.AreEqual(new[] { payload }, after.Cargo.SelectableChoices.Select(choice => choice.PayloadItemId).ToArray());
        }
    }
}
