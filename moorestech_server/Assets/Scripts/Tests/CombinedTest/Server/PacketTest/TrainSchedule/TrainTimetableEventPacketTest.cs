using System.Linq;
using Core.Update;
using Game.PlayerInventory.Interface.Subscription;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
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
        public void ReplaceSendsTimetableEventToViewerWithSidesAndOneMotionResync()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var sink = EventTestUtil.RegisterCaptureSink(fixture.Environment.ServiceProvider, PlayerId);
            SubscribeTrainInventory(fixture, PlayerId);
            var station = fixture.PlaceStation(new Vector3Int(0, 0, 40));
            var position = station.BlockPositionInfo.OriginalPos;

            var response = fixture.Send(TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(
                fixture.Train.TrainUnitInstanceId,
                new[] { new TrainTimetableStop(position, StationNodeSide.Front, TrainDiagram.DepartureConditionType.WaitForTicks, GameUpdater.TicksPerSecond) }));
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
            Assert.AreEqual(TrainTimetableStopSideWireValue.Front, payload.Stops.Single().Side);
            Assert.AreEqual(GameUpdater.TicksPerSecond, payload.Stops.Single().WaitTicks);
        }

        // 列車を開いていないプレイヤーへは配らない
        // A player who is not viewing the train receives nothing
        [Test]
        public void TimetableEventIsNotSentToPlayersWithoutTheTrainOpen()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var sink = EventTestUtil.RegisterCaptureSink(fixture.Environment.ServiceProvider, PlayerId);
            var station = fixture.PlaceStation(new Vector3Int(0, 0, 40));

            var response = fixture.Send(TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(
                fixture.Train.TrainUnitInstanceId,
                new[]
                {
                    new TrainTimetableStop(station.BlockPositionInfo.OriginalPos, StationNodeSide.Front, TrainDiagram.DepartureConditionType.WaitForTicks, GameUpdater.TicksPerSecond),
                }));
            Assert.IsTrue(response.Success);

            var events = sink.TakeAll();
            Assert.AreEqual(0, events.Count(e => e.Tag == TrainTimetableEventPacket.EventTag));
        }

        private static void SubscribeTrainInventory(TrainScheduleProtocolTestEnvironment fixture, int playerId)
        {
            var subscriptionStore = fixture.Environment.ServiceProvider.GetRequiredService<IInventorySubscriptionStore>();
            foreach (var car in fixture.Train.Cars)
            {
                subscriptionStore.Subscribe(playerId, new TrainInventorySubInventoryIdentifier(car.TrainCarInstanceId.AsPrimitive()));
            }
        }
    }
}
