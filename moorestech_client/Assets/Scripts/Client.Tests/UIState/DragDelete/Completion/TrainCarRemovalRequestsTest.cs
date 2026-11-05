using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Entity.Removal;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using Client.Network.API.Requests.Removal;
using Client.Tests.Inventory;
using Game.Train.Unit;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Tests.Module.TestMod;
using UniRx;

namespace Client.Tests.UIState.DragDelete.Completion
{
    public class TrainCarRemovalRequestsTest
    {
        [SetUp]
        public void InitializeMaster()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void TemporaryUnitTombstoneAndCarMovedToAnotherUnitDoNotConfirmRemoval()
        {
            var cache = new TrainUnitClientCache(RailGraphClientCache.CreateForEditorTest());
            var ticks = new TrainUnitTickState();
            var events = new CapturingVanillaApiEvent();
            var sender = new Sender();
            var service = new TrainCarRemovalRequests(cache, ticks, events, sender);
            var original = new TrainUnitInstanceId(Guid.NewGuid());
            var split = new TrainUnitInstanceId(Guid.NewGuid());
            var car = new TrainCarInstanceId(100);
            cache.Upsert(Snapshot(original, car));
            var results = new List<DeleteCompletion>();
            service.Remove(car, car).Subscribe(results.Add);

            // 同tickのdelete/upsert間に一時不在でも成功しない
            // Temporary absence between same-tick delete and upsert is not success
            cache.Remove(original);
            Assert.AreEqual(DeleteCompletionKind.Pending, results[^1].Kind);
            cache.Upsert(Snapshot(split, car));
            ticks.AdvanceTick();
            Assert.AreEqual(DeleteCompletionKind.Pending, results[^1].Kind);
            cache.Remove(split);
            Assert.AreEqual(DeleteCompletionKind.Pending, results[^1].Kind);
            ticks.AdvanceTick();
            Assert.AreEqual(DeleteCompletionKind.Succeeded, results[^1].Kind);
        }

        [Test]
        public void UnconfirmedHoldsLaterRequestsAndLateDenialResumesTheQueue()
        {
            var cache = new TrainUnitClientCache(RailGraphClientCache.CreateForEditorTest());
            var ticks = new TrainUnitTickState();
            var events = new CapturingVanillaApiEvent();
            var sender = new Sender();
            var service = new TrainCarRemovalRequests(cache, ticks, events, sender);
            var unit = new TrainUnitInstanceId(Guid.NewGuid());
            var first = new TrainCarInstanceId(100);
            var second = new TrainCarInstanceId(101);
            cache.Upsert(Snapshot(unit, first, second));
            var firstResults = new List<DeleteCompletion>();
            var secondResults = new List<DeleteCompletion>();
            service.Remove(first, first).Subscribe(firstResults.Add);
            service.Remove(second, second).Subscribe(secondResults.Add);
            Assert.AreEqual(1, sender.Sent.Count);

            // 実タイマーの発火先を同期呼出しし、壁時計に依存せず保留を再現する
            // Invoke the actual timer destination synchronously to reproduce the hold without wall-clock dependence
            typeof(TrainCarRemovalRequests).GetMethod("ReportUnconfirmed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(service, null);
            Assert.AreEqual(DeleteCompletionKind.Unconfirmed, firstResults[^1].Kind);
            Assert.AreEqual(DeleteCompletionKind.Unconfirmed, secondResults[^1].Kind);
            Assert.AreEqual(1, sender.Sent.Count);
            events.Dispatch(NotificationService.EventTag, MessagePackSerializer.Serialize(NotificationMessagePack.CreateOperationDenied("denied.removeTrainCarInventoryFull", new Dictionary<string, string>())));
            Assert.AreEqual(DeleteCompletionKind.Rejected, firstResults[^1].Kind);
            Assert.AreEqual(DeleteCompletionKind.Pending, secondResults[^1].Kind);
            Assert.AreEqual(2, sender.Sent.Count);
            cache.Upsert(Snapshot(unit, first));
            ticks.AdvanceTick();
            Assert.AreEqual(DeleteCompletionKind.Succeeded, secondResults[^1].Kind);
        }

        private static TrainUnitSnapshotBundle Snapshot(TrainUnitInstanceId unit, params TrainCarInstanceId[] ids)
        {
            var cars = new List<TrainCarSnapshot>();
            foreach (var id in ids) cars.Add(new TrainCarSnapshot(id, Guid.Empty, true, 1));
            return new TrainUnitSnapshotBundle(new TrainSimulationSnapshot(unit, 0, 0, 0, 0, cars), null);
        }

        private sealed class Sender : ITrainCarRemovalSender
        {
            internal readonly List<TrainCarInstanceId> Sent = new();
            public void RemoveTrain(TrainCarInstanceId id) => Sent.Add(id);
        }
    }
}
