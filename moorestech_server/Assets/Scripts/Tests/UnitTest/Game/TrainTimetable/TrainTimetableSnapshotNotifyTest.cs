using Game.Context;
using Game.Train.Event;
using Game.Train.Unit;
using NUnit.Framework;
using Tests.Util;
using UniRx;

namespace Tests.UnitTest.Game.TrainTimetable
{
    public class TrainTimetableSnapshotNotifyTest
    {
        [Test]
        public void SnapshotIsNotifiedOnceAfterCurrentEntryAdvances()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            var updateService = ServerContext.GetService<TrainUpdateService>();
            var notify = ServerContext.GetService<ITrainUnitSnapshotNotifyEvent>();
            var initialIndex = train.trainDiagram.CurrentIndex;
            var notificationCount = 0;
            var notificationTick = 0u;
            var preSimulationTick = 0u;

            using var diffSubscription = updateService.OnPreSimulationDiffEvent.Subscribe(data =>
            {
                preSimulationTick = data.Item1;
            });

            using var subscription = notify.OnTrainUnitSnapshotNotified.Subscribe(data =>
            {
                if (data.TrainUnitInstanceId != train.TrainUnitInstanceId || data.IsDeleted)
                {
                    return;
                }

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

            updateService.UpdateTrains();
            Assert.AreEqual(1, notificationCount, "次のtickでは重複通知しない");
        }
    }
}
