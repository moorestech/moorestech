using System;
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
        public void 発射台は状態が届くまで出さず届けばマスタの枠の並びと選んだ積荷からの束縛を持つ()
        {
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.RocketLaunchPadId);
            var layout = new RocketLaunchPadSlotLayout((RocketLaunchPadBlockParam)master.BlockParam);
            var pending = _harness.CreateBlock(Vector3Int.zero, master);
            Assert.IsFalse(Read(pending).TryGetRocketLaunchPad(out _));

            // 未選択は Shift で何も受けず、選んだ後は資材と選んだ積荷の枠だけ（サーバーの受け入れと同じ）
            // Unselected accepts nothing on Shift; after selecting, only materials and the selected payload slot (same as the server)
            var unselected = _harness.CreateBlock(Vector3Int.zero, master, (RocketLaunchPadStateDetail.BlockStateDetailKey, new RocketLaunchPadStateDetail(RocketLaunchPadPhase.Idle, RocketLaunchPadWaitReason.CargoNotSelected, 0f, null, null)));
            Assert.IsTrue(Read(unselected).TryGetRocketLaunchPad(out var unselectedPad));
            CollectionAssert.IsEmpty(unselectedPad.InletBindings);

            // 選び直した後は今の便と次の便の積荷が異なる
            // Current and next flights carry different payloads after reselection
            var payload = layout.RequirementAt(layout.PayloadSlots[0]).ItemId;
            var loaded = layout.RequirementAt(layout.PayloadSlots[1]).ItemId;
            var block = _harness.CreateBlock(Vector3Int.zero, master, (RocketLaunchPadStateDetail.BlockStateDetailKey, new RocketLaunchPadStateDetail(RocketLaunchPadPhase.Building, RocketLaunchPadWaitReason.NoPower, 0.375f, payload, loaded)));
            Assert.IsTrue(Read(block).TryGetRocketLaunchPad(out var pad));

            Assert.AreEqual(0.375f, pad.Progress);
            CollectionAssert.AreEqual(layout.MaterialSlots, pad.MaterialSlots.Select(slot => slot.Slot));
            CollectionAssert.AreEqual(layout.PayloadSlots, pad.PayloadSlots.Select(slot => slot.Slot));
            CollectionAssert.AreEqual(layout.OutputSlots, pad.OutputSlots(layout.TotalSlotCount));
            // 在庫の数が増えると出力の枠も伸びる
            // Output slots extend as the inventory grows
            CollectionAssert.AreEqual(layout.OutputSlots.Concat(new[] { layout.TotalSlotCount }), pad.OutputSlots(layout.TotalSlotCount + 1));
            CollectionAssert.AreEqual(layout.MaterialSlots.Concat(new[] { layout.PayloadSlots[0] }), pad.InletBindings.Select(binding => binding.Slot));
            foreach (var binding in pad.InletBindings) CollectionAssert.AreEqual(new[] { layout.RequirementAt(binding.Slot).ItemId }, binding.AcceptedItemIds);
            Assert.AreEqual(payload, pad.Cargo.SelectedCargoItemId);
            Assert.AreEqual(loaded, pad.Cargo.CurrentFlightCargoItemId);
        }

        [Test]
        public void 宇宙エレベーターは状態が届くまで出さず届けば出口数とケーブル数と選んだ積荷の束縛をマスタから引く()
        {
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestSpaceElevatorBase);
            var param = (SpaceElevatorBaseBlockParam)master.BlockParam;
            var pending = _harness.CreateBlock(Vector3Int.zero, master);
            Assert.IsFalse(Read(pending).TryGetSpaceElevator(out _));

            var carried = param.CarriedPayloadItemGuids.Select(guid => MasterHolder.ItemMaster.GetItemId(guid)).ToList();
            Assert.GreaterOrEqual(carried.Count, 2, "テスト前提: 運べる積荷がマスタに2種類以上あること");
            var block = _harness.CreateBlock(Vector3Int.zero, master, (SpaceElevatorBaseStateDetail.BlockStateDetailKey, new SpaceElevatorBaseStateDetail(true, 250, 0.5f, SpaceElevatorBaseStatus.Climbing, carried[0], carried[1])));
            Assert.IsTrue(Read(block).TryGetSpaceElevator(out var elevator));

            CollectionAssert.AreEqual(new[] { SpaceElevatorBaseInventoryComponent.SatelliteSlot }, elevator.SatelliteInputSlots);
            CollectionAssert.AreEqual(Enumerable.Range(SpaceElevatorBaseInventoryComponent.FirstOutputSlot, param.OutputSlotCount), elevator.OutputSlots);
            Assert.AreEqual(param.CablesPerElevator, elevator.CablesPerElevator);
            Assert.AreEqual(250, elevator.CablePoolAmount);
            Assert.AreEqual(1, elevator.InletBindings.Count);
            Assert.AreEqual(SpaceElevatorBaseInventoryComponent.SatelliteSlot, elevator.InletBindings[0].Slot);
            CollectionAssert.AreEqual(new[] { carried[0] }, elevator.InletBindings[0].AcceptedItemIds);
            Assert.AreEqual(carried[0], elevator.Cargo.SelectedCargoItemId);
            Assert.AreEqual(carried[1], elevator.Cargo.CurrentFlightCargoItemId);
            Assert.AreEqual(param.ClimbSeconds, elevator.Cargo.FlightSeconds, 0.001);
        }

        private static SpecialBlockDetailViewModel Read(Client.Game.InGame.Block.BlockGameObject block)
        {
            return SpecialBlockDetailReader.Read(block, new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), Array.Empty<ItemId>(), out _);
        }
    }
}
