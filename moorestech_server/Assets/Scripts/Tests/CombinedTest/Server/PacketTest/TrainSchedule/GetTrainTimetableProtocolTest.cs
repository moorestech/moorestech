using System.Linq;
using Core.Update;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetTrainTimetableProtocolTest
    {
        [Test]
        public void ReturnsTimetableWithSides()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var station = fixture.PlaceStation(new Vector3Int(0, 0, 40));
            var position = station.BlockPositionInfo.OriginalPos;
            Assert.IsTrue(fixture.Send(TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(
                fixture.Train.TrainUnitInstanceId,
                new[] { new TrainTimetableStop(position, StationNodeSide.Front, TrainDiagram.DepartureConditionType.WaitForTicks, GameUpdater.TicksPerSecond) })).Success);

            var response = Get(fixture, fixture.Train.TrainUnitInstanceId);

            Assert.IsNotNull(response.Timetable);
            Assert.AreEqual(fixture.Train.IsAutoRun, response.Timetable.IsAutoRun);
            Assert.AreEqual(0, response.Timetable.CurrentIndex);
            Assert.AreEqual(position, response.Timetable.Stops.Single().StationPosition.Vector3Int);
            Assert.AreEqual(TrainTimetableStopSideWireValue.Front, response.Timetable.Stops.Single().Side);
            Assert.AreEqual(TrainTimetableDepartureConditionWireValue.WaitForTicks, response.Timetable.Stops.Single().DepartureCondition);
            Assert.AreEqual(GameUpdater.TicksPerSecond, response.Timetable.Stops.Single().WaitTicks);
        }

        [Test]
        public void UnknownTrainReturnsNotFound()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var response = Get(fixture, TrainUnitInstanceId.Create());
            Assert.IsNull(response.Timetable);
        }

        private static GetTrainTimetableProtocol.GetTrainTimetableResponse Get(TrainScheduleProtocolTestEnvironment fixture, TrainUnitInstanceId id)
        {
            var payload = MessagePackSerializer.Serialize(new GetTrainTimetableProtocol.GetTrainTimetableRequest(id));
            var responses = fixture.Environment.PacketResponseCreator.GetPacketResponse(payload, new PacketResponseContext(null));
            return MessagePackSerializer.Deserialize<GetTrainTimetableProtocol.GetTrainTimetableResponse>(responses[0]);
        }
    }
}
