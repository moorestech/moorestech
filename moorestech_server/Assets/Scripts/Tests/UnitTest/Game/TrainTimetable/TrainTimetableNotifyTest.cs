using System.Collections.Generic;
using System.Linq;
using Game.Context;
using Game.Train.Event;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using Tests.Util;
using UniRx;

namespace Tests.UnitTest.Game.TrainTimetable
{
    public class TrainTimetableNotifyTest
    {
        [Test]
        public void AutoRunChangeFlagOnlyTracksTransitions()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            Assert.IsTrue(train.ConsumeAutoRunChanged());
            Assert.IsFalse(train.ConsumeAutoRunChanged());

            train.TurnOnAutoRun();
            Assert.IsFalse(train.ConsumeAutoRunChanged());
            train.TurnOffAutoRun();
            Assert.IsTrue(train.ConsumeAutoRunChanged());
            train.TurnOffAutoRun();
            Assert.IsFalse(train.ConsumeAutoRunChanged());
        }

        [Test]
        public void TimetableIsNotifiedOnceAfterCurrentEntryAdvances()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            var updateService = ServerContext.GetService<TrainUpdateService>();
            var timetableNotify = ServerContext.GetService<ITrainTimetableNotifyEvent>();
            var snapshotNotify = ServerContext.GetService<ITrainUnitSnapshotNotifyEvent>();
            var initialIndex = train.trainDiagram.CurrentIndex;
            train.ConsumeAutoRunChanged();
            var notificationCount = 0;
            var notificationTick = 0u;
            var preSimulationTick = 0u;
            var snapshotCount = 0;

            using var diffSubscription = updateService.OnPreSimulationDiffEvent.Subscribe(data =>
            {
                preSimulationTick = data.Item1;
            });

            using var snapshotSubscription = snapshotNotify.OnTrainUnitSnapshotNotified.Subscribe(data =>
            {
                if (data.TrainUnitInstanceId == train.TrainUnitInstanceId) snapshotCount++;
            });

            using var subscription = timetableNotify.OnTimetableChanged.Subscribe(trainUnit =>
            {
                if (trainUnit.TrainUnitInstanceId != train.TrainUnitInstanceId) return;

                notificationCount++;
                notificationTick = updateService.GetCurrentTick();
            });

            // 停車中の待機を進め、発車による現在地の変更を待つ
            // Advance the docking wait until departure changes the current stop
            for (var i = 0; i < 450 && train.trainDiagram.CurrentIndex == initialIndex; i++)
            {
                updateService.UpdateTrains();
            }

            Assert.AreNotEqual(initialIndex, train.trainDiagram.CurrentIndex, "時刻表の現在地が進む");
            Assert.AreEqual(1, notificationCount, "現在地が進んだtickに一度だけ通知する");
            Assert.AreEqual(preSimulationTick, notificationTick, "同じtickのシミュレーション後に通知する");
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
            diagram.ReplaceEntries(new IRailNode[] { scenario.StationExitFront, nextStation });
            var stationEntry = (RailNode)scenario.StationExitFront.ConnectedNodes.First(node =>
                node.StationRef.HasStation && node.StationRef.StationPosition == nextStation.StationRef.StationPosition);
            scenario.StationExitFront.DisconnectNode(stationEntry);
            diagram.Entries[0].SetDepartureWaitTicks(1);
            diagram.ConsumeCurrentEntryChanged();
            train.ConsumeAutoRunChanged();

            var updateService = ServerContext.GetService<TrainUpdateService>();
            var timetableNotify = ServerContext.GetService<ITrainTimetableNotifyEvent>();
            var snapshotNotify = ServerContext.GetService<ITrainUnitSnapshotNotifyEvent>();
            var offTicks = new List<uint>();
            var snapshotCount = 0;
            using var snapshotSubscription = snapshotNotify.OnTrainUnitSnapshotNotified.Subscribe(data =>
            {
                if (data.TrainUnitInstanceId == train.TrainUnitInstanceId) snapshotCount++;
            });
            using var subscription = timetableNotify.OnTimetableChanged.Subscribe(trainUnit =>
            {
                if (trainUnit.TrainUnitInstanceId == train.TrainUnitInstanceId && !trainUnit.IsAutoRun)
                {
                    offTicks.Add(updateService.GetCurrentTick());
                }
            });

            // 駅Aを出た後、未接続の次駅でOFFになったtickを確認する
            // Check the OFF snapshot on the tick that encounters the disconnected next stop
            for (var i = 0; i < 12000 && train.IsAutoRun; i++)
            {
                updateService.UpdateTrains();
            }
            Assert.IsFalse(train.IsAutoRun);
            Assert.AreEqual(1, offTicks.Count);
            Assert.AreEqual(updateService.GetCurrentTick(), offTicks[0]);
            Assert.AreEqual(0, snapshotCount, "時刻表の前進で列車の走行同期を送らない");
            updateService.UpdateTrains();
            Assert.AreEqual(1, offTicks.Count);
        }

        [Test]
        public void DisconnectingRailWhileRunningNotifiesAutoRunOffOnce()
        {
            using var scenario = TrainAutoRunTestScenario.CreateRunningScenario();
            var train = scenario.Train;
            var next = (RailNode)train.trainDiagram.Entries[1].Node;
            train.trainDiagram.MoveToNextEntry();
            scenario.StationExitFront.DisconnectNode(next);
            train.trainDiagram.ConsumeCurrentEntryChanged();
            train.ConsumeAutoRunChanged();

            var updateService = ServerContext.GetService<TrainUpdateService>();
            var timetableNotify = ServerContext.GetService<ITrainTimetableNotifyEvent>();
            var snapshotNotify = ServerContext.GetService<ITrainUnitSnapshotNotifyEvent>();
            var offSnapshots = 0;
            var snapshotCount = 0;
            using var snapshotSubscription = snapshotNotify.OnTrainUnitSnapshotNotified.Subscribe(data =>
            {
                if (data.TrainUnitInstanceId == train.TrainUnitInstanceId) snapshotCount++;
            });
            using var subscription = timetableNotify.OnTimetableChanged.Subscribe(trainUnit =>
            {
                if (trainUnit.TrainUnitInstanceId == train.TrainUnitInstanceId && !trainUnit.IsAutoRun)
                {
                    offSnapshots++;
                }
            });

            // 走行中の線路切断によるOFFをシミュレーション後に通知する
            // Notify the post-simulation OFF state after a rail is disconnected while running
            for (var i = 0; i < 12000 && train.IsAutoRun; i++)
            {
                updateService.UpdateTrains();
            }
            Assert.IsFalse(train.IsAutoRun);
            Assert.AreEqual(1, offSnapshots);
            Assert.AreEqual(0, snapshotCount, "時刻表の前進で列車の走行同期を送らない");
            updateService.UpdateTrains();
            Assert.AreEqual(1, offSnapshots);
        }
    }
}
