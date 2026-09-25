using System;
using System.Linq;
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
        // 座標配列を末尾側(Back)停車のTrainTimetableStop配列へ変換する
        // Convert bare positions into Back-side TrainTimetableStop entries
        private static TrainTimetableStop[] Stops(params Vector3Int[] positions)
        {
            return positions.Select(p => new TrainTimetableStop(p, StationNodeSide.Back)).ToArray();
        }

        [Test]
        public void ReplaceDisconnectedStationsStopsAutoRunAndNotifiesOnce()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var first = fixture.PlaceStation(new Vector3Int(100, 0, 0));
            var second = fixture.PlaceStation(new Vector3Int(200, 0, 0));
            var timetableNotifications = 0;
            var snapshotNotifications = 0;
            using var timetableSubscription = fixture.TimetableNotifications.OnTimetableChanged.Subscribe(trainUnit =>
            {
                Assert.AreSame(fixture.Train, trainUnit);
                Assert.AreEqual(2, trainUnit.trainDiagram.Entries.Count);
                timetableNotifications++;
            });
            using var snapshotSubscription = fixture.SnapshotNotifications.OnTrainUnitSnapshotNotified.Subscribe(_ => snapshotNotifications++);

            // 外部接続のない駅でも入力順で受理する
            // Accept stations without external connections in the requested order
            var response = fixture.Send(Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId,
                Stops(first.BlockPositionInfo.OriginalPos, second.BlockPositionInfo.OriginalPos)));

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
            Assert.IsFalse(fixture.Train.IsAutoRun);
            // R2: 時刻表編集はtick同期snapshotを増やさず、専用イベントだけを1回発火する
            // R2: timetable edits fire only the dedicated event, never the tick-synced snapshot
            Assert.AreEqual(1, timetableNotifications);
            Assert.AreEqual(0, snapshotNotifications);
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
            using var subscription = fixture.TimetableNotifications.OnTimetableChanged.Subscribe(_ => notifications++);

            // 後続駅の拒否でも部分適用と通知は発生させない
            // Rejecting a later station must not partially apply or notify
            LogAssert.Expect(LogType.Warning, new Regex($"\\[TrainScheduleEdit\\] rejected.*reason={reason}"));
            var response = fixture.Send(Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId,
                Stops(valid.BlockPositionInfo.OriginalPos, invalidPosition)));

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
            Assert.IsTrue(fixture.Send(Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId, Stops(first))).Success);
            Assert.IsTrue(fixture.Send(Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId, Stops(second, second))).Success);

            Assert.AreEqual(2, fixture.Train.trainDiagram.Entries.Count);
            Assert.AreEqual(second, fixture.Train.trainDiagram.Entries[0].Node.StationRef.StationBlock.BlockPositionInfo.OriginalPos);
            Assert.AreSame(fixture.Train.trainDiagram.Entries[0].Node, fixture.Train.trainDiagram.Entries[1].Node);
        }

        [Test]
        public void AutoRunToggleNotifiesAppliedState()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var states = new System.Collections.Generic.List<bool>();
            var snapshotNotifications = 0;
            using var timetableSubscription = fixture.TimetableNotifications.OnTimetableChanged.Subscribe(trainUnit => states.Add(trainUnit.IsAutoRun));
            using var snapshotSubscription = fixture.SnapshotNotifications.OnTrainUnitSnapshotNotified.Subscribe(_ => snapshotNotifications++);

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
            // R2: 自動運転トグルもtick同期snapshotを増やさない
            // R2: the auto-run toggle also never touches the tick-synced snapshot
            Assert.AreEqual(0, snapshotNotifications);
        }

        [TestCase(StationNodeSide.Front)]
        [TestCase(StationNodeSide.Back)]
        public void ReplaceRegistersExitNodeOfRequestedSide(StationNodeSide side)
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var station = fixture.PlaceStation(new Vector3Int(0, 0, 40));
            var response = fixture.Send(Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId,
                new[] { new TrainTimetableStop(station.BlockPositionInfo.OriginalPos, side) }));

            Assert.IsTrue(response.Success);
            var node = fixture.Train.trainDiagram.Entries[0].Node;
            Assert.AreEqual(side, node.StationRef.NodeSide);
            Assert.AreEqual(StationNodeRole.Exit, node.StationRef.NodeRole);
        }

        [Test]
        public void EmptyTimetableAutoRunIsAcceptedAndStops()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            Assert.IsTrue(fixture.Send(Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId, Array.Empty<TrainTimetableStop>())).Success);
            Assert.AreEqual(-1, fixture.Train.trainDiagram.CurrentIndex);
            Assert.IsFalse(fixture.Train.IsAutoRun);
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
