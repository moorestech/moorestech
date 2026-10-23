using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game.RocketLaunchPad
{
    // テンプレート登録後に実際のブロック経由で専用スロットを検証する
    // Verify bound slots through the actual block after template registration
    public class RocketLaunchPadInventoryTest
    {
        private static readonly Vector3Int Position = new(5, 10, 0);
        private PacketResponseCreator _packet;
        private IBlockInventory _blockInventory;
        private IOpenableBlockInventoryComponent _inventory;
        private RocketLaunchPadSlotLayout _layout;

        [SetUp]
        public void Setup()
        {
            (_packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var added = ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.RocketLaunchPadId, Position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            Assert.IsTrue(added);
            _blockInventory = block.GetComponent<IBlockInventory>();
            _inventory = block.GetComponent<IOpenableBlockInventoryComponent>();
            _layout = new RocketLaunchPadSlotLayout((RocketLaunchPadBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.RocketLaunchPadId).BlockParam);
        }

        [Test]
        public void 搬入は専用スロットだけへ入り未登録品は返る()
        {
            var fuel = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.RocketFuelId, 5);
            var unrelated = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ComputeDataId, 1);

            // プレイヤー搬入とブロック搬入は同じ割り当てを使う
            // Player insertion and block insertion share the same slot assignment
            Assert.AreEqual(0, _inventory.InsertItem(fuel).Count);
            Assert.AreEqual(fuel, _inventory.GetItem(1));
            Assert.AreEqual(unrelated, _blockInventory.InsertItem(unrelated, InsertItemContext.Empty));
            Assert.AreEqual(0, _blockInventory.InsertItem(fuel, InsertItemContext.Empty).Count);
            Assert.AreEqual(10, _inventory.GetItem(1).Count);
            Assert.AreEqual(ItemMaster.EmptyItemId, _inventory.GetItem(0).Id);
        }

        [Test]
        public void 出力口と異なる品の入れ口へ配置できない()
        {
            var fuel = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.RocketFuelId, 1);
            var rocketCase = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.RocketCaseId, 1);

            Assert.IsFalse(_inventory.IsAllowedToPlace(_layout.OutputSlotStart, fuel));
            Assert.IsFalse(_inventory.IsAllowedToPlace(0, fuel));
            Assert.IsTrue(_inventory.IsAllowedToPlace(0, rocketCase));
            Assert.IsTrue(_inventory.IsAllowedToPlace(_layout.OutputSlotStart, ServerContext.ItemStackFactory.CreatEmpty()));
        }

        [Test]
        public void 同じ品の複数スタックも合計上限で搬入判定する()
        {
            var overCapacity = new List<IItemStack>
            {
                ServerContext.ItemStackFactory.Create(ForUnitTestItemId.RocketCaseId, 99),
                ServerContext.ItemStackFactory.Create(ForUnitTestItemId.RocketCaseId, 2),
            };

            // 仮挿入は中身を変えず、実挿入と同じ余りを予測する
            // Simulation leaves slots unchanged and predicts the actual remainder
            Assert.IsFalse(_blockInventory.InsertionCheck(overCapacity));
            Assert.AreEqual(0, _inventory.GetItem(0).Count);
            var remainder = _inventory.InsertItem(overCapacity);
            Assert.AreEqual(100, _inventory.GetItem(0).Count);
            Assert.AreEqual(1, remainder.Count);
            Assert.AreEqual(1, remainder[0].Count);
        }

        [Test]
        public void 別々の入れ口ならまとめて搬入できる()
        {
            var inputs = new List<IItemStack>
            {
                ServerContext.ItemStackFactory.Create(ForUnitTestItemId.RocketCaseId, 99),
                ServerContext.ItemStackFactory.Create(ForUnitTestItemId.SatellitePayloadId, 1),
            };

            Assert.IsTrue(_blockInventory.InsertionCheck(inputs));
            Assert.AreEqual(0, _inventory.InsertItem(inputs).Count);
            Assert.AreEqual(99, _inventory.GetItem(0).Count);
            Assert.AreEqual(1, _inventory.GetItem(3).Count);
        }

        [Test]
        public void 資材と積荷と出力の順序を共有する()
        {
            Assert.AreEqual(7, _inventory.GetSlotSize());
            Assert.AreEqual(7, _layout.TotalSlotCount);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, _layout.MaterialSlots);
            CollectionAssert.AreEqual(new[] { 3, 4 }, _layout.PayloadSlots);
            CollectionAssert.AreEqual(new[] { 5, 6 }, _layout.OutputSlots);
            Assert.AreEqual(3, _layout.PayloadSlot(0));
            Assert.IsTrue(_layout.TryGetInputSlot(ForUnitTestItemId.RocketFuelId, out var fuelSlot));
            Assert.AreEqual(1, fuelSlot);
            Assert.IsTrue(_layout.TryGetPayloadRequirement(ForUnitTestItemId.SatellitePayloadId, out var payload));
            Assert.AreEqual(1, payload.Count);
            Assert.IsFalse(_layout.TryGetPayloadRequirement(ForUnitTestItemId.RocketFuelId, out _));
        }

        [Test]
        public void 整理プロトコルでも空きと専用スロットの品は動かない()
        {
            _inventory.InsertItem(ForUnitTestItemId.RocketFuelId, 5);
            _inventory.InsertItem(ForUnitTestItemId.SatellitePayloadId, 1);
            var before = _inventory.InventoryItems.ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(0, 7), ((ISortExcludedSlots)_inventory).SortExcludedSlots);

            // 前方を空けたまま実プロトコルを通し、誤った詰め直しを検出する
            // Use the real protocol with an empty front slot to detect accidental compaction
            var request = new SortInventoryProtocol.SortInventoryProtocolMessagePack(InventoryIdentifierMessagePack.CreateBlockMessage(Position));
            _packet.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1));
            CollectionAssert.AreEqual(before, _inventory.InventoryItems);
        }
    }
}
