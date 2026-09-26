using System.Collections.Generic;
using Client.Game.InGame.Train.Timetable;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.WebUiHost.Train
{
    public class TrainTimetableDatastoreNotificationTest
    {
        [Test]
        public void ApplyStoresTimetableAndNotifiesTrainId()
        {
            var datastore = new ClientTrainTimetableDatastore();
            var id = TrainUnitInstanceId.Create();
            var notified = new List<TrainUnitInstanceId>();
            using var subscription = datastore.OnTimetableUpdated.Subscribe(notified.Add);

            datastore.Apply(new TrainTimetableSnapshot(id, true, 0, new[] { new TrainTimetableStop(new Vector3Int(1, 2, 3), StationNodeSide.Front) }));

            Assert.That(notified, Is.EqualTo(new[] { id }));
            Assert.That(datastore.TryGet(id, out var stored), Is.True);
            Assert.That(stored.IsAutoRun, Is.True);
            Assert.That(stored.Stops[0].Side, Is.EqualTo(StationNodeSide.Front));
        }

        [Test]
        public void LaterApplyReplacesEarlierState()
        {
            var datastore = new ClientTrainTimetableDatastore();
            var id = TrainUnitInstanceId.Create();
            datastore.Apply(new TrainTimetableSnapshot(id, true, 0, new TrainTimetableStop[0]));
            datastore.Apply(new TrainTimetableSnapshot(id, false, -1, new TrainTimetableStop[0]));

            Assert.That(datastore.TryGet(id, out var stored), Is.True);
            Assert.That(stored.IsAutoRun, Is.False);
            Assert.That(stored.CurrentIndex, Is.EqualTo(-1));
        }
    }
}
