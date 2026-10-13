using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Update;
using Game.Train.Diagram;
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
        // 正規のfactoryが作る停車駅1件を組み立てる
        // Build one valid stop exactly as the regular factory would
        private static TrainTimetableStopMessagePack ValidStop(Vector3Int position)
        {
            return new TrainTimetableStopMessagePack(new TrainTimetableStop(
                position, StationNodeSide.Front, TrainDiagram.DepartureConditionType.WaitForTicks, GameUpdater.TicksPerSecond));
        }

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

        // 既定値0の操作種別は「未指定」として拒否する
        // Operation 0 is the unspecified default and is rejected
        [Test]
        public void UnspecifiedOperationIsRejected()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var request = Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId, new TrainTimetableStop[0]);
            request.Operation = TrainScheduleEditOperation.Unspecified;
            var originalEntry = fixture.Train.trainDiagram.Entries[0];
            var notifications = 0;
            using var subscription = fixture.TimetableNotifications.OnTimetableChanged.Subscribe(_ => notifications++);
            LogAssert.Expect(LogType.Warning, new Regex("\\[TrainScheduleEdit\\] rejected.*reason=InvalidRequest"));

            var response = fixture.Send(request);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(TrainScheduleEditFailureReason.InvalidRequest, response.FailureReason);
            Assert.AreSame(originalEntry, fixture.Train.trainDiagram.Entries[0]);
            Assert.AreEqual(0, notifications);
        }

        // 既定値0の入線側は「未指定」として拒否する
        // Stop side 0 is the unspecified default and is rejected
        [Test]
        public void UnspecifiedStationSideIsRejected()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var station = fixture.PlaceStation(new Vector3Int(100, 0, 0));
            var stop = ValidStop(station.BlockPositionInfo.OriginalPos);
            stop.Side = TrainTimetableStopSideWireValue.Unspecified;

            AssertStopIsRejected(fixture, stop, TrainScheduleEditFailureReason.InvalidStationSide);
        }

        [Test]
        public void InvalidStationSideIsRejected()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var station = fixture.PlaceStation(new Vector3Int(100, 0, 0));
            var stop = ValidStop(station.BlockPositionInfo.OriginalPos);
            stop.Side = (TrainTimetableStopSideWireValue)99;

            AssertStopIsRejected(fixture, stop, TrainScheduleEditFailureReason.InvalidStationSide);
        }

        [Test]
        public void UnspecifiedDepartureConditionIsRejected()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var station = fixture.PlaceStation(new Vector3Int(100, 0, 0));
            var stop = ValidStop(station.BlockPositionInfo.OriginalPos);
            stop.DepartureCondition = TrainTimetableDepartureConditionWireValue.Unspecified;

            AssertStopIsRejected(fixture, stop, TrainScheduleEditFailureReason.InvalidRequest);
        }

        private static void AssertStopIsRejected(
            TrainScheduleProtocolTestEnvironment fixture, TrainTimetableStopMessagePack stop, TrainScheduleEditFailureReason reason)
        {
            // 未指定・未定義のenum値を積んだ外部入力を再現する
            // Reproduce external input carrying unspecified or undefined enum values
            var request = Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId, new TrainTimetableStop[0]);
            request.Stops = new List<TrainTimetableStopMessagePack> { stop };

            var originalEntry = fixture.Train.trainDiagram.Entries[0];
            var notifications = 0;
            using var subscription = fixture.TimetableNotifications.OnTimetableChanged.Subscribe(_ => notifications++);
            LogAssert.Expect(LogType.Warning, new Regex($"\\[TrainScheduleEdit\\] rejected.*reason={reason}"));

            var response = fixture.Send(request);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(reason, response.FailureReason);
            Assert.AreSame(originalEntry, fixture.Train.trainDiagram.Entries[0]);
            Assert.AreEqual(0, notifications);
        }
    }
}
