using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Update;
using Server.Util.MessagePack;
using Game.Block.Interface;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Core.CleanRoom;
using Tests.CombinedTest.Core.CleanRoom.Machine;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Server.PacketTest.CleanRoom
{
    public class GetCleanRoomInfoProtocolTest
    {
        [Test]
        public void EnclosedRoomIsReturnedWithAirFilterTest()
        {
            var packet = CreatePacket();
            CleanRoomMachineTestHelpers.BuildSmallCleanRoomWithFilter();
            GameUpdater.UpdateOneTick();

            // 壁のIDで引くと、清浄機1台を持つ密閉済みの部屋の型が返る
            // Querying a wall ID returns the enclosed type holding one purifier
            var wall = ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(0, 2, 2));
            Assert.AreEqual(BlockInfoReadResult.Success, Invoke(packet, wall.BlockInstanceId).TryReadView(out var view));
            Assert.IsInstanceOf<CleanRoomEnclosedView>(view);
            var rooms = ((CleanRoomEnclosedView)view).Rooms;
            Assert.AreEqual(1, rooms.Count);
            Assert.AreEqual(26, rooms[0].Volume);
            Assert.AreEqual(1, rooms[0].AirFilters.Count);
            Assert.AreEqual(new Vector3Int(1, 1, 1), rooms[0].AirFilters[0].Position.Vector3Int);
            Assert.AreEqual(5, rooms[0].AirFilters[0].FilterItemCount);

            // 検出直後の部屋は Out なので、往復後もクラスは読めない
            // A freshly detected room is Out, so no class is readable after the round trip
            Assert.IsFalse(rooms[0].TryGetClass(out _, out _));
        }

        [Test]
        public void BreachedRoomReturnsOpeningCellsTest()
        {
            var packet = CreatePacket();
            var filter = CleanRoomMachineTestHelpers.BuildSmallCleanRoomWithFilter();
            GameUpdater.UpdateOneTick();
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove);
            GameUpdater.UpdateOneTick();

            // 壁を1枚壊すと、清浄機から引いた応答は未密閉の型になり、壊した位置が漏れとして載る
            // Breaking one wall turns the purifier's response into the not-enclosed type carrying that cell as the leak
            Assert.AreEqual(BlockInfoReadResult.Success, Invoke(packet, filter.BlockInstanceId).TryReadView(out var view));
            Assert.IsInstanceOf<CleanRoomNotEnclosedView>(view);
            var leak = ((CleanRoomNotEnclosedView)view).Leak;
            Assert.IsInstanceOf<CleanRoomOpeningsLeakView>(leak);
            var openings = (CleanRoomOpeningsLeakView)leak;
            Assert.AreEqual(1, openings.TotalOpeningCount);
            Assert.AreEqual(new Vector3Int(2, 2, 0), openings.OpeningCells[0].Vector3Int);
        }

        [Test]
        public void UnknownBlockReturnsBlockNotFoundTest()
        {
            var packet = CreatePacket();

            // 存在しないブロックIDは BlockNotFound になり、部屋の型を返さない
            // A missing block ID becomes BlockNotFound and yields no room type
            var response = Invoke(packet, new BlockInstanceId(999999));
            Assert.AreEqual(CleanRoomInfoResponseKind.BlockNotFound, response.Kind);
            Assert.AreEqual(BlockInfoReadResult.BlockNotFound, response.TryReadView(out _));
        }

        [Test]
        public void MalformedResponsesAreUnreadableWithWarningTest()
        {
            // 判別子と入れ子が合わない応答は、読み口を作らず警告して Unreadable を返す
            // A response whose discriminator and nested values disagree yields no view, logs a warning and returns Unreadable
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomInfo\] Enclosed response"));
            Assert.AreEqual(BlockInfoReadResult.Unreadable, GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateEnclosed(new List<CleanRoomSummaryMessagePack>()).TryReadView(out _));

            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomInfo\] NotEnclosed response has no leak"));
            Assert.AreEqual(BlockInfoReadResult.Unreadable, GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateNotEnclosed(null).TryReadView(out _));

            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomInfo\] Openings leak has no cells"));
            Assert.AreEqual(BlockInfoReadResult.Unreadable, GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateNotEnclosed(CleanRoomLeakMessagePack.CreateOpenings(null, 3)).TryReadView(out _));
        }

        [Test]
        public void UnknownKindsAndMissingNestedElementsAreRejectedTest()
        {
            var response = GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateBlockNotFound();
            response.Kind = (CleanRoomInfoResponseKind)999;
            Assert.AreEqual(BlockInfoReadResult.Unreadable, response.TryReadView(out _));
            var leak = CleanRoomLeakMessagePack.CreateNoEnclosure();
            leak.Kind = (CleanRoomLeakResponseKind)999;
            Assert.IsFalse(leak.TryGetView(out _));
            Assert.IsFalse(CleanRoomLeakMessagePack.CreateOpenings(new List<Vector3IntMessagePack> { null }, 1).TryGetView(out _));
            var room = CleanRoomSummaryMessagePack.CreateOut(0, 0, 1, new List<CleanRoomAirFilterMessagePack>());
            response = GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateEnclosed(new List<CleanRoomSummaryMessagePack> { room });
            room.ClassKind = (CleanRoomClassResponseKind)999;
            Assert.AreEqual(BlockInfoReadResult.Unreadable, response.TryReadView(out _));
            room.ClassKind = CleanRoomClassResponseKind.Out;
            room.AirFilters.Add(null);
            Assert.AreEqual(BlockInfoReadResult.Unreadable, response.TryReadView(out _));
            room.AirFilters[0] = new CleanRoomAirFilterMessagePack(null, 1, 1, 1, 1);
            Assert.AreEqual(BlockInfoReadResult.Unreadable, response.TryReadView(out _));
            var yieldResponse = GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateNoRole();
            yieldResponse.Kind = (CleanRoomYieldResponseKind)999;
            Assert.AreEqual(BlockInfoReadResult.Unreadable, yieldResponse.TryReadView(out _));
            var stall = GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateNotStalled();
            stall.Kind = (MachineInputStallResponseKind)999;
            Assert.AreEqual(BlockInfoReadResult.Unreadable, stall.TryReadView(out _));
        }

        private static PacketResponseCreator CreatePacket()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return packet;
        }

        private static GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack Invoke(PacketResponseCreator packet, BlockInstanceId id)
        {
            var request = new GetCleanRoomInfoProtocol.RequestCleanRoomInfoMessagePack(id);
            var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1));
            return MessagePackSerializer.Deserialize<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack>(responseBytes[0]);
        }
    }
}
