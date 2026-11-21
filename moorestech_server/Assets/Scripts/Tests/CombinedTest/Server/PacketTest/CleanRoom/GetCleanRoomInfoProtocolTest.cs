using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Master;
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
            var filter = CleanRoomMachineTestHelpers.BuildSmallCleanRoomWithFilter();
            GameUpdater.UpdateOneTick();

            // 清浄機のIDで引くと、清浄機1台を持つ密閉済みの部屋の型が返る
            // Querying the purifier ID returns the enclosed type holding one purifier
            Assert.AreEqual(BlockInfoReadResult.Success, Invoke(packet, filter.BlockInstanceId).TryReadView(out var view));
            Assert.IsInstanceOf<CleanRoomEnclosedView>(view);
            var room = ((CleanRoomEnclosedView)view).Room;
            Assert.AreEqual(26, room.Volume);
            Assert.AreEqual(1, room.AirFilters.Count);
            Assert.AreEqual(new Vector3Int(1, 1, 1), room.AirFilters[0].Position.Vector3Int);
            Assert.AreEqual(5, room.AirFilters[0].FilterItemCount);

            // 電線の無い清浄機1台は止まっていて、Out からいちばん下のクラスを目指す
            // The single unwired purifier is stopped, and the next class from Out is the lowest one
            Assert.AreEqual(1, room.AirFilterCount);
            Assert.AreEqual(1, room.StoppedAirFilterCount);
            Assert.IsTrue(room.NextClass.TryGetUnitsToNextClass(out var nextClassName, out var units));
            Assert.AreEqual(MasterHolder.CleanRoomMaster.Thresholds[MasterHolder.CleanRoomMaster.OutThresholdIndex - 1].ClassName, nextClassName);
            Assert.Greater(units, 0);

            // 清浄機から引いたので自身の状態（電力0・フィルター満タン）が載る
            // Queried from a purifier, so its own state (zero power, fresh filter) is carried
            Assert.IsTrue(room.QueriedAirFilter.TryGetStatus(out var powerRatio, out var filterRemainingRatio));
            Assert.AreEqual(0d, powerRatio);
            Assert.AreEqual(1d, filterRemainingRatio, 1e-9);

            // 検出直後の部屋は Out なので、往復後もクラスは読めない
            // A freshly detected room is Out, so no class is readable after the round trip
            Assert.IsFalse(room.TryGetClass(out _, out _));
        }

        [Test]
        public void BreachedRoomReturnsOpeningCountTest()
        {
            var packet = CreatePacket();
            var filter = CleanRoomMachineTestHelpers.BuildSmallCleanRoomWithFilter();
            GameUpdater.UpdateOneTick();
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove);
            GameUpdater.UpdateOneTick();

            // 壁を1枚壊すと、清浄機から引いた応答は未密閉の型になり、漏れ1か所が載る
            // Breaking one wall turns the purifier's response into the not-enclosed type carrying one opening
            Assert.AreEqual(BlockInfoReadResult.Success, Invoke(packet, filter.BlockInstanceId).TryReadView(out var view));
            Assert.IsInstanceOf<CleanRoomNotEnclosedView>(view);
            var leak = ((CleanRoomNotEnclosedView)view).Leak;
            Assert.IsInstanceOf<CleanRoomOpeningsLeakView>(leak);
            var openings = (CleanRoomOpeningsLeakView)leak;
            Assert.AreEqual(1, openings.TotalOpeningCount);
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
            Assert.AreEqual(BlockInfoReadResult.Unreadable, GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateEnclosed(null).TryReadView(out _));

            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomInfo\] NotEnclosed response has no leak"));
            Assert.AreEqual(BlockInfoReadResult.Unreadable, GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateNotEnclosed(null).TryReadView(out _));

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
            var nextClass = CleanRoomNextClassMessagePack.CreateNotCounted();
            var queriedAirFilter = CleanRoomQueriedAirFilterMessagePack.CreateNotAirFilter();
            var filters = new List<CleanRoomAirFilterMessagePack>();
            AssertUnreadable(new CleanRoomSummaryMessagePack((CleanRoomClassResponseKind)999, null, 0, 0, 1, filters, 0, 0, nextClass, queriedAirFilter));
            filters.Add(null);
            AssertUnreadable(CleanRoomSummaryMessagePack.CreateOut(0, 1, filters, 0, 0, nextClass, queriedAirFilter));
            filters[0] = new CleanRoomAirFilterMessagePack(null, 1, 1, 1, 1);
            AssertUnreadable(CleanRoomSummaryMessagePack.CreateOut(0, 1, filters, 0, 0, nextClass, queriedAirFilter));

            // 入れ子をワイヤーで往復し、欠損と未知の種別を拒否する
            // Round-trip nested fields over the wire and reject missing or unknown kinds
            filters.Clear();
            AssertUnreadable(CleanRoomSummaryMessagePack.CreateOut(0, 1, filters, 0, 0, null, queriedAirFilter));
            AssertUnreadable(CleanRoomSummaryMessagePack.CreateOut(0, 1, filters, 0, 0, new CleanRoomNextClassMessagePack((CleanRoomNextClassResponseKind)999, null, 0), queriedAirFilter));
            AssertUnreadable(CleanRoomSummaryMessagePack.CreateOut(0, 1, filters, 0, 0, nextClass, null));
            AssertUnreadable(CleanRoomSummaryMessagePack.CreateOut(0, 1, filters, 0, 0, nextClass, new CleanRoomQueriedAirFilterMessagePack((CleanRoomQueriedAirFilterResponseKind)999, 0, 0)));
            var yieldResponse = new GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack(GetCleanRoomYieldInfoProtocol.ProtocolTag, 0, (CleanRoomYieldResponseKind)999,
                null, null, null, null, null, null, null);
            Assert.AreEqual(BlockInfoReadResult.Unreadable, yieldResponse.TryReadView(out _));
            var stall = GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateNotStalled();
            stall.Kind = (MachineInputStallResponseKind)999;
            Assert.AreEqual(BlockInfoReadResult.Unreadable, stall.TryReadView(out _));
        }

        [Test]
        public void MismatchedQuerySourceKindsAreUnreadableWithWarningTest()
        {
            // 次のクラスが NotCounted なのに自身の状態がある応答と、台数があるのに NotAirFilter の応答は、2つの種別が食い違った壊れた wire なので警告して読めない
            // A response with NotCounted but its own state, or with units but NotAirFilter, is broken wire whose two kinds disagree, so it warns and is unreadable
            var uncountedWithStatus = CleanRoomSummaryMessagePack.CreateOut(0, 1, new List<CleanRoomAirFilterMessagePack>(), 1, 0, CleanRoomNextClassMessagePack.CreateNotCounted(), CleanRoomQueriedAirFilterMessagePack.CreateAirFilter(1, 1));
            ExpectDisagreementWarnings();
            Assert.AreEqual(BlockInfoReadResult.Unreadable, RoundTrip(uncountedWithStatus).TryReadView(out _));
            var countedWithoutStatus = CleanRoomSummaryMessagePack.CreateOut(0, 1, new List<CleanRoomAirFilterMessagePack>(), 1, 0, CleanRoomNextClassMessagePack.CreateUnitsToNextClass("D", 1), CleanRoomQueriedAirFilterMessagePack.CreateNotAirFilter());
            ExpectDisagreementWarnings();
            Assert.AreEqual(BlockInfoReadResult.Unreadable, RoundTrip(countedWithoutStatus).TryReadView(out _));

            // そろった組（機械からの照会・清浄機からの照会）はどちらも読める
            // Agreeing pairs (queried from a machine, queried from a purifier) are both readable
            var fromMachine = CleanRoomSummaryMessagePack.CreateOut(0, 1, new List<CleanRoomAirFilterMessagePack>(), 1, 0, CleanRoomNextClassMessagePack.CreateNotCounted(), CleanRoomQueriedAirFilterMessagePack.CreateNotAirFilter());
            Assert.AreEqual(BlockInfoReadResult.Success, RoundTrip(fromMachine).TryReadView(out _));
            var fromPurifier = CleanRoomSummaryMessagePack.CreateOut(0, 1, new List<CleanRoomAirFilterMessagePack>(), 1, 0, CleanRoomNextClassMessagePack.CreateUnitsToNextClass("D", 1), CleanRoomQueriedAirFilterMessagePack.CreateAirFilter(1, 1));
            Assert.AreEqual(BlockInfoReadResult.Success, RoundTrip(fromPurifier).TryReadView(out _));

            #region Internal

            // 食い違いの警告と、部屋が読めない警告の2つが開発者向けのログに出る
            // Both the disagreement warning and the unreadable-room warning reach the developer log
            static void ExpectDisagreementWarnings()
            {
                LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomInfo\] Room query-source kinds disagree"));
                LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomInfo\] Enclosed response has a missing or broken room"));
            }

            #endregion
        }

        private static void AssertUnreadable(CleanRoomSummaryMessagePack room)
        {
            Assert.AreEqual(BlockInfoReadResult.Unreadable, RoundTrip(room).TryReadView(out _));
        }

        private static GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack RoundTrip(CleanRoomSummaryMessagePack room)
        {
            var response = GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateEnclosed(room);
            return MessagePackSerializer.Deserialize<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack>(MessagePackSerializer.Serialize(response));
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
