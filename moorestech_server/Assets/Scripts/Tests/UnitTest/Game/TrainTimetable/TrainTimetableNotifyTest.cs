using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Context;
using Game.Train.Diagram;
using Game.Train.Event;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using Server.Util.MessagePack;
using Tests.Util;
using UniRx;

namespace Tests.UnitTest.Game.TrainTimetable
{
    public class TrainTimetableNotifyTest
    {
        [Test]
        public void AutoRunNotificationOnlyTracksRealTransitions()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            var notifications = 0;
            using var subscription = ServerContext.GetService<ITrainTimetableNotifyEvent>().OnTimetableChanged
                .Subscribe(notified =>
                {
                    if (notified.TrainUnitInstanceId == train.TrainUnitInstanceId) notifications++;
                });

            // すでにONの列車を再度ONにしても状態は変わらない
            // Turning ON an already running train does not change its state
            Assert.IsTrue(train.IsAutoRun);
            train.TurnOnAutoRun();
            Assert.AreEqual(0, notifications);

            train.TurnOffAutoRun();
            Assert.AreEqual(1, notifications);
            train.TurnOffAutoRun();
            Assert.AreEqual(1, notifications);
        }

        [Test]
        public void FailedAutoRunRequestDoesNotNotify()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            train.ReplaceTimetable(new List<TrainDiagramStopPlan>());
            Assert.IsFalse(train.IsAutoRun);
            var notifications = 0;
            using var subscription = ServerContext.GetService<ITrainTimetableNotifyEvent>().OnTimetableChanged
                .Subscribe(notified =>
                {
                    if (notified.TrainUnitInstanceId == train.TrainUnitInstanceId) notifications++;
                });

            // 目的地が無いのでONにできず、検証中の一時的なOFFも外へ出さない
            // The request cannot take effect without a destination, and the transient OFF stays inside
            train.TurnOnAutoRun();

            Assert.IsFalse(train.IsAutoRun);
            Assert.AreEqual(0, notifications);
        }

        [Test]
        public void TimetableIsNotifiedOnceWhenCurrentEntryAdvances()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            var updateService = ServerContext.GetService<TrainUpdateService>();
            var timetableNotify = ServerContext.GetService<ITrainTimetableNotifyEvent>();
            var snapshotNotify = ServerContext.GetService<ITrainUnitSnapshotNotifyEvent>();
            var initialIndex = train.trainDiagram.CurrentIndex;
            var notificationCount = 0;
            var snapshotCount = 0;

            using var snapshotSubscription = snapshotNotify.OnTrainUnitSnapshotNotified.Subscribe(data =>
            {
                if (data.TrainUnitInstanceId == train.TrainUnitInstanceId) snapshotCount++;
            });

            using var subscription = timetableNotify.OnTimetableChanged.Subscribe(trainUnit =>
            {
                if (trainUnit.TrainUnitInstanceId != train.TrainUnitInstanceId) return;
                notificationCount++;
            });

            // 停車中の待機を進め、発車による現在地の変更を待つ
            // Advance the docking wait until departure changes the current stop
            for (var i = 0; i < 450 && train.trainDiagram.CurrentIndex == initialIndex; i++)
            {
                updateService.UpdateTrains();
            }

            Assert.AreNotEqual(initialIndex, train.trainDiagram.CurrentIndex, "時刻表の現在地が進む");
            Assert.AreEqual(1, notificationCount, "現在地が進んだ時点で一度だけ通知する");
            Assert.AreEqual(0, snapshotCount, "時刻表の前進で列車の走行同期を送らない");

            updateService.UpdateTrains();
            Assert.AreEqual(1, notificationCount, "次のtickでは重複通知しない");
        }

        [Test]
        public void DepartingTowardDisconnectedStopNotifiesAutoRunOffOnce()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            var diagram = train.trainDiagram;
            var nextStation = scenario.AddConnectedDestinationStation();
            train.ReplaceTimetable(new List<TrainDiagramStopPlan>
            {
                new(scenario.StationExitFront, TrainDiagram.DepartureConditionType.WaitForTicks, GameUpdater.TicksPerSecond),
                new(nextStation, TrainDiagram.DepartureConditionType.WaitForTicks, GameUpdater.TicksPerSecond),
            });
            var stationEntry = (RailNode)scenario.StationExitFront.ConnectedNodes.First(node =>
                node.StationRef.HasStation && node.StationRef.StationPosition == nextStation.StationRef.StationPosition);
            scenario.StationExitFront.DisconnectNode(stationEntry);
            diagram.Entries[0].SetDepartureWaitTicks(1);

            var updateService = ServerContext.GetService<TrainUpdateService>();
            var timetableNotify = ServerContext.GetService<ITrainTimetableNotifyEvent>();
            var snapshotNotify = ServerContext.GetService<ITrainUnitSnapshotNotifyEvent>();
            var offNotifications = 0;
            var snapshotCount = 0;
            using var snapshotSubscription = snapshotNotify.OnTrainUnitSnapshotNotified.Subscribe(data =>
            {
                if (data.TrainUnitInstanceId == train.TrainUnitInstanceId) snapshotCount++;
            });
            using var subscription = timetableNotify.OnTimetableChanged.Subscribe(trainUnit =>
            {
                if (trainUnit.TrainUnitInstanceId == train.TrainUnitInstanceId && !trainUnit.IsAutoRun)
                {
                    offNotifications++;
                }
            });

            // 駅Aを出た後、未接続の次駅でOFFになったことを確認する
            // Check the OFF notification when the disconnected next stop is encountered
            for (var i = 0; i < 12000 && train.IsAutoRun; i++)
            {
                updateService.UpdateTrains();
            }
            Assert.IsFalse(train.IsAutoRun);
            Assert.AreEqual(1, offNotifications);
            Assert.AreEqual(0, snapshotCount, "時刻表の前進で列車の走行同期を送らない");
            updateService.UpdateTrains();
            Assert.AreEqual(1, offNotifications);
        }

        [Test]
        public void DisconnectingRailWhileRunningNotifiesAutoRunOffOnce()
        {
            using var scenario = TrainAutoRunTestScenario.CreateRunningScenario();
            var train = scenario.Train;
            var next = (RailNode)train.trainDiagram.Entries[1].Node;
            train.trainDiagram.MoveToNextEntry();
            scenario.StationExitFront.DisconnectNode(next);

            var updateService = ServerContext.GetService<TrainUpdateService>();
            var timetableNotify = ServerContext.GetService<ITrainTimetableNotifyEvent>();
            var snapshotNotify = ServerContext.GetService<ITrainUnitSnapshotNotifyEvent>();
            var offNotifications = 0;
            var snapshotCount = 0;
            using var snapshotSubscription = snapshotNotify.OnTrainUnitSnapshotNotified.Subscribe(data =>
            {
                if (data.TrainUnitInstanceId == train.TrainUnitInstanceId) snapshotCount++;
            });
            using var subscription = timetableNotify.OnTimetableChanged.Subscribe(trainUnit =>
            {
                if (trainUnit.TrainUnitInstanceId == train.TrainUnitInstanceId && !trainUnit.IsAutoRun)
                {
                    offNotifications++;
                }
            });

            // 走行中の線路切断によるOFFをその場で通知する
            // The OFF caused by a disconnected rail while running is notified on the spot
            for (var i = 0; i < 12000 && train.IsAutoRun; i++)
            {
                updateService.UpdateTrains();
            }
            Assert.IsFalse(train.IsAutoRun);
            Assert.AreEqual(1, offNotifications);
            Assert.AreEqual(0, snapshotCount, "時刻表の前進で列車の走行同期を送らない");
            updateService.UpdateTrains();
            Assert.AreEqual(1, offNotifications);
        }

        [Test]
        public void SimulationSnapshotWireFormHasNoTimetableFields()
        {
            var keys = typeof(TrainSimulationSnapshotMessagePack).GetProperties()
                .Select(property => property.Name).ToArray();
            CollectionAssert.DoesNotContain(keys, "IsAutoRun");
            CollectionAssert.DoesNotContain(keys, "TimetableCurrentIndex");
            CollectionAssert.DoesNotContain(keys, "TimetableStops");
        }
    }
}
