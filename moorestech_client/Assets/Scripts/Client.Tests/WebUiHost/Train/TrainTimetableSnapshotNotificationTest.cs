using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Game.Train.Unit;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.WebUiHost
{
    public class TrainTimetableSnapshotNotificationTest
    {
        [Test]
        public void UpsertNotifiesAfterTimetableStateIsApplied()
        {
            var cache = new TrainUnitClientCache(RailGraphClientCache.CreateForEditorTest());
            var snapshot = CreateSnapshot("11111111-1111-1111-1111-111111111111");
            var notifications = 0;
            using var subscription = cache.OnSnapshotApplied.Subscribe(id =>
            {
                Assert.That(cache.TryGet(id, out var unit), Is.True);
                Assert.That(unit.IsAutoRun, Is.True);
                Assert.That(unit.TimetableCurrentIndex, Is.EqualTo(0));
                Assert.That(unit.TimetableStops[0].StationPosition, Is.EqualTo(new Vector3Int(1, 2, 3)));
                notifications++;
            });

            cache.Upsert(snapshot);
            cache.Upsert(snapshot);
            Assert.That(notifications, Is.EqualTo(2));
        }

        [Test]
        public void OverrideAllNotifiesAfterEveryTrainHasBeenInserted()
        {
            var cache = new TrainUnitClientCache(RailGraphClientCache.CreateForEditorTest());
            var first = CreateSnapshot("11111111-1111-1111-1111-111111111111");
            var second = CreateSnapshot("22222222-2222-2222-2222-222222222222");
            var ids = new List<TrainUnitInstanceId>();
            using var subscription = cache.OnSnapshotApplied.Subscribe(id =>
            {
                Assert.That(cache.Units.Count, Is.EqualTo(2));
                ids.Add(id);
            });

            cache.OverrideAll(new[] { first, second });
            CollectionAssert.AreEquivalent(new[] { first.Simulation.TrainUnitInstanceId, second.Simulation.TrainUnitInstanceId }, ids);
        }

        // 通知の観測には車両マスタや線路の初期化を必要としない
        // Observe notification order without initializing car masters or rails
        private static TrainUnitSnapshotBundle CreateSnapshot(string id)
        {
            var simulation = new TrainSimulationSnapshot(
                new TrainUnitInstanceId(Guid.Parse(id)), 0, 0, 0, 0,
                Array.Empty<TrainCarSnapshot>(), true, 0,
                new[] { new TrainTimetableStopSnapshot(new Vector3Int(1, 2, 3)) });
            return new TrainUnitSnapshotBundle(simulation, null);
        }
    }
}
