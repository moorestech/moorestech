using System;
using Core.Master;
using Game.Block.Blocks.FilterSplitter;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// FilterSplitterStateProtocol の Get/SetMode/SetFilterItem 各 Operation を検証する。
    /// Verifies the Get/SetMode/SetFilterItem operations of FilterSplitterStateProtocol.
    /// </summary>
    public class FilterSplitterStateProtocolTest
    {
        private static readonly Vector3Int SplitterPos = new(20, 0, 20);

        [Test]
        public void GetReturnsCurrentSnapshotTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            PlaceFilterSplitter();

            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateGetRequest(SplitterPos));

            Assert.IsTrue(response.Success);
            Assert.AreEqual(3, response.DirectionCount);
            Assert.AreEqual(4, response.FilterSlotCountPerDirection);
            // 初期状態は全方向 Default
            // Initial state is Default for all directions
            for (var d = 0; d < response.DirectionCount; d++)
            {
                Assert.AreEqual(FilterSplitterMode.Default, response.Directions[d].Mode);
            }
        }

        [Test]
        public void GetForNonexistentBlockReturnsBlockNotFoundTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateGetRequest(new Vector3Int(999, 0, 999)));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(FilterSplitterStateProtocol.FilterSplitterStateFailureReason.BlockNotFound, response.FailureReason);
        }

        [Test]
        public void GetForWrongBlockTypeReturnsNotFilterSplitterTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            // Chest を置く（FilterSplitter ではない）
            // Place a Chest (not a FilterSplitter)
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, SplitterPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateGetRequest(SplitterPos));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(FilterSplitterStateProtocol.FilterSplitterStateFailureReason.NotFilterSplitter, response.FailureReason);
        }

        [Test]
        public void SetModeUpdatesModeAndReturnsSnapshotTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            PlaceFilterSplitter();

            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(SplitterPos, 1, FilterSplitterMode.Whitelist));

            Assert.IsTrue(response.Success);
            Assert.AreEqual(FilterSplitterMode.Whitelist, response.Directions[1].Mode);
            Assert.AreEqual(FilterSplitterMode.Default, response.Directions[0].Mode);
        }

        [Test]
        public void SetModeWithInvalidDirectionReturnsInvalidDirectionTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            PlaceFilterSplitter();

            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(SplitterPos, 99, FilterSplitterMode.Whitelist));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(FilterSplitterStateProtocol.FilterSplitterStateFailureReason.InvalidDirection, response.FailureReason);
        }

        [Test]
        public void SetModeWithInvalidModeReturnsInvalidModeTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            PlaceFilterSplitter();

            // 未定義 enum 値 (例: 999) を投入
            // Inject an undefined enum value (e.g. 999)
            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(SplitterPos, 0, (FilterSplitterMode)999));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(FilterSplitterStateProtocol.FilterSplitterStateFailureReason.InvalidMode, response.FailureReason);
        }

        [Test]
        public void SetFilterItemValidIdUpdatesSlotTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            PlaceFilterSplitter();

            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(SplitterPos, 0, 0, ForUnitTestItemId.ItemId1));

            Assert.IsTrue(response.Success);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, response.Directions[0].FilterItemIds[0]);
        }

        [Test]
        public void SetFilterItemEmptyClearsSlotTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            PlaceFilterSplitter();
            // 一旦アイテムを入れてからクリアする
            // Set then clear
            Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(SplitterPos, 0, 0, ForUnitTestItemId.ItemId1));
            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(SplitterPos, 0, 0, ItemMaster.EmptyItemId));

            Assert.IsTrue(response.Success);
            Assert.AreEqual(ItemMaster.EmptyItemId, response.Directions[0].FilterItemIds[0]);
        }

        [Test]
        public void SetFilterItemUnknownIdReturnsInvalidItemTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            PlaceFilterSplitter();

            // master に存在しない ItemId を投入
            // Inject an ItemId that does not exist in the master
            var unknownItemId = new ItemId(int.MaxValue);
            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(SplitterPos, 0, 0, unknownItemId));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(FilterSplitterStateProtocol.FilterSplitterStateFailureReason.InvalidItem, response.FailureReason);
        }

        [Test]
        public void SetFilterItemInvalidSlotReturnsInvalidSlotTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            PlaceFilterSplitter();

            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(SplitterPos, 0, 99, ForUnitTestItemId.ItemId1));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(FilterSplitterStateProtocol.FilterSplitterStateFailureReason.InvalidSlot, response.FailureReason);
        }

        private static void PlaceFilterSplitter()
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.FilterSplitter,
                SplitterPos,
                BlockDirection.North,
                Array.Empty<BlockCreateParam>(),
                out _);
        }

        private static FilterSplitterStateProtocol.FilterSplitterStateResponse Send(
            PacketResponseCreator packet,
            FilterSplitterStateProtocol.FilterSplitterStateRequest request)
        {
            var payload = MessagePackSerializer.Serialize(request);
            var responseBytes = packet.GetPacketResponse(payload)[0];
            return MessagePackSerializer.Deserialize<FilterSplitterStateProtocol.FilterSplitterStateResponse>(responseBytes);
        }
    }
}
