using System.Linq;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Tests.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Source;
using Core.Master;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Special.Source
{
    /// <summary>
    /// 発射台と宇宙エレベーターの詳細を実マスタの枠の並びから組み、状態が未着なら出さないことを確かめる（L0）
    /// Verifies launch pad and space elevator details are built from the real master layout and omitted until state arrives (L0)
    /// </summary>
    public class SpecialBlockDetailReaderSlotTest
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
        public void 発射台は状態が届くまで出さず届けばマスタの枠の並びと要求アイテムの束縛を持つ()
        {
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.RocketLaunchPadId);
            var layout = new RocketLaunchPadSlotLayout((RocketLaunchPadBlockParam)master.BlockParam);
            var pending = _harness.CreateBlock(Vector3Int.zero, master);
            Assert.IsFalse(Read(pending).TryGetRocketLaunchPad(out _));

            var block = _harness.CreateBlock(Vector3Int.zero, master, (RocketLaunchPadStateDetail.BlockStateDetailKey, new RocketLaunchPadStateDetail(RocketLaunchPadPhase.Building, RocketLaunchPadWaitReason.NoPower, 0.375f, null, null)));
            Assert.IsTrue(Read(block).TryGetRocketLaunchPad(out var pad));

            Assert.AreEqual(0.375f, pad.Progress);
            CollectionAssert.AreEqual(layout.MaterialSlots, pad.MaterialSlots);
            CollectionAssert.AreEqual(layout.PayloadSlots, pad.PayloadSlots);
            CollectionAssert.AreEqual(layout.OutputSlots, pad.OutputSlots(layout.TotalSlotCount));
            // 在庫の数が増えると出力の枠も伸びる
            // Output slots extend as the inventory grows
            CollectionAssert.AreEqual(layout.OutputSlots.Concat(new[] { layout.TotalSlotCount }), pad.OutputSlots(layout.TotalSlotCount + 1));
            // 入れ口の受け付け ID はサーバーの要求アイテム 1 品
            // Each inlet accepts exactly the server's required item
            CollectionAssert.AreEqual(layout.MaterialSlots.Concat(layout.PayloadSlots), pad.InletBindings.Select(binding => binding.Slot));
            foreach (var binding in pad.InletBindings) CollectionAssert.AreEqual(new[] { layout.RequirementAt(binding.Slot).ItemId }, binding.AcceptedItemIds);
        }

        [Test]
        public void 宇宙エレベーターは状態が届くまで出さず届けば出口数とケーブル数と運ぶ積荷をマスタから引く()
        {
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestSpaceElevatorBase);
            var param = (SpaceElevatorBaseBlockParam)master.BlockParam;
            var pending = _harness.CreateBlock(Vector3Int.zero, master);
            Assert.IsFalse(Read(pending).TryGetSpaceElevator(out _));

            var block = _harness.CreateBlock(Vector3Int.zero, master, (SpaceElevatorBaseStateDetail.BlockStateDetailKey, new SpaceElevatorBaseStateDetail(true, 250, 0.5f, SpaceElevatorBaseStatus.Climbing, null, null)));
            Assert.IsTrue(Read(block).TryGetSpaceElevator(out var elevator));

            var carried = param.CarriedPayloadItemGuids.Select(guid => MasterHolder.ItemMaster.GetItemId(guid)).ToList();
            CollectionAssert.IsNotEmpty(carried, "テスト前提: 運ぶ積荷がマスタにあること");
            CollectionAssert.AreEqual(new[] { SpaceElevatorBaseInventoryComponent.SatelliteSlot }, elevator.SatelliteInputSlots);
            CollectionAssert.AreEqual(Enumerable.Range(SpaceElevatorBaseInventoryComponent.FirstOutputSlot, param.OutputSlotCount), elevator.OutputSlots);
            Assert.AreEqual(param.CablesPerElevator, elevator.CablesPerElevator);
            Assert.AreEqual(250, elevator.CablePoolAmount);
            Assert.AreEqual(1, elevator.InletBindings.Count);
            Assert.AreEqual(SpaceElevatorBaseInventoryComponent.SatelliteSlot, elevator.InletBindings[0].Slot);
            CollectionAssert.AreEqual(carried, elevator.InletBindings[0].AcceptedItemIds);
        }

        private static SpecialBlockDetailViewModel Read(Client.Game.InGame.Block.BlockGameObject block)
        {
            return SpecialBlockDetailReader.Read(block, new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), out _);
        }
    }
}
