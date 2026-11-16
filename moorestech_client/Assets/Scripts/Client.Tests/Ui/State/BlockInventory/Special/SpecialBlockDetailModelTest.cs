using System;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Tests.Ui.State.BlockInventory.Family;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.State;
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
            var block = _harness.CreateBlock(Vector3Int.zero, master, (SpaceElevatorBaseStateDetail.BlockStateDetailKey, new SpaceElevatorBaseStateDetail(true, 250, 0.5f, SpaceElevatorBaseStatus.Climbing)));
            // 追跡ブロックの個体 ID（既定値）とパネルの個体 ID をそろえ、違いを Error だけにする
            // Match the panel's instance ID to the tracked block's (default) so only Error differs
            var panel = new OpenBlockInventoryPanel(master.BlockType, master.BlockGuid, default, block.BlockInstanceId, Vector3Int.zero, Array.Empty<SlotItem>(), error);
            var model = new SpecialBlockDetailModel(new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(panel)), _harness.Tracker, new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()));
            model.Initialize();

            _harness.Track(block);

            Assert.AreEqual(expected, model.Current.Value.TryGetSpaceElevator(out _));
        }
    }
}
