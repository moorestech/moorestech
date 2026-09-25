using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using Request = Server.Protocol.PacketResponse.TrainScheduleEditProtocol.TrainScheduleEditRequest;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class TrainScheduleMalformedRequestTest
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void MalformedPayloadDoesNotMutateOrNotify(int invalidInput)
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var request = Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId, new TrainTimetableStop[0]);

            // クライアントのfactoryを迂回する不正な外部入力を再現する
            // Reproduce malformed external input bypassing the client factories
            switch (invalidInput)
            {
                case 0:
                    request.Operation = (TrainScheduleEditOperation)999;
                    break;
                case 1:
                    request.Stops = null;
                    break;
                case 2:
                    request.Stops = new List<TrainTimetableStopMessagePack> { null };
                    break;
            }
            var originalEntry = fixture.Train.trainDiagram.Entries[0];
            var notifications = 0;
            using var subscription = fixture.TimetableNotifications.OnTimetableChanged.Subscribe(_ => notifications++);
            LogAssert.Expect(LogType.Warning, new Regex("\\[TrainScheduleEdit\\] rejected.*reason=InvalidRequest"));

            var response = fixture.Send(request);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(TrainScheduleEditFailureReason.InvalidRequest, response.FailureReason);
            Assert.AreSame(originalEntry, fixture.Train.trainDiagram.Entries[0]);
            Assert.IsTrue(fixture.Train.IsAutoRun);
            Assert.AreEqual(0, notifications);
        }

        [Test]
        public void InvalidStationSideIsRejected()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var station = fixture.PlaceStation(new Vector3Int(100, 0, 0));
            var request = Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId, new TrainTimetableStop[0]);

            // 未定義のenum値を積んだ外部入力を再現する
            // Reproduce external input carrying an undefined enum value
            request.Stops = new List<TrainTimetableStopMessagePack>
            {
                new(new TrainTimetableStop(station.BlockPositionInfo.OriginalPos, (StationNodeSide)99)),
            };

            var originalEntry = fixture.Train.trainDiagram.Entries[0];
            var notifications = 0;
            using var subscription = fixture.TimetableNotifications.OnTimetableChanged.Subscribe(_ => notifications++);
            LogAssert.Expect(LogType.Warning, new Regex("\\[TrainScheduleEdit\\] rejected.*reason=InvalidStationSide"));

            var response = fixture.Send(request);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(TrainScheduleEditFailureReason.InvalidStationSide, response.FailureReason);
            Assert.AreSame(originalEntry, fixture.Train.trainDiagram.Entries[0]);
            Assert.AreEqual(0, notifications);
        }
    }
}
