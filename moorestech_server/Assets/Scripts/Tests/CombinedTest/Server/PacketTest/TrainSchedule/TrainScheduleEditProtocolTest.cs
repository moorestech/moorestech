using System;
using Core.Update;
using Game.Block.Interface;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using Request = Server.Protocol.PacketResponse.TrainScheduleEditProtocol.TrainScheduleEditRequest;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class TrainScheduleEditProtocolTest
    {
        [Test]
        public void ReplaceDisconnectedStationsPreservesAutoRunAndNotifiesOnce()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var first = fixture.PlaceStation(new Vector3Int(100, 0, 0));
            var second = fixture.PlaceStation(new Vector3Int(200, 0, 0));
            var notifications = 0;
            using var subscription = fixture.Notifications.OnTrainUnitSnapshotNotified.Subscribe(data =>
            {
                Assert.AreSame(fixture.Train, data.TrainUnit);
                Assert.AreEqual(2, data.TrainUnit.trainDiagram.Entries.Count);
                notifications++;
            });

            // 外部接続のない駅でも入力順で受理する
            // Accept stations without external connections in the requested order
            var response = fixture.Send(Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId,
                new[] { first.BlockPositionInfo.OriginalPos, second.BlockPositionInfo.OriginalPos }));

            Assert.IsTrue(response.Success);
            Assert.AreEqual(TrainScheduleEditFailureReason.None, response.FailureReason);
            Assert.AreEqual(TrainScheduleEditOperation.ReplaceTimetable, response.Operation);
            var diagram = fixture.Train.trainDiagram;
            Assert.AreEqual(0, diagram.CurrentIndex);
            Assert.AreEqual(2, diagram.Entries.Count);
            Assert.AreSame(first, diagram.Entries[0].Node.StationRef.StationBlock);
            Assert.AreSame(second, diagram.Entries[1].Node.StationRef.StationBlock);
            foreach (var entry in diagram.Entries)
            {
                Assert.AreEqual(StationNodeSide.Back, entry.Node.StationRef.NodeSide);
                Assert.AreEqual(StationNodeRole.Exit, entry.Node.StationRef.NodeRole);
                Assert.AreEqual(GameUpdater.TicksPerSecond, entry.GetWaitForTicksInitialTicks());
            }
            Assert.IsTrue(fixture.Train.IsAutoRun);
            Assert.AreEqual(1, notifications);
            Assert.IsFalse(diagram.ConsumeCurrentEntryChanged());
        }

        [TestCase(true, TrainScheduleEditFailureReason.NotTrainStation)]
        [TestCase(false, TrainScheduleEditFailureReason.StationBlockNotFound)]
        public void InvalidLaterStationRejectsEntireReplacement(bool placePlatform, TrainScheduleEditFailureReason reason)
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var valid = fixture.PlaceStation(new Vector3Int(100, 0, 0));
            var invalidPosition = new Vector3Int(200, 0, 0);
            if (placePlatform)
            {
                TrainTestHelper.PlaceBlock(fixture.Environment, ForUnitTestModBlockId.TestTrainItemPlatform, invalidPosition, BlockDirection.North);
            }
            var before = fixture.Train.trainDiagram.Entries[0];
            var notifications = 0;
            using var subscription = fixture.Notifications.OnTrainUnitSnapshotNotified.Subscribe(_ => notifications++);

            // 後続駅の拒否でも部分適用と通知は発生させない
            // Rejecting a later station must not partially apply or notify
            LogAssert.Expect(LogType.Warning, new Regex($"\\[TrainScheduleEdit\\] rejected.*reason={reason}"));
            var response = fixture.Send(Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId,
                new[] { valid.BlockPositionInfo.OriginalPos, invalidPosition }));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(reason, response.FailureReason);
            Assert.AreEqual(1, fixture.Train.trainDiagram.Entries.Count);
            Assert.AreSame(before, fixture.Train.trainDiagram.Entries[0]);
            Assert.IsTrue(fixture.Train.IsAutoRun);
            Assert.AreEqual(0, notifications);
        }

        [Test]
        public void SecondReplacementWinsAndDuplicateStationsAreAllowed()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var first = fixture.PlaceStation(new Vector3Int(100, 0, 0)).BlockPositionInfo.OriginalPos;
            var second = fixture.PlaceStation(new Vector3Int(200, 0, 0)).BlockPositionInfo.OriginalPos;
            Assert.IsTrue(fixture.Send(Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId, new[] { first })).Success);
            Assert.IsTrue(fixture.Send(Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId, new[] { second, second })).Success);

            Assert.AreEqual(2, fixture.Train.trainDiagram.Entries.Count);
            Assert.AreEqual(second, fixture.Train.trainDiagram.Entries[0].Node.StationRef.StationBlock.BlockPositionInfo.OriginalPos);
            Assert.AreSame(fixture.Train.trainDiagram.Entries[0].Node, fixture.Train.trainDiagram.Entries[1].Node);
        }

        [Test]
        public void AutoRunToggleNotifiesAppliedState()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var states = new System.Collections.Generic.List<bool>();
            using var subscription = fixture.Notifications.OnTrainUnitSnapshotNotified.Subscribe(data => states.Add(data.TrainUnit.IsAutoRun));

            // 両操作の適用済み状態が通知される
            // Notifications expose the applied state for both toggles
            var off = fixture.Send(Request.CreateSetAutoRunRequest(fixture.Train.TrainUnitInstanceId, false));
            Assert.IsTrue(off.Success);
            Assert.IsFalse(fixture.Train.IsAutoRun);
            var on = fixture.Send(Request.CreateSetAutoRunRequest(fixture.Train.TrainUnitInstanceId, true));
            Assert.IsTrue(on.Success);
            Assert.IsTrue(fixture.Train.IsAutoRun);
            Assert.AreEqual(TrainScheduleEditOperation.SetAutoRun, on.Operation);
            CollectionAssert.AreEqual(new[] { false, true }, states);
        }

        [Test]
        public void EmptyTimetableAutoRunIsAcceptedAndStops()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            Assert.IsTrue(fixture.Send(Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId, Array.Empty<Vector3Int>())).Success);
            Assert.AreEqual(-1, fixture.Train.trainDiagram.CurrentIndex);
            var response = fixture.Send(Request.CreateSetAutoRunRequest(fixture.Train.TrainUnitInstanceId, true));

            Assert.IsTrue(response.Success);
            fixture.Train.Update();
            Assert.IsFalse(fixture.Train.IsAutoRun);
        }

        [Test]
        public void UnknownTrainIsRejected()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            LogAssert.Expect(LogType.Warning, new Regex("\\[TrainScheduleEdit\\] rejected.*reason=TrainNotFound"));
            var response = fixture.Send(Request.CreateSetAutoRunRequest(new TrainUnitInstanceId(Guid.NewGuid()), true));
            Assert.IsFalse(response.Success);
            Assert.AreEqual(TrainScheduleEditFailureReason.TrainNotFound, response.FailureReason);
        }
    }
}
