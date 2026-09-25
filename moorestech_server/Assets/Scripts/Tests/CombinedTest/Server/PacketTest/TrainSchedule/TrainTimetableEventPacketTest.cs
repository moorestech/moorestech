using System.Linq;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using NUnit.Framework;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.CombinedTest.Server.PacketTest.Event;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class TrainTimetableEventPacketTest
    {
        private const int PlayerId = 1;

        [Test]
        public void ReplaceBroadcastsTimetableEventWithSidesAndOneMotionResync()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var sink = EventTestUtil.RegisterCaptureSink(fixture.Environment.ServiceProvider, PlayerId);
            var station = fixture.PlaceStation(new Vector3Int(0, 0, 40));
            var position = station.BlockPositionInfo.OriginalPos;

            var response = fixture.Send(TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(
                fixture.Train.TrainUnitInstanceId, new[] { new TrainTimetableStop(position, StationNodeSide.Front) }));
            Assert.IsTrue(response.Success);

            var events = sink.TakeAll();

            // 時刻表イベントは1件。tick外で走行状態を書き換えたので走行snapshotも1件だけ送る
            // One timetable event; the off-tick motion change also sends exactly one motion snapshot
            var timetableEvents = events.Where(e => e.Tag == TrainTimetableEventPacket.EventTag).ToList();
            Assert.AreEqual(1, timetableEvents.Count);
            Assert.AreEqual(1, events.Count(e => e.Tag == TrainUnitSnapshotEventPacket.EventTag));

            var payload = MessagePackSerializer.Deserialize<TrainTimetableMessagePack>(timetableEvents[0].Payload);
            Assert.AreEqual(fixture.Train.TrainUnitInstanceId, payload.TrainUnitInstanceId);
            Assert.AreEqual(position, payload.Stops.Single().StationPosition.Vector3Int);
            Assert.AreEqual(StationNodeSide.Front, payload.Stops.Single().Side);
        }
    }
}
